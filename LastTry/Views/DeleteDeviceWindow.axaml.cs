using Avalonia.Controls;
using Avalonia.Interactivity;

namespace LastTry.Views
{
    public partial class DeleteDeviceWindow : Window
    {
        public string DeviceName { get; private set; }
        public string RoomName { get; private set; }

        public DeleteDeviceWindow()
        {
            InitializeComponent();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            DeviceName = DeviceNameTextBox.Text;
            RoomName = RoomNameTextBox.Text;
            Close();
        }
    }
}
