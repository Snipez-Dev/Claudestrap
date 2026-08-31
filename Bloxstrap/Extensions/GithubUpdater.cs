using System;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using Claudestrap;
using Claudestrap.UI;
using Claudestrap.UI.Elements.Dialogs;

/// <summary>What an update check ended up doing, so callers can report it without
/// re-implementing the check themselves.</summary>
public enum UpdateCheckResult
{
    /// <summary>Already checked this session and not forced.</summary>
    AlreadyChecked,
    /// <summary>version.txt couldn't be read.</summary>
    Unreachable,
    UpToDate,
    /// <summary>The user chose to stay on the current version.</summary>
    Declined,
    /// <summary>Downloaded and installed; the process is about to restart.</summary>
    Applied,
    Failed
}

public static class GithubUpdater
{
    private static readonly HttpClient http = new()
    {
        // HttpClient's 100 second default applies to the whole operation, download
        // included -- and the release binary is over 200 MB, which takes longer than
        // that on anything below ~20 Mbit. Updating was impossible on a slow line, so
        // the download is unbounded and the checks below carry their own deadlines.
        Timeout = Timeout.InfiniteTimeSpan,
        DefaultRequestHeaders = { { "User-Agent", "Claudestrap-Updater" } }
    };

    /// <summary>How long to wait for the version probe before carrying on without it.
    /// It's a seven byte file; if it hasn't answered by now the network is in no state
    /// to download 200 MB either, and the app should not sit there unopened.</summary>
    private static readonly TimeSpan VersionProbeTimeout = TimeSpan.FromSeconds(8);

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
    /// <param name="quiet">Apply without asking and without any window -- silent launches.</param>
    /// <param name="force">Run even if this session already checked. The automatic check
    /// happens once per session; the button in settings is an explicit request, so it
    /// bypasses that instead of silently doing nothing.</param>
    public static async Task<UpdateCheckResult> CheckForUpdateAsync(bool quiet, bool force = false)
    {
        if (_checkedThisSession && !force)
            return UpdateCheckResult.AlreadyChecked;

        _checkedThisSession = true;

        try
        {
            string? latestTag = await GetLatestVersionTagAsync();

            if (string.IsNullOrWhiteSpace(latestTag))
                return UpdateCheckResult.Unreachable;

            string currentVersion = Assembly.GetExecutingAssembly().GetName().Version!.ToString();
            string remote = latestTag.TrimStart('v', 'V');

            bool isNewer = Version.TryParse(remote, out var rv) && Version.TryParse(currentVersion, out var cv)
                ? rv > cv
                : string.Compare(remote, currentVersion, StringComparison.OrdinalIgnoreCase) > 0;

            if (!isNewer)
            {
                App.Logger.WriteLine("GitHubUpdater", "No newer release found.");
                return UpdateCheckResult.UpToDate;
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
                return UpdateCheckResult.Declined;
            }

            // Quiet launches stay silent; every other path gets a window, because the
            // download is a couple of hundred megabytes and an app that appears to hang
            // for a minute is indistinguishable from one that crashed.
            UpdateProgressDialog? progress = quiet ? null : ShowProgressDialog(latestTag);

            bool applied;

            try
            {
                applied = await DownloadAndInstallUpdate(latestTag, progress);
            }
            finally
            {
                // On success the process exits inside UpdateExe and never gets here; this
                // is the failure path, where the window has to come down before the error
                // message box goes up.
                progress?.CloseDialog();
            }

            App.Logger.WriteLine("GitHubUpdater", applied ? "Update applied, restarting." : "Update failed.");

            if (!applied)
            {
                Frontend.ShowMessageBox(
                    "Downloading or installing the update failed. Claudestrap will keep running on the current version -- check the log for details, or update manually from the GitHub releases page.",
                    MessageBoxImage.Warning);
            }

            return applied ? UpdateCheckResult.Applied : UpdateCheckResult.Failed;
        }
        catch (Exception ex)
        {
            App.Logger.WriteLine("GitHubUpdater", $"Update check failed: {ex.Message}");
            return UpdateCheckResult.Failed;
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
            using var timeout = new CancellationTokenSource(VersionProbeTimeout);
            string response = await http.GetStringAsync(VersionFileUrl, timeout.Token);
            string version = response.Trim();
            return string.IsNullOrWhiteSpace(version) ? null : version;
        }
        catch (Exception ex)
        {
            App.Logger.WriteLine("GitHubUpdater", $"Failed to read version.txt: {ex}");
            return null;
        }
    }

    private static UpdateProgressDialog? ShowProgressDialog(string tag)
    {
        var app = Application.Current;

        if (app is null)
            return null;

        try
        {
            return app.Dispatcher.Invoke(() =>
            {
                var dialog = new UpdateProgressDialog(tag);
                dialog.Show();
                return dialog;
            });
        }
        catch (Exception ex)
        {
            // A missing progress window is no reason to skip the update itself.
            App.Logger.WriteLine("GitHubUpdater", $"Couldn't open the progress window: {ex.Message}");
            return null;
        }
    }

    public static async Task<bool> DownloadAndInstallUpdate(string tag, UpdateProgressDialog? progress = null)
    {
        try
        {
            App.Logger.WriteLine("GitHubUpdater", $"Downloading update {tag}...");
            return await UpdateExe(LatestExeDownloadUrl, "Claudestrap.exe", progress);
        }
        catch (Exception ex)
        {
            App.Logger.WriteLine("GitHubUpdater", $"Update failed: {ex}");
            return false;
        }
    }

    private static async Task<bool> UpdateExe(string url, string name, UpdateProgressDialog? progress)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "Claudestrap_Update");
        Directory.CreateDirectory(tempDir);

        string exePath = Path.Combine(tempDir, name);

        progress?.SetStatus("Connecting to GitHub...", indeterminate: true);

        long received = await DownloadWithProgressAsync(url, exePath, progress);

        if (received == 0)
        {
            App.Logger.WriteLine("GitHubUpdater", "Downloaded update was empty, aborting.");
            return false;
        }

        progress?.SetStatus("Installing update...", indeterminate: true);

        string currentExe = Environment.ProcessPath!;
        string backupExe = currentExe + ".old";
        if (File.Exists(backupExe)) File.Delete(backupExe);
        File.Move(currentExe, backupExe);

        try
        {
            File.Copy(exePath, currentExe, true);

            progress?.SetStatus("Restarting Claudestrap...", indeterminate: true);

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

    /// <summary>
    /// Streams the download to disk instead of buffering the whole file in memory,
    /// reporting progress as it goes. Returns the number of bytes written.
    /// </summary>
    private static async Task<long> DownloadWithProgressAsync(string url, string destination, UpdateProgressDialog? progress)
    {
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        long? total = response.Content.Headers.ContentLength;

        progress?.SetStatus("Downloading update...");
        progress?.SetProgress(0, total);

        using var source = await response.Content.ReadAsStreamAsync();
        using var target = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);

        byte[] buffer = new byte[81920];
        long received = 0;
        var lastReport = DateTime.UtcNow;
        int read;

        while ((read = await source.ReadAsync(buffer)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read));
            received += read;

            // ~2,600 chunks for a 200 MB download -- reporting every one of them would
            // spend more time on the dispatcher than on the transfer.
            if (progress is not null && (DateTime.UtcNow - lastReport).TotalMilliseconds >= 100)
            {
                progress.SetProgress(received, total);
                lastReport = DateTime.UtcNow;
            }
        }

        progress?.SetProgress(received, total);

        return received;
    }
}
