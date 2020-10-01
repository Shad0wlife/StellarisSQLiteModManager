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
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
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

        #region Startup

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

        #endregion

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

        public void DeletePlayset(Playset playset)
        {
            using (SqliteConnection connection = new SqliteConnection($"Data Source={connectionPath}"))
            {
                connection.Open();

                SqliteTransaction transaction = connection.BeginTransaction();

                try
                {
                    SqliteCommand deleteChildrenCommand = connection.CreateCommand();
                    deleteChildrenCommand.CommandText = $"DELETE FROM playsets_mods WHERE playsetId = @playsetID;";
                    deleteChildrenCommand.Parameters.AddWithValue("@playsetID", playset.UUID);
                    deleteChildrenCommand.Prepare();

                    deleteChildrenCommand.ExecuteNonQuery();

                    SqliteCommand deleteParentCommand = connection.CreateCommand();
                    deleteParentCommand.CommandText = $"DELETE FROM playsets WHERE id = @playsetID;";
                    deleteParentCommand.Parameters.AddWithValue("@playsetID", playset.UUID);
                    deleteParentCommand.Prepare();

                    deleteParentCommand.ExecuteNonQuery();

                    transaction.Commit();
                }
                catch (Exception e)
                {
                    Debug.WriteLine("Exception: " + e.Message + "\r\nRolling Back!");
                    transaction.Rollback();
                }
            }
        }


        public bool ActivatePlayset(Playset playset)
        {
            using (SqliteConnection connection = new SqliteConnection($"Data Source={connectionPath}"))
            {
                connection.Open();

                SqliteTransaction transaction = connection.BeginTransaction();

                try
                {
                    SqliteCommand deactivateCurrentCommand = connection.CreateCommand();
                    deactivateCurrentCommand.CommandText = $"UPDATE playsets SET isActive = false WHERE isActive = true;";

                    deactivateCurrentCommand.ExecuteNonQuery();

                    SqliteCommand activateNewCommand = connection.CreateCommand();
                    activateNewCommand.CommandText = $"UPDATE playsets SET isActive = true WHERE id = @playsetID;";
                    activateNewCommand.Parameters.AddWithValue("@playsetID", playset.UUID);
                    activateNewCommand.Prepare();

                    activateNewCommand.ExecuteNonQuery();

                    transaction.Commit();
                    return true;
                }
                catch (Exception e)
                {
                    Debug.WriteLine("Exception: " + e.Message + "\r\nRolling Back!");
                    transaction.Rollback();
                    return false;
                }
            }
        }

        public void ClonePlaysetToNew(string newPlaysetName, Playset fromPlayset)
        {
            using (SqliteConnection connection = new SqliteConnection($"Data Source={connectionPath}"))
            {
                connection.Open();
                SqliteTransaction transaction = connection.BeginTransaction();

                Playset newPlayset = createNewPlaysetOnConnection(newPlaysetName, connection);

                try
                {
                    SqliteCommand fillCommand = connection.CreateCommand();
                    fillCommand.CommandText = $"INSERT INTO playsets_mods(playsetId, modId, position, enabled) SELECT '{newPlayset.UUID}', modId, position, enabled FROM playsets_mods WHERE playsetId = @oldUUID;";
                    fillCommand.Parameters.AddWithValue("@oldUUID", fromPlayset.UUID);
                    fillCommand.Prepare();

                    Debug.WriteLine("Command: " + fillCommand.CommandText + " with oldUUID = " + fromPlayset.UUID);

                    fillCommand.ExecuteNonQuery();

                    transaction.Commit();
                }catch(Exception e)
                {
                    Debug.WriteLine("Exception: " + e.Message + "\r\nRolling Back!");
                    transaction.Rollback();
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

            return new Playset(uuid, newPlaysetName, false);
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
                command.CommandText = $"SELECT id, name, isActive FROM playsets;";

                SqliteDataReader reader = command.ExecuteReader();
                while (reader.Read())
                {
                    string uuid = reader.GetString(0);
                    string name = reader.GetString(1);
                    bool isActive = reader.GetBoolean(2);
                    collection.Add(new Playset(uuid, name, isActive));
                }
            }

            Debug.WriteLine($"Now has {collection.Count} entries.");
        }

        public void ExportPlayset(Playset playset)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter =
            saveFileDialog.Filter = "Stellaris Playset Database|*.sqlite";
            saveFileDialog.DefaultExt = ".sqlite";
            saveFileDialog.FileName = playset.Name;

            bool? result = saveFileDialog.ShowDialog();
            string path;
            if (result == true)
            {
                path = saveFileDialog.FileName;
                Debug.WriteLine(path);
            }
            else
            {
                return;
            }

            using (FileStream stream = File.Create(path))
            {
            }

            using (SqliteConnection connection = new SqliteConnection($"Data Source={path}"))
            {
                connection.Open();
                InitExportDB(connection);
                CloneDataToExportDB(connection, playset);
            }
        }

        private void InitExportDB(SqliteConnection connection)
        {
            SqliteTransaction transaction = connection.BeginTransaction();

            try
            {
                SqliteCommand command = connection.CreateCommand();
                command.CommandText = $"CREATE TABLE playset_mods(position TEXT PRIMARY KEY, modname TEXT NOT NULL, steamID TEXT, pdxID TEXT, active BOOLEAN NOT NULL);";

                int res = command.ExecuteNonQuery();

                transaction.Commit();
            }
            catch (Exception e)
            {
                Debug.WriteLine("Exception: " + e.Message + "\r\nRolling Back!");
                transaction.Rollback();
            }
        }

        private void AttachLauncherDB(SqliteConnection connection)
        {
            SqliteCommand attachCommand = connection.CreateCommand();
            attachCommand.CommandText = $"ATTACH '{connectionPath}' AS LAUNCHERDATA;";

            int res = attachCommand.ExecuteNonQuery();
        }

        private void CloneDataToExportDB(SqliteConnection connection, Playset playset)
        {
            SqliteTransaction transaction = connection.BeginTransaction();

            try
            {
                AttachLauncherDB(connection);

                SqliteCommand copyCommand = connection.CreateCommand();
                copyCommand.CommandText = "INSERT INTO main.playset_mods(position, modname, steamID, pdxID, active) SELECT LAUNCHERDATA.playsets_mods.position, LAUNCHERDATA.mods.displayName, LAUNCHERDATA.mods.steamId, LAUNCHERDATA.mods.pdxId, LAUNCHERDATA.playsets_mods.enabled FROM LAUNCHERDATA.playsets_mods INNER JOIN LAUNCHERDATA.mods ON LAUNCHERDATA.playsets_mods.modId = LAUNCHERDATA.mods.id WHERE LAUNCHERDATA.playsets_mods.playsetId = @playsetID ORDER BY position ASC;";
                copyCommand.Parameters.AddWithValue("@playsetID", playset.UUID);
                copyCommand.Prepare();

                int res = copyCommand.ExecuteNonQuery();

                transaction.Commit();
            }
            catch (Exception e)
            {
                Debug.WriteLine("Exception: " + e.Message + "\r\nRolling Back!");
                transaction.Rollback();
            }
        }

        public void ImportPlayset()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Stellaris Playset Database|*.sqlite";
            openFileDialog.CheckFileExists = true;
            bool? result = openFileDialog.ShowDialog();
            string path;
            if (result == true)
            {
                path = openFileDialog.FileName;
            }
            else
            {
                return;
            }

            TextInputDialog dialog = new TextInputDialog(Properties.Resources.EnterName, Properties.Resources.EnterPlaysetNameQuestion, openFileDialog.SafeFileName.Replace(".sqlite", ""));
            string playsetName;
            if (dialog.ShowDialog() == true)
            {
                playsetName = dialog.ResultText;
            }
            else
            {
                return;
            }

            using (SqliteConnection connection = new SqliteConnection($"Data Source={path}"))
            {
                connection.Open();

                List<ImportMod> toImport = ToImport(connection);
                AttachLauncherDB(connection);
                List<ImportMod> missing = GetMissingMods(connection, toImport);
                if(missing == null)
                {
                    return;
                }
                if(missing.Count > 0)
                {
                    MissingModsMessageBox messageBox = new MissingModsMessageBox(Properties.Resources.MissingMods, missing);
                    if (messageBox.ShowDialog() == true)
                    {
                        return;
                    }
                    else
                    {
                        //Closed some other way. Eh whatever.
                        return;
                    }
                }

                Playset newPlayset = createNewPlaysetOnConnection(playsetName, connection);
                CloneDataFromImportDB(connection, newPlayset);
            }
        }

        private List<ImportMod> ToImport(SqliteConnection connection)
        {
            List<ImportMod> result = new List<ImportMod>();

            SqliteCommand command = connection.CreateCommand();
            command.CommandText = $"SELECT position, modname, steamId, pdxID, active FROM playset_mods;";

            SqliteDataReader reader = command.ExecuteReader();
            while (reader.Read())
            {
                string position = reader.GetString(0);
                string name = reader.GetString(1);
                string steamID = GetStringOrNull(reader, 2);
                string pdxID = GetStringOrNull(reader, 3);
                bool active = reader.GetBoolean(4);
                result.Add(new ImportMod(position, name, steamID, pdxID, active));
            }

            return result;
        }

        private List<ImportMod> GetMissingMods(SqliteConnection connection, List<ImportMod> required)
        {
            List<ImportMod> missing = new List<ImportMod>();

            foreach(ImportMod mod in required)
            {
                SqliteCommand command = connection.CreateCommand();
                if(mod.SteamID != null && mod.ParadoxID == null)
                {
                    command.CommandText = $"SELECT COUNT(*) FROM LAUNCHERDATA.mods WHERE LAUNCHERDATA.mods.steamID = @steamID;";
                    command.Parameters.AddWithValue("@steamID", mod.SteamID);
                }
                else if(mod.ParadoxID != null && mod.SteamID == null)
                {
                    command.CommandText = $"SELECT COUNT(*) FROM LAUNCHERDATA.mods WHERE LAUNCHERDATA.mods.pdxID = @pdxID;";
                    command.Parameters.AddWithValue("@pdxID", mod.ParadoxID);
                }
                else if(mod.SteamID != null && mod.ParadoxID != null)
                {
                    command.CommandText = $"SELECT COUNT(*) FROM LAUNCHERDATA.mods WHERE LAUNCHERDATA.mods.steamID = @steamID OR LAUNCHERDATA.mods.pdxID = @pdxID;";
                    command.Parameters.AddWithValue("@steamID", mod.SteamID);
                    command.Parameters.AddWithValue("@pdxID", mod.ParadoxID);
                }
                else if(mod.SteamID == null && mod.ParadoxID == null)
                {
                    command.CommandText = $"SELECT COUNT(*) FROM LAUNCHERDATA.mods WHERE LAUNCHERDATA.mods.steamID IS NULL AND LAUNCHERDATA.mods.pdxID IS NULL AND LAUNCHERDATA.mods.displayName = @displayName";
                    command.Parameters.AddWithValue("@displayName", mod.Modname);
                }
                command.Prepare();

                object result = command.ExecuteScalar();
                if (result is long n)
                {
                    if (n > 0)
                    {
                        continue;
                    }
                    else
                    {
                        missing.Add(mod);
                    }
                }
                else
                {
                    Debug.WriteLine("Scalar was not long!");
                    return null;
                }
            }

            return missing;
        }

        private void CloneDataFromImportDB(SqliteConnection connection, Playset playset)
        {
            SqliteTransaction transaction = connection.BeginTransaction();
            string insertBase = "INSERT INTO LAUNCHERDATA.playsets_mods(playsetID, modID, position, enabled) SELECT @playsetID, LAUNCHERDATA.mods.id, main.playset_mods.position, main.playset_mods.active FROM main.playset_mods INNER JOIN LAUNCHERDATA.mods";

            try
            {
                SqliteCommand steamCopyCommand = connection.CreateCommand();
                steamCopyCommand.CommandText = insertBase + " ON main.playset_mods.steamID = LAUNCHERDATA.mods.steamId WHERE main.playset_mods.steamID IS NOT NULL AND main.playset_mods.pdxID IS NULL;";
                steamCopyCommand.Parameters.AddWithValue("@playsetID", playset.UUID);
                steamCopyCommand.Prepare();

                int res = steamCopyCommand.ExecuteNonQuery();
                Debug.WriteLine("Inserted " + res + " steam only rows.");

                SqliteCommand pdxCopyCommand = connection.CreateCommand();
                pdxCopyCommand.CommandText = insertBase + " ON main.playset_mods.pdxID = LAUNCHERDATA.mods.pdxId WHERE main.playset_mods.pdxID IS NOT NULL AND main.playset_mods.steamID IS NULL;";
                pdxCopyCommand.Parameters.AddWithValue("@playsetID", playset.UUID);
                pdxCopyCommand.Prepare();

                res = pdxCopyCommand.ExecuteNonQuery();
                Debug.WriteLine("Inserted " + res + " paradox only rows.");

                SqliteCommand bothCopyCommand = connection.CreateCommand();
                bothCopyCommand.CommandText = insertBase + " ON main.playset_mods.steamID = LAUNCHERDATA.mods.steamId WHERE main.playset_mods.pdxID IS NOT NULL AND main.playset_mods.steamID IS NOT NULL AND main.playset_mods.pdxID = LAUNCHERDATA.mods.pdxId;";
                bothCopyCommand.Parameters.AddWithValue("@playsetID", playset.UUID);
                bothCopyCommand.Prepare();

                res = bothCopyCommand.ExecuteNonQuery();
                Debug.WriteLine("Inserted " + res + " combined rows.");

                SqliteCommand localCopyCommand = connection.CreateCommand();
                localCopyCommand.CommandText = insertBase + " ON main.playset_mods.modname = LAUNCHERDATA.mods.displayName WHERE main.playset_mods.pdxID IS NULL AND main.playset_mods.steamID IS NULL;";
                localCopyCommand.Parameters.AddWithValue("@playsetID", playset.UUID);
                localCopyCommand.Prepare();

                res = localCopyCommand.ExecuteNonQuery();
                Debug.WriteLine("Inserted " + res + " local only rows.");

                transaction.Commit();
            }
            catch (Exception e)
            {
                Debug.WriteLine("Exception: " + e.Message + "\r\nRolling Back!");
                transaction.Rollback();
            }
        }

        #region Mod Management

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
            SqliteTransaction transaction = connection.BeginTransaction();

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

                transaction.Commit();
            }
            catch (Exception e)
            {
                Debug.WriteLine("Exception: " + e.Message + "\r\nRolling Back!");
                transaction.Rollback();
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

        #endregion
    }
}
