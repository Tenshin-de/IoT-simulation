using Avalonia.Controls;
using Avalonia.Interactivity;

namespace LastTry.Views
{
    public partial class NewDeviceWindow : Window
    {
        public string DeviceName { get; private set; }
        public string RoomName { get; private set; }
        public string BackgroundColor { get; private set; }

        public NewDeviceWindow()
        {
            InitializeComponent();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DeviceName = DeviceNameTextBox.Text;
            RoomName = RoomNameTextBox.Text;
            BackgroundColor = (BackgroundColorComboBox.SelectedItem as ComboBoxItem)?.Content.ToString();
            Close();
        }
    }
}
