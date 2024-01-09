using PortaPackRemoteApi;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

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
            btnEnter.Focus();
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
                screen.Source = null;
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
            if (btnConnDisconn.Content.Equals("Disconnect")) { 
                api.Close(); 
            }
            else
            {
                if (listSerials.SelectedIndex != -1)
                {
                    await api.OpenPort(listSerials.Text);
                    await DoAutoRefresh();
                    btnEnter.Focus();
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

        private async Task SendKeyStroke(PPApi.ButtonState state)
        {
            try
            {
                await api.SendButton(state);
                await DoAutoRefresh();
            }
            catch (Exception ex)
            {
                ShowError(ex.ToString(), "Error sending keystroke.");
            }
        }

        private async void btnUp_Click(object sender, RoutedEventArgs e)
        {
            await SendKeyStroke(PPApi.ButtonState.BUTTON_UP);
            
        }

        private async void btnRight_Click(object sender, RoutedEventArgs e)
        {
            await SendKeyStroke(PPApi.ButtonState.BUTTON_RIGHT);
        }

        private async void btnDown_Click(object sender, RoutedEventArgs e)
        {
            await SendKeyStroke(PPApi.ButtonState.BUTTON_DOWN);
        }

        private async void btnLeft_Click(object sender, RoutedEventArgs e)
        {
            await SendKeyStroke(PPApi.ButtonState.BUTTON_LEFT);
        }

        private async void btnEnter_Click(object sender, RoutedEventArgs e)
        {
            await SendKeyStroke(PPApi.ButtonState.BUTTON_ENTER);
        }

        private async void btnRotLeft_Click(object sender, RoutedEventArgs e)
        {
            await SendKeyStroke(PPApi.ButtonState.BUTTON_ROTLEFT);
        }

        private async void btnRotRight_Click(object sender, RoutedEventArgs e)
        {
            await SendKeyStroke(PPApi.ButtonState.BUTTON_ROTRIGHT);
        }

        private void btnRestart_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                api.SendRestart();
            }
            catch (Exception ex)
            {
                ShowError(ex.ToString(), "Error requesting reboot.");
            }
        }

        private async void btnHfMode_Click(object sender, RoutedEventArgs e)
        {
            try
            {
               await api.SendHFMode();
            }
            catch (Exception ex)
            {
                ShowError(ex.ToString(), "Error requesting HF mode.");
            }
        }

        private async void btnScreenshot_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await api.SendScreenshot();
            }
            catch (Exception ex) {
                ShowError(ex.ToString(), "Error requesting screenshot.");
            }
        }

        private async void screen_MouseUp(object sender, MouseButtonEventArgs e)
        {
            btnEnter.Focus();
            await api.SendTouch((int)e.GetPosition((IInputElement)sender).X, (int)e.GetPosition((IInputElement)sender).Y);
            await DoAutoRefresh();
        }

        private async void screen_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Delta < 0) { await SendKeyStroke(PPApi.ButtonState.BUTTON_ROTRIGHT); }
            if (e.Delta > 0) { await SendKeyStroke(PPApi.ButtonState.BUTTON_ROTLEFT); }
            e.Handled = true;
        }

        private async void screen_KeyUp(object sender, KeyEventArgs e)
        {
            if (txtKeyboard.IsKeyboardFocused) return;
            if (e.Key == Key.Enter) { await SendKeyStroke(PPApi.ButtonState.BUTTON_ENTER); e.Handled = true; }
            if (e.Key == Key.Left) { await SendKeyStroke(PPApi.ButtonState.BUTTON_LEFT); e.Handled = true; }
            if (e.Key == Key.Right) { await SendKeyStroke(PPApi.ButtonState.BUTTON_RIGHT); e.Handled = true; }
            if (e.Key == Key.Up) { await SendKeyStroke(PPApi.ButtonState.BUTTON_UP); e.Handled = true; }
            if (e.Key == Key.Down) { await SendKeyStroke(PPApi.ButtonState.BUTTON_DOWN); e.Handled = true; }
            if (e.Handled) btnEnter.Focus();
        }

        private async void btnFileMan_Click(object sender, RoutedEventArgs e)
        {
            var browser = new PPFileMan(api);
            try
            {
                this.Hide();
                browser.ShowDialog();
                this.Show();
            } catch (Exception ex)
            {
                browser.Close();
                this.Show();
                ShowError(ex.ToString(), "File operation fatal error.");
            }
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
            try
            {
                Dispatcher.Invoke(() => { Mouse.OverrideCursor = Cursors.Wait; });
                var bmp = await api.SendScreenFrameShort();
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = ConvertBitmapToMemoryStream(bmp);
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                await Dispatcher.InvokeAsync(() => { screen.Source = bitmapImage; Mouse.OverrideCursor = null; });
            }
            catch(Exception ex)
            {
                ShowError(ex.ToString(), "Error refreshing screen");
            }
        }

        private async void btnRefreshScreen_Click(object sender, RoutedEventArgs e)
        {
            try { 
                await RefreshScreen();
            } catch (Exception ex) 
            {
                ShowError(ex.ToString(), "Error refreshing screen");
            }
            
        }


        private void ShowError(string message, string title = "")
        {
            Dispatcher.Invoke(() => { MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error); });
            
        }

        private async void btnSd_Click(object sender, RoutedEventArgs e)
        {
            await api.SendSDOUsb();
        }

        private void chkChrBc_Checked(object sender, RoutedEventArgs e)
        {
            if (btnKeySend != null) btnKeySend.IsEnabled = !(bool)chkChrBc.IsChecked;
            if (txtKeyboard != null) txtKeyboard.Text = "";
        }

        private async void txtKeyboard_KeyUp(object sender, KeyEventArgs e)
        {
            if ((bool)chkChrBc.IsChecked)
            {
                e.Handled = true;
                string toSend = txtKeyboard.Text;
                if (e.Key == Key.Back) toSend = "\b";
                if (e.Key == Key.Delete) toSend = "\b";
                txtKeyboard.Text = "";
                txtKeyboard.IsEnabled = false;
                await api.SendKeyboard(toSend);
                await DoAutoRefresh();
                txtKeyboard.IsEnabled = true;
                txtKeyboard.Focus();
            }
            e.Handled = true;
        }

        private async void btnKeySend_Click(object sender, RoutedEventArgs e)
        {
            string toSend = txtKeyboard.Text;
            txtKeyboard.Text = "";
            txtKeyboard.IsEnabled = false;
            await api.SendKeyboard(toSend);
            await DoAutoRefresh();
            txtKeyboard.IsEnabled = true;
            txtKeyboard.Focus();
        }

        private async void btnBackspace_Click(object sender, RoutedEventArgs e)
        {
            string toSend = "\b";
            txtKeyboard.Text = "";
            txtKeyboard.IsEnabled = false;
            await api.SendKeyboard(toSend);
            await DoAutoRefresh();
            txtKeyboard.IsEnabled = true;
            txtKeyboard.Focus();
        }
    }
}