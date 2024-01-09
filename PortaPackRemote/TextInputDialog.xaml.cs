using System;
using System.Collections.Generic;
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
    /// Interaction logic for TextInputDialog.xaml
    /// </summary>
    public partial class TextInputDialog : Window
    {
        public string EnteredText { get; private set; }

        public TextInputDialog(string defaultText, string labelText, string titleText)
        {
            InitializeComponent();
            EnteredText = defaultText;
            txtText.Text = defaultText;
            lblFT.Content = labelText;
            this.Title = titleText;
        }

        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            EnteredText = txtText.Text;
            DialogResult = true;
            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
