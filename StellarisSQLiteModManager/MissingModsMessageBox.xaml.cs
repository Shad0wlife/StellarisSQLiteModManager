using StellarisSQLiteModManager.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
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

namespace StellarisSQLiteModManager
{
    /// <summary>
    /// Interaktionslogik für TextInputDialog.xaml
    /// </summary>
    public partial class MissingModsMessageBox : Window
    {
        public MissingModsMessageBox(string title, List<ImportMod> missingMods) {

            Title = title;
            MissingMods = missingMods;
            DataContext = this;

            InitializeComponent();
        }

        public List<ImportMod> MissingMods { get; }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void DataGrid_Hyperlink_Click(object sender, RoutedEventArgs e)
        {
            Hyperlink link = (Hyperlink)e.OriginalSource;
            Process.Start(link.NavigateUri.AbsoluteUri);
        }
    }
}
