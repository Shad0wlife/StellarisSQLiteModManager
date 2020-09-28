using Microsoft.Data.Sqlite;
using Microsoft.Win32;
using StellarisSQLiteModManager.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Navigation;

namespace StellarisSQLiteModManager.Database
{
    public class DatabaseFunctions
    {
        private DatabaseFunctions()
        {
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            connectionPath = $@"{documentsPath}\Paradox Interactive\Stellaris\launcher-v2.sqlite";

            if (!File.Exists(connectionPath))
            {
                connectionPath = GetCustomFile();
            }
            else
            {
                Valid = true;
                Debug.WriteLine("Default Database file found!");
            }
        }

        private static DatabaseFunctions singleton = null;
        public static DatabaseFunctions Singleton { 
            get
            {
                //Lazy AF
                if(singleton == null)
                {
                    Init();
                }
                return singleton;
            } 
        }

        public static bool FixLoadOrder = true;

        private static void Init()
        {
            singleton = new DatabaseFunctions();
        }

        private string GetCustomFile()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Stellaris Launcher Database|launcher-v2.sqlite";
            openFileDialog.CheckFileExists = true;
            bool? result = openFileDialog.ShowDialog();
            if (result == true)
            {
                Valid = true;
                return openFileDialog.FileName;
            }
            Valid = false;
            return null;
        }

        private string connectionPath;

        public bool Valid { get; private set; }

        private string GetStringOrNull(SqliteDataReader reader, int index)
        {
            if (reader.IsDBNull(index))
            {
                return null;
            }
            else
            {
                return reader.GetString(index);
            }
        }

        public void CreateNewPlayset(string newPlaysetName)
        {
            using(SqliteConnection connection = new SqliteConnection($"Data Source={connectionPath}"))
            {
                connection.Open();
                createNewPlaysetOnConnection(newPlaysetName, connection);
            }
        }

        public void ClonePlaysetToNew(string newPlaysetName, Playset fromPlayset)
        {
            using (SqliteConnection connection = new SqliteConnection($"Data Source={connectionPath}"))
            {
                connection.Open();
                SqliteTransaction transcation = connection.BeginTransaction();

                Playset newPlayset = createNewPlaysetOnConnection(newPlaysetName, connection);

                try
                {
                    SqliteCommand fillCommand = connection.CreateCommand();
                    fillCommand.CommandText = $"INSERT INTO playsets_mods(playsetId, modId, position, enabled) SELECT '{newPlayset.UUID}', modId, position, enabled FROM playsets_mods WHERE playsetId = @oldUUID;";
                    fillCommand.Parameters.AddWithValue("@oldUUID", fromPlayset.UUID);
                    fillCommand.Prepare();

                    Debug.WriteLine("Command: " + fillCommand.CommandText + " with oldUUID = " + fromPlayset.UUID);

                    fillCommand.ExecuteNonQuery();

                    transcation.Commit();
                }catch(Exception e)
                {
                    Debug.WriteLine("Exception: " + e.Message + "\r\nRolling Back!");
                    transcation.Rollback();
                }
            }
        }

        private Playset createNewPlaysetOnConnection(string newPlaysetName, SqliteConnection openConnection)
        {
            Debug.WriteLine("Starting insertion of new Playset.");
            string uuid = Guid.NewGuid().ToString();
            while (CheckForExistingPlaysetUUID(uuid, openConnection))
            {
                uuid = Guid.NewGuid().ToString();
            }

            SqliteCommand createCommand = openConnection.CreateCommand();
            createCommand.CommandText = $"INSERT INTO playsets(id, name, isActive, loadOrder) VALUES(@uuid, @playsetname, FALSE, 'custom');";
            createCommand.Parameters.AddWithValue("@uuid", uuid);
            createCommand.Parameters.AddWithValue("@playsetname", newPlaysetName);
            createCommand.Prepare();

            createCommand.ExecuteNonQuery();

            return new Playset(uuid, newPlaysetName);
        }

        private bool CheckForExistingPlaysetUUID(string uuid, SqliteConnection openConnection)
        {
            SqliteCommand command = openConnection.CreateCommand();
            command.CommandText = $"SELECT Count(*) FROM playsets WHERE id = '{uuid}';";

            object result = command.ExecuteScalar();
            if(result is long n)
            {
                if(n > 0)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                Debug.WriteLine("Scalar was not long!");
                return true;
            }
        }

        public void ReloadPlaysets(ObservableCollection<Playset> collection)
        {
            collection.Clear();

            using (SqliteConnection connection = new SqliteConnection($"Data Source={connectionPath}"))
            {
                connection.Open();

                SqliteCommand command = connection.CreateCommand();
                command.CommandText = $"SELECT id, name FROM playsets;";

                SqliteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    string uuid = reader.GetString(0);
                    string name = reader.GetString(1);
                    collection.Add(new Playset(uuid, name));
                }
            }

            Debug.WriteLine($"Now has {collection.Count} entries.");
        }

        public void ReloadAllMods(Dictionary<string, Mod> dict)
        {
            dict.Clear();

            using (SqliteConnection connection = new SqliteConnection($"Data Source={connectionPath}"))
            {
                connection.Open();

                SqliteCommand command = connection.CreateCommand();
                command.CommandText = $"SELECT id, displayName, pdxId, steamId, source FROM mods;";

                SqliteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    string uuid = reader.GetString(0);
                    string name = reader.GetString(1);
                    string pdxId = GetStringOrNull(reader, 2);
                    string steamId = GetStringOrNull(reader, 3);
                    string source = reader.GetString(4);
                    dict.Add(uuid, new Mod(uuid, name, pdxId, steamId, source));
                }
            }

            Debug.WriteLine($"Now has {dict.Count} entries.");
        }

        public void ReloadPlaysetMods(Dictionary<string, Mod> allMods, ObservableCollection<ModInPlayset> toReloadSelected, ObservableCollection<Mod> toReloadUnselected, Playset playset)
        {
            toReloadSelected.Clear();
            toReloadUnselected.Clear();

            if(playset == null)
            {
                return;
            }

            using (SqliteConnection connection = new SqliteConnection($"Data Source={connectionPath}"))
            {
                connection.Open();

                SqliteCommand command = connection.CreateCommand();
                command.CommandText = $"SELECT modId, position, enabled FROM playsets_mods WHERE playsetID = @playsetID ORDER BY position ASC;";
                command.Parameters.AddWithValue("@playsetID", playset.UUID);
                command.Prepare();

                SqliteDataReader reader = command.ExecuteReader();
                long fixCnt = -1;
                bool first = true;

                Debug.WriteLineIf(!FixLoadOrder, "Possibly wrong load Order due to string ordering instead of correct numbers. FixLoadOrder is off.");
                Debug.Assert(ModInPlayset.DBIndexToLongIndex(ModInPlayset.LongIndexToDbIndex(0)) == 0, "Index Test 0 Failed!");
                Debug.Assert(ModInPlayset.DBIndexToLongIndex(ModInPlayset.LongIndexToDbIndex(33)) == 33, "Index Test 33 Failed!");
                Debug.Assert(ModInPlayset.DBIndexToLongIndex(ModInPlayset.LongIndexToDbIndex(1300)) == 1300, "Index Test 1300 Failed!");

                while (reader.Read())
                {
                    string modID = reader.GetString(0);
                    string position = reader.GetString(1);
                    bool enabled = reader.GetBoolean(2);

                    if (first)
                    {
                        fixCnt = ModInPlayset.DBIndexToLongIndex(position);
                        Debug.WriteLine("Fixcnt: " + fixCnt);
                        first = false;
                    }

                    if (FixLoadOrder) {
                        toReloadSelected.Add(new ModInPlayset(allMods[modID], playset, fixCnt, enabled, true));
                        fixCnt++;
                    }
                    else
                    {
                        toReloadSelected.Add(new ModInPlayset(allMods[modID], playset, ModInPlayset.DBIndexToLongIndex(position), enabled, true));
                    }
                }

                if (FixLoadOrder)
                {
                    FixPlaysetLoadorderPositions(toReloadSelected, connection);
                }
            }

            foreach(Mod mod in allMods.Values)
            {
                if(!SelectedHasMod(mod, toReloadSelected))
                {
                    toReloadUnselected.Add(mod);
                }
            }

            Debug.WriteLine($"Now has {toReloadSelected.Count} entries.");
            Debug.WriteLine($"Now has {toReloadUnselected.Count} entries.");
        }

        private void FixPlaysetLoadorderPositions(ObservableCollection<ModInPlayset> loadOrder, SqliteConnection connection)
        {
            SqliteTransaction transcation = connection.BeginTransaction();

            try
            {
                foreach (ModInPlayset currentMod in loadOrder) {
                    SqliteCommand fixCommand = connection.CreateCommand();
                    fixCommand.CommandText = $"UPDATE playsets_mods SET position = @newPos WHERE modID = @modID AND playsetID = @playsetID;";
                    fixCommand.Parameters.AddWithValue("@newPos", currentMod.DatabaseIndex);
                    fixCommand.Parameters.AddWithValue("@modID", currentMod.TargetMod.UUID);
                    fixCommand.Parameters.AddWithValue("@playsetID", currentMod.TargetPlayset.UUID);
                    fixCommand.Prepare();

                    int res = fixCommand.ExecuteNonQuery();

                    Debug.WriteLineIf(res != 1, $"No row was Updated with the fixed Load Order! Mod was {currentMod.ModName}");
                }

                transcation.Commit();
            }
            catch (Exception e)
            {
                Debug.WriteLine("Exception: " + e.Message + "\r\nRolling Back!");
                transcation.Rollback();
            }
        }

        private bool SelectedHasMod(Mod mod, ObservableCollection<ModInPlayset> selected)
        {
            foreach(ModInPlayset mip in selected)
            {
                if(mip.TargetMod == mod)
                {
                    return true;
                }
            }
            return false;
        }

        public void UpdateModInPlaysetEnabled(ModInPlayset mod)
        {
            using (SqliteConnection connection = new SqliteConnection($"Data Source={connectionPath}"))
            {
                connection.Open();

                SqliteCommand command = connection.CreateCommand();
                command.CommandText = $"UPDATE playsets_mods SET enabled = @enabled WHERE playsetID = @playsetID AND modID = @modID;";
                command.Parameters.AddWithValue("@enabled", mod.Active);
                command.Parameters.AddWithValue("@playsetID", mod.TargetPlayset.UUID);
                command.Parameters.AddWithValue("@modID", mod.TargetMod.UUID);
                command.Prepare();

                int res = command.ExecuteNonQuery();
            }
        }

        public void UpdateModInPlaysetIndex(ModInPlayset mod)
        {
            using (SqliteConnection connection = new SqliteConnection($"Data Source={connectionPath}"))
            {
                connection.Open();

                SqliteCommand command = connection.CreateCommand();
                command.CommandText = $"UPDATE playsets_mods SET position = @newPos WHERE playsetID = @playsetID AND modID = @modID;";
                command.Parameters.AddWithValue("@newPos", mod.DatabaseIndex);
                command.Parameters.AddWithValue("@playsetID", mod.TargetPlayset.UUID);
                command.Parameters.AddWithValue("@modID", mod.TargetMod.UUID);
                command.Prepare();

                int res = command.ExecuteNonQuery();
            }
        }

        public void RemoveModFromPlayset(ModInPlayset mod)
        {
            using (SqliteConnection connection = new SqliteConnection($"Data Source={connectionPath}"))
            {
                connection.Open();

                SqliteCommand command = connection.CreateCommand();
                command.CommandText = $"DELETE FROM playsets_mods WHERE playsetID = @playsetID AND modID = @modID;";
                command.Parameters.AddWithValue("@playsetID", mod.TargetPlayset.UUID);
                command.Parameters.AddWithValue("@modID", mod.TargetMod.UUID);
                command.Prepare();

                int res = command.ExecuteNonQuery();
            }
        }
        public void AddModToPlayset(ModInPlayset mod)
        {
            using (SqliteConnection connection = new SqliteConnection($"Data Source={connectionPath}"))
            {
                connection.Open();

                SqliteCommand command = connection.CreateCommand();
                command.CommandText = $"INSERT INTO playsets_mods(playsetID, modID, position, enabled) VALUES(@playsetID, @modID, @position, @enabled);";
                command.Parameters.AddWithValue("@playsetID", mod.TargetPlayset.UUID);
                command.Parameters.AddWithValue("@modID", mod.TargetMod.UUID);
                command.Parameters.AddWithValue("@position", mod.DatabaseIndex);
                command.Parameters.AddWithValue("@enabled", mod.Active);
                command.Prepare();

                int res = command.ExecuteNonQuery();
            }
        }
    }
}
