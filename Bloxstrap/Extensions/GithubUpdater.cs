using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Claudestrap;

public static class GithubUpdater
{
    private static readonly HttpClient http = new()
    {
        DefaultRequestHeaders = { { "User-Agent", "Claudestrap-Updater" } }
    };

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
        await File.WriteAllBytesAsync(exePath, bytes);

        string currentExe = Environment.ProcessPath!;
        string backupExe = currentExe + ".old";
        if (File.Exists(backupExe)) File.Delete(backupExe);
        File.Move(currentExe, backupExe);
        File.Copy(exePath, currentExe, true);

        RestartAfterUpdate(currentExe);
        return true;
    }

    private static void RestartAfterUpdate(string exePath)
    {
        Task.Delay(800).ContinueWith(_ =>
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true
            });
            Environment.Exit(0);
        });
    }
}
