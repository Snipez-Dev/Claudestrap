using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Shapes;

namespace Claudestrap.Integrations
{
    public class Cleaner
    {
        public static Dictionary<string, string?> Directories = new Dictionary<string, string?> {
            { "ClaudestrapLogs", Paths.Logs },
            { "ClaudestrapCache", Paths.Downloads },
            { "RobloxLogs", Paths.RobloxLogs },
            { "RobloxCache", Paths.RobloxCache }
        };

        public static void DoCleaning()
        {
            const string LOG_IDENT = "Cleaner::DoCleaning";

            App.Logger.WriteLine(LOG_IDENT, "Cleaner has started");

            var MaxFileAge = App.Settings.Prop.CleanerOptions switch
            {
                CleanerOptions.OneDay => 1,
                CleanerOptions.OneWeek => 7,
                CleanerOptions.OneMonth => 30,
                CleanerOptions.TwoMonths => 60,
                CleanerOptions.Never => int.MaxValue,
                _ => int.MaxValue,
            };

            var Threshold = DateTime.Now.AddHours(-MaxFileAge);

            foreach (var directory in Directories)
            {
                string? Folder = directory.Value;
                string Type = directory.Key;

                if (!App.Settings.Prop.CleanerDirectories.Contains(Type))
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Skipping {Type}");
                    continue;
                }

                if (String.IsNullOrEmpty(Folder) || !Directory.Exists(Folder))
                    continue;

                try
                {
                    string[] Files = RecursivlyGetFiles(Folder);

                    App.Logger.WriteLine(LOG_IDENT, $"Running cleaner in {directory}, {Files.Length} files found");

                    foreach (string file in Files)
                    {
                        // verify file
                        if (!VerifyFile(file, Threshold))
                            continue;

                        // attempt deletion
                        try { File.Delete(file); }
                        catch (Exception ex)
                        {
                            App.Logger.WriteLine(LOG_IDENT, $"Unable to delete {file}");
                            App.Logger.WriteException(LOG_IDENT, ex);
                            continue;
                        }
                    }
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to clean up {Folder}");
                    App.Logger.WriteException(LOG_IDENT, ex);
                }
            }

            App.Logger.WriteLine(LOG_IDENT, "Cleaner finished");
        }

        /// <summary>
        /// Player data folders under %LOCALAPPDATA%\Roblox. LocalStorage is the one that
        /// matters most: RobloxCookies.dat is the client's own saved session, and the
        /// client sends it along when it redeems an account launch's auth ticket -- a
        /// stale session there made every account launch fail with "User is moderated"
        /// (403) until it was removed.
        /// </summary>
        private static readonly string[] RobloxDataDirectories =
        {
            "LocalStorage",
            "logs",
            "http",
            "Temp",
            "Downloads",
            "UniversalApp",
            "notifications",
            "rbx-storage",
            "rbx-storage-sc",
            "tmp-capture-storage",
            "Benchmark"
        };

        private static readonly string[] RobloxDataFiles =
        {
            "rbx-storage.db",
            "rbx-storage.db-shm",
            "rbx-storage.db-wal",
            "rbx-storage.id",
            "AnalysticsSettings.xml"
        };

        /// <summary>
        /// Wipes all of Roblox's local player data -- saved session, local storage, logs,
        /// http cache, downloads and telemetry ids -- regardless of the configured cleaner
        /// age threshold. Leaves the installed client (Versions), Studio and its plugins,
        /// and the user's graphics settings alone. Closes every Roblox player first, since
        /// a running one (the windowless tray resident included) locks these files and
        /// writes its session straight back. Returns the file count. <paramref name="progress"/>
        /// gets the fraction of files handled so far, 0 to 1.
        /// </summary>
        public static int CleanRobloxData(IProgress<double>? progress = null)
        {
            const string LOG_IDENT = "Cleaner::CleanRobloxData";
            App.Logger.WriteLine(LOG_IDENT, "Cleaning Roblox local data");

            AccountLauncher.CloseRunningPlayer();
            AccountLauncher.CloseBackgroundPlayers();

            string robloxData = System.IO.Path.Combine(Paths.LocalAppData, "Roblox");

            // Collected up front so the progress bar has a total to count against.
            var files = new List<string>();

            foreach (string name in RobloxDataDirectories)
            {
                string dir = System.IO.Path.Combine(robloxData, name);

                if (!Directory.Exists(dir))
                    continue;

                try
                {
                    files.AddRange(RecursivlyGetFiles(dir));
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to list {dir}");
                    App.Logger.WriteException(LOG_IDENT, ex);
                }
            }

            foreach (string name in RobloxDataFiles)
            {
                string file = System.IO.Path.Combine(robloxData, name);

                if (File.Exists(file))
                    files.Add(file);
            }

            int cleaned = 0;
            int lastPercent = -1;

            for (int i = 0; i < files.Count; i++)
            {
                if (TryDeleteFile(files[i], LOG_IDENT))
                    cleaned++;

                // Only when the whole percent changes -- a cache can hold thousands of
                // files, and one UI update per file would cost more than the deletes.
                int percent = (i + 1) * 100 / files.Count;
                if (percent != lastPercent)
                {
                    lastPercent = percent;
                    progress?.Report(percent / 100.0);
                }
            }

            progress?.Report(1.0);

            App.Logger.WriteLine(LOG_IDENT, $"Cleaned {cleaned} files");
            return cleaned;
        }

        private static bool TryDeleteFile(string file, string logIdent)
        {
            try
            {
                File.Delete(file);
                return true;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(logIdent, $"Unable to delete {file}");
                App.Logger.WriteException(logIdent, ex);
                return false;
            }
        }

        private static bool VerifyFile(string file, DateTime Threshold)
        {
            // true = can be deleted
            // false = silently cancel deletion for current file
            // exception = deletion could be dangerous, cancels cleaner for current directory

            if (!File.Exists(file))
                return false;

            if (File.GetCreationTime(file) > Threshold)
                return false;

            // TODO add more safety checks?
            if (!file.Contains("Roblox") && !file.Contains(App.ProjectName) && !file.Contains(Paths.Base))
                throw new Exception($"{file} was in disallowed directory");

            if (file.Contains("Windows"))
                throw new Exception($"{file} was in Windows directory"); // we dont want any contact with windows directory
                                                                         // this will cancel the cleaner process
            return true;
        }

        private static string[] RecursivlyGetFiles(string Folder)
        {
            List<string> filesList = new List<string>();

            if (String.IsNullOrEmpty(Folder) || !Directory.Exists(Folder))
                throw new Exception("Folder was not found");

            foreach (string File in Directory.EnumerateFiles(Folder, "*.*", SearchOption.AllDirectories))
            {
                filesList.Add(File);
            }

            return filesList.ToArray();
        }

    }
}