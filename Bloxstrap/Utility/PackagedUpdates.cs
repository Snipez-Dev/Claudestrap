using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Velopack;
using Velopack.Locators;
using Claudestrap.UI.Elements.Dialogs;

namespace Claudestrap.Utility;

public static class PackagedUpdates
{
    public static bool IsManaged => VelopackLocator.IsCurrentSet && VelopackLocator.Current.CurrentlyInstalledVersion is not null;
    public static string DataDirectory => Path.Combine(VelopackLocator.Current.RootAppDir!, "UserData");

    public static async Task<UpdateCheckResult> CheckAsync(bool quiet)
    {
        string source = App.Settings.Prop.PackageUpdateSource;
        if (string.IsNullOrWhiteSpace(source)) return UpdateCheckResult.NotUpdatable;
        if (!Directory.Exists(source) && (!Uri.TryCreate(source, UriKind.Absolute, out var uri) || uri.Scheme != "https"))
            throw new IOException("The package update source must be an HTTPS feed or an existing local directory.");
        var manager = new UpdateManager(source, new UpdateOptions { ExplicitChannel = App.Settings.Prop.PackageUpdateChannel });
        var update = await manager.CheckForUpdatesAsync();
        if (update is null) return UpdateCheckResult.UpToDate;
        if (!quiet && Frontend.ShowMessageBox($"A package update is available ({update.TargetFullRelease.Version}). Install and restart?",
            MessageBoxImage.Information, MessageBoxButton.YesNo) != MessageBoxResult.Yes) return UpdateCheckResult.Declined;
        var progress = quiet ? null : new UpdateProgressDialog(update.TargetFullRelease.Version.ToString());
        progress?.Show();
        try
        {
            progress?.SetStatus("Downloading package changes...");
            await manager.DownloadUpdatesAsync(update, percent => progress?.SetPercent(percent));
            manager.ApplyUpdatesAndRestart(update.TargetFullRelease, RestartArguments.Clean(App.LaunchSettings.Args));
            return UpdateCheckResult.Applied;
        }
        finally { progress?.CloseDialog(); }
    }
}
