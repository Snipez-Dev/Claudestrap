using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using Claudestrap;
using Claudestrap.UI;

public static class GithubUpdater
{
    private static readonly HttpClient http = new()
    {
        DefaultRequestHeaders = { { "User-Agent", "Claudestrap-Updater" } }
    };

    private static bool _checkedThisSession;

    /// <summary>
    /// Checks version.txt against this build's own version and, if newer, either
    /// applies the update immediately (quiet launches) or asks the user whether to
    /// upgrade now or stay on the current version. Safe to call from more than one
    /// launch path in the same process -- only actually performs the check once per
    /// session, so opening the launch menu and then pressing Play doesn't re-prompt.
    /// Returns true if an update was applied -- the caller should stop what it was
    /// doing, since the process is about to exit and restart into the new build.
    /// </summary>
    public static async Task<bool> CheckForUpdateAsync(bool quiet)
    {
        if (_checkedThisSession)
            return false;

        _checkedThisSession = true;

        try
        {
            string? latestTag = await GetLatestVersionTagAsync();

            if (string.IsNullOrWhiteSpace(latestTag))
                return false;

            string currentVersion = Assembly.GetExecutingAssembly().GetName().Version!.ToString();
            string remote = latestTag.TrimStart('v', 'V');

            bool isNewer = Version.TryParse(remote, out var rv) && Version.TryParse(currentVersion, out var cv)
                ? rv > cv
                : string.Compare(remote, currentVersion, StringComparison.OrdinalIgnoreCase) > 0;

            if (!isNewer)
            {
                App.Logger.WriteLine("GitHubUpdater", "No newer release found.");
                return false;
            }

            App.Logger.WriteLine("GitHubUpdater", $"Newer release found: {latestTag}");

            bool proceed = quiet;

            if (!proceed)
            {
                var result = Frontend.ShowMessageBox(
                    $"A new version of Claudestrap is available ({latestTag}), and you're currently on {currentVersion}.\n\n" +
                    "Would you like to upgrade now, or stay on your current version?",
                    MessageBoxImage.Information,
                    MessageBoxButton.YesNo,
                    MessageBoxResult.Yes);

                proceed = result == MessageBoxResult.Yes;
            }

            if (!proceed)
            {
                App.Logger.WriteLine("GitHubUpdater", "Update declined.");
                return false;
            }

            bool applied = await DownloadAndInstallUpdate(latestTag);
            App.Logger.WriteLine("GitHubUpdater", applied ? "Update applied, restarting." : "Update failed.");

            if (!applied)
            {
                Frontend.ShowMessageBox(
                    "Downloading or installing the update failed. Claudestrap will keep running on the current version -- check the log for details, or update manually from the GitHub releases page.",
                    MessageBoxImage.Warning);
            }

            return applied;
        }
        catch (Exception ex)
        {
            App.Logger.WriteLine("GitHubUpdater", $"Update check failed: {ex.Message}");
            return false;
        }
    }

    // Plain-text file at the repo root holding just the latest version number --
    // simpler than parsing the Releases API, and the release workflow keeps it in
    // sync automatically on every tag push.
    private static string VersionFileUrl => $"https://raw.githubusercontent.com/{App.ProjectRepository}/main/version.txt";

    // GitHub's well-known "latest release" download alias -- always resolves to
    // whatever the newest release's same-named asset is, no API call needed.
    private static string LatestExeDownloadUrl => $"https://github.com/{App.ProjectRepository}/releases/latest/download/Claudestrap.exe";

    public static async Task<string?> GetLatestVersionTagAsync()
    {
        try
        {
            string response = await http.GetStringAsync(VersionFileUrl);
            string version = response.Trim();
            return string.IsNullOrWhiteSpace(version) ? null : version;
        }
        catch (Exception ex)
        {
            App.Logger.WriteLine("GitHubUpdater", $"Failed to read version.txt: {ex}");
            return null;
        }
    }

    public static async Task<bool> DownloadAndInstallUpdate(string tag)
    {
        try
        {
            App.Logger.WriteLine("GitHubUpdater", $"Downloading update {tag}...");
            return await UpdateExe(LatestExeDownloadUrl, "Claudestrap.exe");
        }
        catch (Exception ex)
        {
            App.Logger.WriteLine("GitHubUpdater", $"Update failed: {ex}");
            return false;
        }
    }

    private static async Task<bool> UpdateExe(string url, string name)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "Claudestrap_Update");
        Directory.CreateDirectory(tempDir);

        string exePath = Path.Combine(tempDir, name);
        var bytes = await http.GetByteArrayAsync(url);

        if (bytes.Length == 0)
        {
            App.Logger.WriteLine("GitHubUpdater", "Downloaded update was empty, aborting.");
            return false;
        }

        await File.WriteAllBytesAsync(exePath, bytes);

        string currentExe = Environment.ProcessPath!;
        string backupExe = currentExe + ".old";
        if (File.Exists(backupExe)) File.Delete(backupExe);
        File.Move(currentExe, backupExe);

        try
        {
            File.Copy(exePath, currentExe, true);

            Process.Start(new ProcessStartInfo
            {
                FileName = currentExe,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            // Couldn't finish the swap or launch the new build -- restore the exe this
            // process is actually running from so a manual restart still works, instead
            // of silently leaving the old build running with nothing telling the user
            // the update never actually applied.
            App.Logger.WriteLine("GitHubUpdater", $"Swap/restart failed, rolling back: {ex.Message}");

            try
            {
                if (File.Exists(currentExe)) File.Delete(currentExe);
                File.Move(backupExe, currentExe);
            }
            catch (Exception rollbackEx)
            {
                App.Logger.WriteLine("GitHubUpdater", $"Rollback failed: {rollbackEx.Message}");
            }

            return false;
        }

        // The new process is up and reading from currentExe; this one's file handle
        // (backupExe) is no longer needed, so exit immediately rather than racing it.
        Environment.Exit(0);
        return true;
    }
}
