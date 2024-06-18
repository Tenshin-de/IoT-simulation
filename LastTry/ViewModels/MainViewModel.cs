using System;
using System.Collections.ObjectModel;
using System.Data;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Windows.Input;
using Avalonia.Controls;
using Dapper;
using LastTry.Views;
using Microsoft.Data.Sqlite;
using ReactiveUI;

namespace LastTry.ViewModels
{
    public class MainViewModel : ReactiveObject
    {
        private const string DbPath = "devices.db";
        private readonly string connectionString = $"Data Source={DbPath}";

        public ICommand AddButtonCommand { get; }
        public ICommand DeleteButtonCommand { get; }
        public ReactiveCommand<NewDeviceModel, Unit> ToggleDeviceCommand { get; }

        public ObservableCollection<NewDeviceModel> Devices { get; } = new ObservableCollection<NewDeviceModel>();
        public ObservableCollection<DeviceLogModel> DeviceLogs { get; } = new ObservableCollection<DeviceLogModel>();
        public ObservableCollection<RoomViewModel> RoomViewModels { get; } = new ObservableCollection<RoomViewModel>();

        public MainViewModel()
        {
            AddButtonCommand = ReactiveCommand.CreateFromTask<Window>(AddNewDevice);
            DeleteButtonCommand = ReactiveCommand.CreateFromTask<Window>(DeleteDevice);
            ToggleDeviceCommand = ReactiveCommand.CreateFromTask<NewDeviceModel>(ToggleDeviceAsync);

            InitializeDatabase();
            LoadDevices();
            LoadDeviceLogs();
            CreateRoomTables();
            LoadRooms();
        }

        private void InitializeDatabase()
        {
            try
            {
                if (!File.Exists(DbPath))
                {
                    using (var connection = new SqliteConnection(connectionString))
                    {
                        connection.Open();
                        var deviceTableCommand = "CREATE TABLE Devices (Id INTEGER PRIMARY KEY AUTOINCREMENT, DeviceName TEXT, RoomName TEXT, BackgroundColor TEXT, IsDeviceOn INTEGER DEFAULT 0)";
                        var createDeviceTable = new SqliteCommand(deviceTableCommand, connection);
                        createDeviceTable.ExecuteNonQuery();

                        var logTableCommand = "CREATE TABLE DeviceLogs (Id INTEGER PRIMARY KEY AUTOINCREMENT, DeviceId INTEGER, Action TEXT, Timestamp DATETIME DEFAULT CURRENT_TIMESTAMP)";
                        var createLogTable = new SqliteCommand(logTableCommand, connection);
                        createLogTable.ExecuteNonQuery();
                    }
                }
            }
            catch (SqliteException ex)
            {
                Console.WriteLine($"Error initializing database: {ex.Message}");
            }
        }

        private void CreateRoomTables()
        {
            try
            {
                using (var connection = new SqliteConnection(connectionString))
                {
                    connection.Open();
                    var rooms = connection.Query<string>("SELECT DISTINCT RoomName FROM Devices");
                    foreach (var room in rooms)
                    {
                        var roomTableName = room.Replace(" ", "_");
                        var roomTableCommand = $"CREATE TABLE IF NOT EXISTS {roomTableName} (Id INTEGER PRIMARY KEY AUTOINCREMENT, DeviceName TEXT, BackgroundColor TEXT, IsDeviceOn INTEGER DEFAULT 0)";
                        var createRoomTable = new SqliteCommand(roomTableCommand, connection);
                        createRoomTable.ExecuteNonQuery();

                        // Перенесення пристроїв з основної таблиці в нову таблицю
                        var transferDevicesCommand = $@"
                            INSERT INTO {roomTableName} (DeviceName, BackgroundColor, IsDeviceOn)
                            SELECT DeviceName, BackgroundColor, IsDeviceOn
                            FROM Devices
                            WHERE RoomName = @RoomName
                            AND NOT EXISTS (
                                SELECT 1 FROM {roomTableName} WHERE DeviceName = Devices.DeviceName
                            )";
                        var transferDevices = new SqliteCommand(transferDevicesCommand, connection);
                        transferDevices.Parameters.AddWithValue("@RoomName", room);
                        transferDevices.ExecuteNonQuery();
                    }
                }
            }
            catch (SqliteException ex)
            {
                Console.WriteLine($"Error creating room tables: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task AddNewDevice(Window owner)
        {
            var newDeviceWindow = new NewDeviceWindow();
            await newDeviceWindow.ShowDialog(owner);

            if (!string.IsNullOrEmpty(newDeviceWindow.DeviceName) &&
                !string.IsNullOrEmpty(newDeviceWindow.RoomName) &&
                !string.IsNullOrEmpty(newDeviceWindow.BackgroundColor))
            {
                var newDevice = new NewDeviceModel
                {
                    DeviceName = newDeviceWindow.DeviceName,
                    RoomName = newDeviceWindow.RoomName,
                    BackgroundColor = newDeviceWindow.BackgroundColor,
                    IsDeviceOn = false // Default to OFF
                };

                SaveDevice(newDevice);
                LogDeviceAction(newDevice.Id, "Added");
                LoadDevices();
                LoadDeviceLogs();
                CreateRoomTables(); 
                LoadRooms(); 
            }
        }

        private async System.Threading.Tasks.Task DeleteDevice(Window owner)
        {
            var deleteDeviceWindow = new DeleteDeviceWindow();
            await deleteDeviceWindow.ShowDialog(owner);

            if (!string.IsNullOrEmpty(deleteDeviceWindow.DeviceName) &&
                !string.IsNullOrEmpty(deleteDeviceWindow.RoomName))
            {
                var deviceToDelete = GetDeviceByNameAndRoom(deleteDeviceWindow.DeviceName, deleteDeviceWindow.RoomName);
                if (deviceToDelete != null)
                {
                    DeleteDeviceFromDatabase(deviceToDelete.Id);
                    LogDeviceAction(deviceToDelete.Id, "Deleted");
                    LoadDevices();
                    LoadDeviceLogs();
                    LoadRooms();
                }
            }
        }

        private NewDeviceModel GetDeviceByNameAndRoom(string deviceName, string roomName)
        {
            try
            {
                using (var connection = new SqliteConnection(connectionString))
                {
                    connection.Open();
                    var query = "SELECT * FROM Devices WHERE DeviceName = @DeviceName AND RoomName = @RoomName";
                    return connection.QueryFirstOrDefault<NewDeviceModel>(query, new { DeviceName = deviceName, RoomName = roomName });
                }
            }
            catch (SqliteException ex)
            {
                Console.WriteLine($"Error retrieving device: {ex.Message}");
                return null;
            }
        }

        private void DeleteDeviceFromDatabase(int deviceId)
        {
            try
            {
                using (var connection = new SqliteConnection(connectionString))
                {
                    connection.Open();
                    var deleteCommand = "DELETE FROM Devices WHERE Id = @Id";
                    connection.Execute(deleteCommand, new { Id = deviceId });

                    var device = Devices.FirstOrDefault(d => d.Id == deviceId);
                    if (device != null)
                    {
                        var roomTableName = device.RoomName.Replace(" ", "_");
                        var roomDeleteCommand = $"DELETE FROM {roomTableName} WHERE DeviceName = @DeviceName";
                        connection.Execute(roomDeleteCommand, new { device.DeviceName });
                    }
                }
            }
            catch (SqliteException ex)
            {
                Console.WriteLine($"Error deleting device: {ex.Message}");
            }
        }

        private async System.Threading.Tasks.Task ToggleDeviceAsync(NewDeviceModel device)
        {
            device.IsDeviceOn = !device.IsDeviceOn;

            await UpdateDeviceAsync(device);
            LogDeviceAction(device.Id, device.IsDeviceOn ? "Turned On" : "Turned Off");
            LoadDevices(); 
            LoadDeviceLogs(); 
            LoadRooms();
        }

        private async System.Threading.Tasks.Task UpdateDeviceAsync(NewDeviceModel device)
        {
            try
            {
                using (var connection = new SqliteConnection(connectionString))
                {
                    connection.Open();
                    var updateCommand = "UPDATE Devices SET IsDeviceOn = @IsDeviceOn WHERE Id = @Id";
                    await connection.ExecuteAsync(updateCommand, device);

                    var roomTableName = device.RoomName.Replace(" ", "_");
                    var roomUpdateCommand = $"UPDATE {roomTableName} SET IsDeviceOn = @IsDeviceOn WHERE DeviceName = @DeviceName";
                    await connection.ExecuteAsync(roomUpdateCommand, new { device.IsDeviceOn, device.DeviceName });
                }
            }
            catch (SqliteException ex)
            {
                Console.WriteLine($"Error updating device: {ex.Message}");
            }
        }

        private void SaveDevice(NewDeviceModel device)
        {
            try
            {
                using (var connection = new SqliteConnection(connectionString))
                {
                    connection.Open();
                    var insertCommand = "INSERT INTO Devices (DeviceName, RoomName, BackgroundColor, IsDeviceOn) VALUES (@DeviceName, @RoomName, @BackgroundColor, @IsDeviceOn)";
                    connection.Execute(insertCommand, device);

                    var roomTableName = device.RoomName.Replace(" ", "_");
                    var roomInsertCommand = $"INSERT INTO {roomTableName} (DeviceName, BackgroundColor, IsDeviceOn) VALUES (@DeviceName, @BackgroundColor, @IsDeviceOn)";
                    connection.Execute(roomInsertCommand, new { DeviceName = device.DeviceName, BackgroundColor = device.BackgroundColor, IsDeviceOn = device.IsDeviceOn });
                }
            }
            catch (SqliteException ex)
            {
                Console.WriteLine($"Error saving device: {ex.Message}");
            }
        }

        private void LoadDevices()
        {
            Devices.Clear();
            try
            {
                using (var connection = new SqliteConnection(connectionString))
                {
                    connection.Open();
                    var devices = connection.Query<NewDeviceModel>("SELECT * FROM Devices");
                    foreach (var device in devices)
                    {
                        Devices.Add(device);
                    }
                }
            }
            catch (SqliteException ex)
            {
                Console.WriteLine($"Error loading devices: {ex.Message}");
            }
        }

        private void LoadDeviceLogs()
        {
            DeviceLogs.Clear(); // Clear the current list to avoid duplicates
            try
            {
                using (var connection = new SqliteConnection(connectionString))
                {
                    connection.Open();
                    var logs = connection.Query<DeviceLogModel>("SELECT * FROM DeviceLogs ORDER BY Timestamp DESC");
                    foreach (var log in logs)
                    {
                        DeviceLogs.Add(log);
                    }
                }
            }
            catch (SqliteException ex)
            {
                Console.WriteLine($"Error loading device logs: {ex.Message}");
            }
        }

        private void LogDeviceAction(int deviceId, string action)
        {
            try
            {
                using (var connection = new SqliteConnection(connectionString))
                {
                    connection.Open();
                    var logCommand = "INSERT INTO DeviceLogs (DeviceId, Action) VALUES (@DeviceId, @Action)";
                    connection.Execute(logCommand, new { DeviceId = deviceId, Action = action });
                }
            }
            catch (SqliteException ex)
            {
                Console.WriteLine($"Error logging device action: {ex.Message}");
            }
        }

        private void LoadRooms()
        {
            RoomViewModels.Clear(); // Clear the current list to avoid duplicates
            try
            {
                using (var connection = new SqliteConnection(connectionString))
                {
                    connection.Open();
                    var rooms = connection.Query<string>("SELECT DISTINCT RoomName FROM Devices");
                    foreach (var room in rooms)
                    {
                        var roomTableName = room.Replace(" ", "_");
                        var roomDevices = connection.Query<NewDeviceModel>($"SELECT * FROM {roomTableName}");
                        RoomViewModels.Add(new RoomViewModel
                        {
                            RoomName = room,
                            Devices = new ObservableCollection<NewDeviceModel>(roomDevices)
                        });
                    }
                }
            }
            catch (SqliteException ex)
            {
                Console.WriteLine($"Error loading rooms: {ex.Message}");
            }
        }
    }
    public class NewDeviceModel
    {
        public int Id { get; set; }
        public string DeviceName { get; set; }
        public string RoomName { get; set; }
        public string BackgroundColor { get; set; }
        public bool IsDeviceOn { get; set; }

        public string Status => IsDeviceOn ? "ON" : "OFF";
    }

    public class DeviceLogModel
    {
        public int Id { get; set; }
        public int DeviceId { get; set; }
        public string Action { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class RoomViewModel : ReactiveObject
    {
        public string RoomName { get; set; }
        public ObservableCollection<NewDeviceModel> Devices { get; set; }
    }
}
