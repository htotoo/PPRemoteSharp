using Microsoft.Win32;
using PortaPackRemoteApi;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace PortaPackRemote
{
    /// <summary>
    /// Interaction logic for PPFileMan.xaml
    /// </summary>
    public partial class PPFileMan : Window
    {
        private PPApi _api;
        private string currPath = "/";
        private List<string> dirlist = new List<string>();
        public PPFileMan(PPApi api)
        {
            _api = api;
            InitializeComponent();
            RefreshPath();
        }
        private async  Task RefreshPath()
        {
            Dispatcher.Invoke(() =>
            {
                Mouse.OverrideCursor = Cursors.Wait;
            });
                var cp = currPath;
            if (cp.Length>1 )
            {
                cp = cp.TrimEnd('/');
            }
            var o = await _api.LS(cp);
            Trace.WriteLine(o);
            dirlist.Clear();
            if (cp != "/") dirlist.Add("..");
            if (o.Count>0 )
            {
                for (int i= 0; i<o.Count; i++)
                {
                    if (i == 0 && o[i].StartsWith("ls ")) continue;
                    dirlist.Add(o[i]);
                }
            }
            Dispatcher.Invoke(()=>
            {
                dirListView.ItemsSource = null;
                dirListView.ItemsSource = dirlist;
                
                lblPath.Content = currPath;
                Mouse.OverrideCursor = null;
            });
        }

         private void ListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dirListView.SelectedItem != null)
            {
                var sel = (string)dirListView.SelectedItem;
                if (sel.EndsWith("/") || sel == "..")
                {
                    //path
                    if (sel == "..") currPath = CdUp(currPath); else currPath += sel;
                    RefreshPath();
                }
                else
                {
                    if (sel.EndsWith(".bin"))
                    {
                        btnFlash_Click(sender, e);
                    }
                    else
                    {
                        //download file
                        btnDownload_Click(sender, e);
                    }
                }
            }
        }
        static string CdUp(string path)
        {
            // Ensure the path is not already at the root
            if (path.Length > 1)
            {
                path = path.TrimEnd('/'); //remove trailing
                // Find the last occurrence of '/'

                int lastSlashIndex = path.LastIndexOf('/');

                // If '/' is found, remove the last segment
                if (lastSlashIndex >= 0)
                {
                    path = path.Substring(0, lastSlashIndex + 1);
                }
            }

            return path;
        }
        private void btnPwdUp_Click(object sender, RoutedEventArgs e)
        {
            currPath = CdUp(currPath);
            RefreshPath();
        }

        private async void btnDel_Click(object sender, RoutedEventArgs e)
        {
            if (dirListView.SelectedItem != null)
            {
                var sel = (string)dirListView.SelectedItem;
                if (sel.EndsWith("/") || sel == "..")
                {
                    MessageBox.Show("Only files can be deleted.");
                }
                else
                {
                    var file = currPath + sel;
                    var res =  MessageBox.Show("Are you sure you wanna delete \n" + file + " ?","Delete file", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (res == MessageBoxResult.Yes)
                    {
                        await _api.SendFileDel(file);
                        RefreshPath() ;
                    }
                }
            }
        }

        private async void btnDownload_Click(object sender, RoutedEventArgs e)
        {
            if (dirListView.SelectedItem == null) return;
            var sel = (string)dirListView.SelectedItem;
            if (sel.EndsWith("/") || sel == "..")
            {
                MessageBox.Show("Only files can be deleted.");
                return;
            }
            FileDialog saveFileDialog = new SaveFileDialog();
            string ext = Path.GetExtension(sel);
            saveFileDialog.DefaultExt =  ext;
            saveFileDialog.FileName =  sel;
            saveFileDialog.Filter = ext + " (*"+ext+")|*"+ext;

            saveFileDialog.ShowHiddenItems = true;
            


            bool? result = saveFileDialog.ShowDialog();
            if (result != null && result == true)
            {
                Action<int> progressCallback = value =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        progress.Maximum = 100;
                        progress.Value = value;
                        progress.InvalidateVisual();
                    });
                };

                string src = currPath + sel;
                string dst = saveFileDialog.FileName;
                Mouse.OverrideCursor = Cursors.Wait;
                await _api.DownloadFile(src, dst, progressCallback);
                Mouse.OverrideCursor = null;
                Dispatcher.Invoke(() =>
                {
                    progress.Value = 0;
                });
            }
        }

        private async void btnUpload_Click(object sender, RoutedEventArgs e)
        {
            FileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "*.*" + " (*.*)|*.*";
            openFileDialog.CheckFileExists = true;
            bool? result = openFileDialog.ShowDialog();
            if (result != null && result == true)
            {
                Action<int> progressCallback = value =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        progress.Maximum = 100;
                        progress.Value = value;
                        progress.InvalidateVisual();
                    });
                };
                
                string src = openFileDialog.FileName;
                string srcBase = Path.GetFileName(src);
                var dlg = new TextInputDialog(currPath + srcBase, "Uploaded file name");
                var res = dlg.ShowDialog();
                if (res != null && res == true)
                {
                    string dst = dlg.EnteredText;
                    Mouse.OverrideCursor = Cursors.Wait;
                    await _api.UploadFile(src, dst, progressCallback);
                    Mouse.OverrideCursor = null;
                    Dispatcher.Invoke(() =>
                    {
                        progress.Value = 0;
                    });
                    await RefreshPath();
                }
            }
        }

      
        private async void btnFlash_Click(object sender, RoutedEventArgs e)
        {
            if (dirListView.SelectedItem == null) return;
            var sel = (string)dirListView.SelectedItem;
            if (!sel.EndsWith(".bin"))
            {
                MessageBox.Show("Only BIN files can be flashed (yet)");
                return;
            }
            if (MessageBox.Show("Are you sure you want to flash this image: " + sel, "Flash", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                await _api.SendFlash(currPath + sel);
                Close(); //flashing will reset serial too
            }
            
        }
    }
}
