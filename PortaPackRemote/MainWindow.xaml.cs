using PortaPackRemoteApi;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

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

        private async void btnConnDisconn_Click(object sender, RoutedEventArgs e)
        {
            if (api.IsConnected()) { 
                api.Close(); 
            }
            else
            {
                if (listSerials.SelectedIndex != -1)
                {
                    await api.OpenPort(listSerials.Text);
                    await RefreshScreen();
                }
            }
        }

        private async Task DoAutoRefresh()
        {
            if (chkAutoRefresh.IsChecked == true)
            {
                await RefreshScreen();
            }
        }

        private async void btnUp_Click(object sender, RoutedEventArgs e)
        {
            await api.SendButton(PPApi.ButtonState.BUTTON_UP);
            await DoAutoRefresh();
        }

        private async void btnRight_Click(object sender, RoutedEventArgs e)
        {
            await api.SendButton(PPApi.ButtonState.BUTTON_RIGHT);
            await DoAutoRefresh();
        }

        private async void btnDown_Click(object sender, RoutedEventArgs e)
        {
            await api.SendButton(PPApi.ButtonState.BUTTON_DOWN);
            await DoAutoRefresh();
        }

        private async void btnLeft_Click(object sender, RoutedEventArgs e)
        {
            await api.SendButton(PPApi.ButtonState.BUTTON_LEFT);
            await DoAutoRefresh();
        }

        private async void btnEnter_Click(object sender, RoutedEventArgs e)
        {
            await api.SendButton(PPApi.ButtonState.BUTTON_ENTER);
            await DoAutoRefresh();
        }

        private async void btnRotLeft_Click(object sender, RoutedEventArgs e)
        {
            await api.SendButton(PPApi.ButtonState.BUTTON_ROTLEFT);
            await DoAutoRefresh();
        }

        private async void btnRotRight_Click(object sender, RoutedEventArgs e)
        {
            await api.SendButton(PPApi.ButtonState.BUTTON_ROTRIGHT);
            await DoAutoRefresh();
        }

        private void btnRestart_Click(object sender, RoutedEventArgs e)
        {
            api.SendRestart();
        }

        private void btnHfMode_Click(object sender, RoutedEventArgs e)
        {
            api.SendHFMode();
        }

        private async void btnScreenshot_Click(object sender, RoutedEventArgs e)
        {
            await api.SendScreenshot();
        }

        private void screen_MouseUp(object sender, MouseButtonEventArgs e)
        {
            //send click
        }

        private async void screen_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta < 0) { await api.SendButton(PPApi.ButtonState.BUTTON_ROTRIGHT);  }
            if (e.Delta > 0) { await api.SendButton(PPApi.ButtonState.BUTTON_ROTLEFT);  }
            e.Handled = true;
            await DoAutoRefresh();
        }

        private async void screen_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) { await api.SendButton(PPApi.ButtonState.BUTTON_ENTER); e.Handled = true; }
            if (e.Key == Key.Left) { await api.SendButton(PPApi.ButtonState.BUTTON_LEFT); e.Handled = true; }
            if (e.Key == Key.Right) { await api.SendButton(PPApi.ButtonState.BUTTON_RIGHT); e.Handled = true; }
            if (e.Key == Key.Up) { await api.SendButton(PPApi.ButtonState.BUTTON_UP); e.Handled = true; }
            if (e.Key == Key.Down) { await api.SendButton(PPApi.ButtonState.BUTTON_DOWN); e.Handled = true; }

            if (e.Handled) { await DoAutoRefresh(); }
            
        }

        private async void btnFileMan_Click(object sender, RoutedEventArgs e)
        {
            var browser = new PPFileMan(api);
            this.Hide();
            browser.ShowDialog();            
            this.Show();
        }

        private void btnPortRefresh_Click(object sender, RoutedEventArgs e)
        {
            listSerials.ItemsSource = api.GetPorts();
        }

        private static System.IO.Stream ConvertBitmapToMemoryStream(System.Drawing.Bitmap bitmap)
        {
            System.IO.MemoryStream stream = new System.IO.MemoryStream();
            bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            stream.Position = 0;
            return stream;
        }

        private async Task RefreshScreen()
        {
            Dispatcher.Invoke(() =>  {  Mouse.OverrideCursor = Cursors.Wait; });
            var bmp = await api.SendScreenFrameShort();
            BitmapImage bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.StreamSource = ConvertBitmapToMemoryStream(bmp);
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.EndInit();
            await Dispatcher.InvokeAsync(() => { screen.Source = bitmapImage; Mouse.OverrideCursor = null; });
        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
            await RefreshScreen();
        }
    }
}