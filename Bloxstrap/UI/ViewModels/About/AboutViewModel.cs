using System.Windows;

namespace Claudestrap.UI.ViewModels.About
{
    public class AboutViewModel : NotifyPropertyChangedViewModel
    {
        public string Version => string.Format(Strings.Menu_About_Version, App.Version);
        public string Diagnostics => string.Join(Environment.NewLine, new[]
        {
            $"Build: {System.Reflection.Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false).OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion}",
            $"Update channel: {GithubUpdater.CurrentUpdateChannel}",
            $"Runtime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}",
            $"Packaging: {(PackagedUpdates.IsManaged ? "Velopack" : "Portable single-file")}",
            $"Managed startup: settings {LauncherStartup.SettingsReadyMilliseconds} ms; first window {LauncherStartup.FirstWindowMilliseconds} ms",
            "Memory: automatic GC; no periodic forced collection",
            $"Update policy: {GithubUpdater.UpdateUnavailableReason}",
            $"Executable: {Environment.ProcessPath}",
            $"Settings directory: {Paths.Base}",
            $"Roblox data: {Paths.RobloxBase}",
            $"Roblox versions: {Paths.Versions}",
            $"Log: {App.Logger.FileLocation ?? "Memory only"}",
            $"Latest error: {GithubUpdater.LastError ?? App.Logger.LatestError ?? "None in this process"}"
        });

        public BuildMetadataAttribute BuildMetadata => App.BuildMetadata;
            
        public string BuildTimestamp => BuildMetadata.Timestamp.ToFriendlyString();
        public string BuildCommitHashUrl => $"https://github.com/{App.ProjectRepository}/commit/{BuildMetadata.CommitHash}";

        public Visibility BuildInformationVisibility => App.IsProductionBuild ? Visibility.Collapsed : Visibility.Visible;
        public Visibility BuildCommitVisibility => App.IsActionBuild ? Visibility.Visible : Visibility.Collapsed;
    }
}
