using PortaPackRemoteApi;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace PortaPackRemote
{
 
    public partial class MainWindow : Window
    {
        PPApi api = new PPApi();

        public MainWindow()
        {
            InitializeComponent();
            listSerials.ItemsSource = api.GetPorts();
            api.SerialOpened += Api_SerialOpened;
            api.SerialClosed += Api_SerialClosed;
            api.SerialError += Api_SerialError;
        }

        private void Api_SerialError(object? sender, EventArgs e)
        {
            Api_SerialClosed(sender, e);
        }

        private void Api_SerialClosed(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                btnConnDisconn.Content = "Connect";
            } );
        }

        private void Api_SerialOpened(object? sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                btnConnDisconn.Content = "Disconnect";
            });
        }

        private void btnConnDisconn_Click(object sender, RoutedEventArgs e)
        {
            if (api.IsConnected()) { 
                api.Close(); 
            }
            else
            {
                if (listSerials.SelectedIndex != -1)
                {
                    api.OpenPort(listSerials.Text);
                }
            }
        }

        private void btnUp_Click(object sender, RoutedEventArgs e)
        {
            api.SendButton(PPApi.ButtonState.BUTTON_UP);
        }

        private void btnRight_Click(object sender, RoutedEventArgs e)
        {
            api.SendButton(PPApi.ButtonState.BUTTON_RIGHT);
        }

        private void btnDown_Click(object sender, RoutedEventArgs e)
        {
            api.SendButton(PPApi.ButtonState.BUTTON_DOWN);
        }

        private void btnLeft_Click(object sender, RoutedEventArgs e)
        {
            api.SendButton(PPApi.ButtonState.BUTTON_LEFT);
        }

        private void btnEnter_Click(object sender, RoutedEventArgs e)
        {
            api.SendButton(PPApi.ButtonState.BUTTON_ENTER);
        }

        private void btnRotLeft_Click(object sender, RoutedEventArgs e)
        {
            api.SendButton(PPApi.ButtonState.BUTTON_ROTLEFT);
        }

        private void btnRotRight_Click(object sender, RoutedEventArgs e)
        {
            api.SendButton(PPApi.ButtonState.BUTTON_ROTRIGHT);
        }

        private void btnRestart_Click(object sender, RoutedEventArgs e)
        {
            api.SendRestart();
        }

        private void btnHfMode_Click(object sender, RoutedEventArgs e)
        {
            api.SendHFMode();
        }

        private void btnScreenshot_Click(object sender, RoutedEventArgs e)
        {
            api.SendScreenshot();
        }

        private void screen_MouseUp(object sender, MouseButtonEventArgs e)
        {
            //send click
        }

        private void screen_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta < 0) { api.SendButton(PPApi.ButtonState.BUTTON_ROTRIGHT);}
            if (e.Delta > 0) { api.SendButton(PPApi.ButtonState.BUTTON_ROTLEFT); }
            e.Handled = true;
        }

        private void screen_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { api.SendButton(PPApi.ButtonState.BUTTON_ENTER); e.Handled = true; }
            if (e.Key == Key.Left) { api.SendButton(PPApi.ButtonState.BUTTON_LEFT); e.Handled = true; }
            if (e.Key == Key.Right) { api.SendButton(PPApi.ButtonState.BUTTON_RIGHT); e.Handled = true; }
            if (e.Key == Key.Up) { api.SendButton(PPApi.ButtonState.BUTTON_UP); e.Handled = true; }
            if (e.Key == Key.Down) { api.SendButton(PPApi.ButtonState.BUTTON_DOWN); e.Handled = true; }
            
        }

        private async void btnFileMan_Click(object sender, RoutedEventArgs e)
        {
            var browser = new PPFileMan(api);
            browser.ShowDialog();
            
        }

        private void btnPortRefresh_Click(object sender, RoutedEventArgs e)
        {
            listSerials.ItemsSource = api.GetPorts();
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            /*string[] asd = (await api.ReadReply()).ToArray();
            Console.WriteLine(asd[0]);*/

        }
    }
}