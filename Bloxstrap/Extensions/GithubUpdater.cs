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
    Failed,
    /// <summary>A newer release exists, but this is a multi-file build (Visual Studio /
    /// dotnet build output) that the single-file release can't replace in place.</summary>
    NotUpdatable
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
            if (PackagedUpdates.IsManaged) return await PackagedUpdates.CheckAsync(quiet);
            string? latestTag = await GetLatestVersionTagAsync();

            if (string.IsNullOrWhiteSpace(latestTag))
                return UpdateCheckResult.Unreachable;

            string currentVersion = App.Version;
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

            if (!UpdateBuildPolicy.CanReplace(CurrentUpdateChannel, IsMultiFileBuild()))
            {
                App.Logger.WriteLine("GitHubUpdater", "This build is protected from upstream replacement: " + UpdateUnavailableReason);
                return UpdateCheckResult.NotUpdatable;
            }

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

    /// <summary>
    /// True when this process runs from a normal build folder -- a small apphost next to
    /// Claudestrap.dll, Wpf.Ui.dll and a copy of the runtime -- rather than from the
    /// single-file release. Swapping just the exe there leaves the release's bundled
    /// runtime and precompiled code next to the folder's older loose assemblies, and the
    /// runtime loads those loose ones instead. On 9.0.17 that ran with mismatched code
    /// and crashed at random with access violations; on 9.0.21 it refuses to start at
    /// all ("MVID mismatch ... Claudestrap.r2r.dll").
    /// </summary>
    private static bool IsMultiFileBuild()
    {
        string? dir = Path.GetDirectoryName(Environment.ProcessPath);
        return dir is not null && File.Exists(Path.Combine(dir, "Claudestrap.dll"));
    }

    // Plain-text file at the repo root holding just the latest version number --
    // simpler than parsing the Releases API, and the release workflow keeps it in
    // sync automatically on every tag push.
    private static string VersionFileUrl => $"https://raw.githubusercontent.com/{App.ProjectRepository}/main/version.txt";

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
            LastError = ex.Message;
            App.Logger.WriteException("GitHubUpdater", ex);
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

    public static string CurrentUpdateChannel => Assembly.GetExecutingAssembly()
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(attribute => attribute.Key == "ClaudestrapUpdateChannel")?.Value ?? "local";

    public static string UpdateUnavailableReason => PackagedUpdates.IsManaged
        ? "This packaged build uses its own package feed. Configure that feed under Updates."
        : IsMultiFileBuild()
        ? "This multi-file build must be rebuilt from its source."
        : UpdateBuildPolicy.CanReplace(CurrentUpdateChannel, false)
            ? "Verified upstream updates are enabled."
            : "This local build contains custom changes. Installing a standard release would remove them; update it from the local source instead.";

    public static string? LastError { get; private set; }

    public static async Task<bool> DownloadAndInstallUpdate(string tag, UpdateProgressDialog? progress = null)
    {
        try
        {
            if (!UpdateBuildPolicy.CanReplace(CurrentUpdateChannel, IsMultiFileBuild()))
                throw new InvalidOperationException(UpdateUnavailableReason);

            string currentExe = Environment.ProcessPath!;
            using var updateLock = new FileStream(currentExe + ".update.lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            progress?.SetStatus("Checking release metadata...", indeterminate: true);
            string releaseTag = "v" + tag.TrimStart('v', 'V');
            using var metadataTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            string metadata = await http.GetStringAsync(
                $"https://api.github.com/repos/{App.ProjectRepository}/releases/tags/{Uri.EscapeDataString(releaseTag)}", metadataTimeout.Token);
            using var release = JsonDocument.Parse(metadata);
            if (release.RootElement.GetProperty("tag_name").GetString() != releaseTag)
                throw new InvalidDataException("The release tag does not match the requested update.");
            var asset = release.RootElement.GetProperty("assets").EnumerateArray()
                .First(item => item.GetProperty("name").GetString() == "Claudestrap.exe");
            long size = asset.GetProperty("size").GetInt64();
            string digest = asset.GetProperty("digest").GetString() ?? "";
            if (!digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("This release does not provide a SHA-256 digest.");
            var url = new Uri(asset.GetProperty("browser_download_url").GetString()!);
            if (url.Scheme != Uri.UriSchemeHttps || url.Host != "github.com")
                throw new InvalidDataException("Unexpected release download location.");

            string downloaded = await PersistentUpdateDownload.DownloadAsync(http, url, Path.Combine(Paths.Cache, "Updates"), size, digest[7..],
                (received, total) => progress?.SetProgress(received, total),
                status => progress?.SetStatus(status, indeterminate: true));
            progress?.SetStatus("Installing and checking startup...", indeterminate: true);
            await ExecutableUpdate.ApplyAsync(downloaded, currentExe,
                path => UpdateHandoff.StartAndConfirmAsync(path, RestartArguments.Clean(App.LaunchSettings.Args)));
            try { Directory.Delete(Path.GetDirectoryName(downloaded)!, recursive: true); }
            catch (IOException ex) { App.Logger.WriteLine("GitHubUpdater", "Update applied; cache cleanup deferred: " + ex.Message); }
            catch (UnauthorizedAccessException ex) { App.Logger.WriteLine("GitHubUpdater", "Update applied; cache cleanup deferred: " + ex.Message); }
            App.Logger.WriteLine("GitHubUpdater", "New launcher confirmed startup; completing update.");
            LastError = null;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            App.Logger.WriteException("GitHubUpdater", ex);
            return false;
        }
        Environment.Exit(0);
        return true;
    }
}
