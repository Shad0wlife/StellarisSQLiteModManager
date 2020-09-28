using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace StellarisSQLiteModManager.Models
{
    public class ImportMod
    {
        public ImportMod(string position, string displayName, string steamID, string paradoxID, bool active)
        {
            Position = position;
            Modname = displayName;
            ParadoxID = paradoxID;
            SteamID = steamID;
            Active = active;
        }

        public string Modname { get; }
        public string ParadoxID { get; }
        public string SteamID { get; }
        public string Position { get; }
        public bool Active { get; }

        public string SteamLink { 
            get
            {
                if(SteamID == null)
                {
                    return null;
                }
                return $"https://steamcommunity.com/sharedfiles/filedetails/?id={SteamID}";
            } 
        }

        public string ParadoxLink
        {
            get
            {
                if (ParadoxID == null)
                {
                    return null;
                }
                return $"https://mods.paradoxplaza.com/mods/{ParadoxID}/Any";
            }
        }
    }
}
