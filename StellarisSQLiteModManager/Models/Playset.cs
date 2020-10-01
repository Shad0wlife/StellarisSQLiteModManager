using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace StellarisSQLiteModManager.Models
{
    public class Playset : INotifyPropertyChanged
    {
        public Playset(string uuid, string name, bool isActive)
        {
            UUID = uuid;
            Name = name;
            IsActive = isActive;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public string UUID { get; }
        public string Name { get; }

        private bool isActive = false;
        public bool IsActive
        {
            get
            {
                return isActive;
            }
            set
            {
                if(value != isActive)
                {
                    isActive = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public override string ToString()
        {
            return Name;
        }

        protected void NotifyPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
