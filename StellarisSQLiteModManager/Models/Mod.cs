using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace StellarisSQLiteModManager.Models
{
    public class Mod
    {
        public Mod(string uuid, string displayName, string paradoxID, string steamID, string source)
        {
            UUID = uuid;
            Modname = displayName;
            ParadoxID = paradoxID;
            SteamID = steamID;
            Source = source;
        }

        public string UUID { get; }
        public string Modname { get; }
        public string ParadoxID { get; }
        public string SteamID { get; }
        public string Source { get; }
    }
}
