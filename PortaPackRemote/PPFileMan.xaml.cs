using PortaPackRemoteApi;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

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
