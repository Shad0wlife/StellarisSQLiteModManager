using StellarisSQLiteModManager.ViewModels;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace StellarisSQLiteModManager
{
    /// <summary>
    /// Interaktionslogik für MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            ViewModel = new MainPageViewModel();

            InitializeComponent();
            DataContext = ViewModel;
        }

        public MainPageViewModel ViewModel { get; }

        private void NewPlayset_Click(object sender, RoutedEventArgs e)
        {
            TextInputDialog dialog = new TextInputDialog(Properties.Resources.EnterName, Properties.Resources.EnterPlaysetNameQuestion);
            if(dialog.ShowDialog() == true)
            {
                ViewModel.CreatePlayset(dialog.ResultText);
            }
        }

        private void ClonePlayset_Click(object sender, RoutedEventArgs e)
        {
            TextInputDialog dialog = new TextInputDialog(Properties.Resources.EnterName, Properties.Resources.EnterPlaysetNameQuestion);
            if (dialog.ShowDialog() == true)
            {
                ViewModel.ClonePlayset(dialog.ResultText, ViewModel.SelectedPlayset);
            }
        }

        private void ExportPlayset_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ExportCurrentPlayset();
        }

        private void ImportPlayset_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ImportPlayset();
            //TODO Import from file, check for missing mods
        }
    }
}
