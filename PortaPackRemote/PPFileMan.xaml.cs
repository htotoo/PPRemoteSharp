using PortaPackRemoteApi;
using System.Diagnostics;
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
        private async void RefreshPath()
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
    }
}
