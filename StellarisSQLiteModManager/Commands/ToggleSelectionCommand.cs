using StellarisSQLiteModManager.Database;
using StellarisSQLiteModManager.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;

namespace StellarisSQLiteModManager.Commands
{
    public class ToggleSelectionCommand : ICommand
    {
        public ToggleSelectionCommand(ObservableCollection<ModInPlayset> mods, bool enable)
        {
            Mods = mods;
            Enable = enable;
        }

        private ObservableCollection<ModInPlayset> Mods { get; set; }
        private bool Enable { get; }

        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter)
        {
            return true;
        }

        public void Execute(object parameter)
        {
            if(parameter is ObservableCollection<object> collection)
            {
                foreach(object obj in collection)
                {
                    if (obj is ModInPlayset mod)
                    {
                        mod.Active = Enable;
                    }
                }
            }
        }
    }
}
