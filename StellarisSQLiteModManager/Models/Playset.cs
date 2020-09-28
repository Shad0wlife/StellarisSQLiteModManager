using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StellarisSQLiteModManager.Models
{
    public class Playset
    {
        public Playset(string uuid, string name)
        {
            UUID = uuid;
            Name = name;
        }

        public string UUID { get; }
        public string Name { get; }

        public override string ToString()
        {
            return Name;
        }
    }
}
