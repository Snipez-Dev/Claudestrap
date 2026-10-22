using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows;

namespace Claudestrap
{
    public class JsonManager<T> where T : class, new()
    {
        public T OriginalProp { get; set; } = new();
        public T Prop { get; set; } = new();
        public virtual string ClassName => typeof(T).Name;
        public string? LastFileHash { get; private set; }
        public bool LastSaveSucceeded { get; private set; }
        private bool _loadFailed;
        public virtual string BackupsLocation => Path.Combine(Paths.Base, "Backup.json");
        public virtual string FileLocation => Path.Combine(Paths.Base, $"{ClassName}.json");
        public virtual string LOG_IDENT_CLASS => $"JsonManager<{ClassName}>";

        public virtual void Load(bool alertFailure = true)
        {
            string LOG_IDENT = $"{LOG_IDENT_CLASS}::Load";
            App.Logger.WriteLine(LOG_IDENT, $"Loading from {FileLocation}...");

            try
            {
                if (!File.Exists(FileLocation) && !File.Exists(FileLocation + ".bak"))
                {
                    App.Logger.WriteLine(LOG_IDENT, "File does not exist, saving defaults.");
                    _loadFailed = false;
                    LastFileHash = null;
                    Save();
                    return;
                }

                var snapshot = AtomicJsonFile.Read<T>(FileLocation);
                Prop = snapshot.Value;
                LastFileHash = snapshot.Hash;
                _loadFailed = false;
                if (snapshot.Recovered)
                    App.Logger.WriteLine(LOG_IDENT, "Loaded the last valid backup. The original file is preserved until a successful save.");
                App.Logger.WriteLine(LOG_IDENT, "Loaded successfully!");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, "Failed to load!");
                App.Logger.WriteException(LOG_IDENT, ex);

                _loadFailed = true;
                if (alertFailure)
                    Frontend.ShowMessageBox($"Failed to load settings. The original file has been preserved; saving is disabled until it can be reloaded.\n\n{ex.Message}", MessageBoxImage.Warning);
            }
        }

        public virtual void Save()
        {
            LastSaveSucceeded = false;
            string LOG_IDENT = $"{LOG_IDENT_CLASS}::Save";
            App.Logger.WriteLine(LOG_IDENT, $"Saving to {FileLocation}...");

            if (_loadFailed)
            {
                App.Logger.WriteLine(LOG_IDENT, "Save blocked because the existing file could not be loaded.");
                Frontend.ShowMessageBox($"Cannot save {ClassName}: its existing file could not be loaded. Reload it first. The original file is unchanged.", MessageBoxImage.Warning);
                return;
            }

            const int maxRetries = 5;
            const int delayMs = 100;
            int attempts = 0;

            while (true)
            {
                try
                {
                    LastFileHash = AtomicJsonFile.Write(FileLocation, Prop, LastFileHash);
                    LastSaveSucceeded = true;
                    App.Logger.WriteLine(LOG_IDENT, "Save complete!");
                    break;
                }
                catch (IOException ex) when ((ex.HResult & 0xFFFF) == 32 && attempts < maxRetries)
                {
                    attempts++;
                    Thread.Sleep(delayMs);
                }
                catch (UnauthorizedAccessException ex) when (attempts < maxRetries)
                {
                    attempts++;
                    Thread.Sleep(delayMs);
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, "Failed to save");
                    App.Logger.WriteException(LOG_IDENT, ex);

                    string errorMessage = string.Format(Resources.Strings.Bootstrapper_JsonManagerSaveFailed, ClassName, ex.Message);
                    Frontend.ShowMessageBox(errorMessage, MessageBoxImage.Warning);
                    break;
                }
            }
        }

        private static string? SafeGetFileHash(string path)
        {
            try
            {
                return AtomicJsonFile.Hash(path);
            }
            catch
            {
                return null;
            }
        }

        public bool HasFileOnDiskChanged()
        {
            try
            {
                string? currentHash = SafeGetFileHash(FileLocation);
                return LastFileHash != currentHash;
            }
            catch
            {
                return true;
            }
        }

        public void SaveBackup(string name)
        {
            const string LOGGER_STRING = "SaveBackup::Backups";
            string baseDir = Paths.SavedBackups;

            try
            {
                if (string.IsNullOrWhiteSpace(name))
                    return;

                Directory.CreateDirectory(baseDir);

                string filePath = Path.Combine(baseDir, name);
                AtomicJsonFile.Write(filePath, Prop, expectedHash: null, checkForChanges: false);

                App.Logger.WriteLine(LOGGER_STRING, $"Backup '{name}' saved successfully.");
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox($"Failed to save backup:\n{ex.Message}", MessageBoxImage.Error);
            }
        }

        public void LoadBackup(string? name, bool? clearFlags)
        {
            const string LOGGER_STRING = "LoadBackup::Backups";
            string baseDir = Paths.SavedBackups;

            try
            {
                if (string.IsNullOrWhiteSpace(name))
                    return;

                string filePath = Path.Combine(baseDir, name);

                if (!File.Exists(filePath))
                    throw new FileNotFoundException($"Backup file '{name}' not found.");

                string json;
                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream))
                    json = reader.ReadToEnd();

                T? settings = JsonSerializer.Deserialize<T>(json);
                if (settings is null)
                    throw new InvalidOperationException("Deserialization returned null.");

                if (clearFlags == true)
                {
                    Prop = settings;
                }
                else if (settings is IDictionary<string, object> settingsDict && Prop is IDictionary<string, object> propDict)
                {
                    foreach (var kvp in settingsDict)
                    {
                        if (kvp.Value != null)
                            propDict[kvp.Key] = kvp.Value;
                    }
                }

                App.Logger.WriteLine(LOGGER_STRING, $"Backup '{name}' loaded successfully.");
                App.FastFlags.Save();
            }
            catch (Exception ex)
            {
                App.Logger.WriteException(LOGGER_STRING, ex);
                Frontend.ShowMessageBox($"Failed to load backup:\n{ex.Message}", MessageBoxImage.Error);
            }
        }
    }
}
