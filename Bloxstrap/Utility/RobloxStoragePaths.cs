namespace Claudestrap.Utility
{
    public static class RobloxStoragePaths
    {
        public static bool IsPortableInstallation(string directory, Func<string, bool>? fileExists = null)
        {
            fileExists ??= File.Exists;
            return fileExists(Path.Combine(directory, "State.json"))
                && (fileExists(Path.Combine(directory, "AppSettings.json"))
                    || fileExists(Path.Combine(directory, "Settings.json")));
        }

        public static string Resolve(string installationDirectory, string legacyDirectory,
            string? explicitDirectory = null, Func<string, bool>? directoryExists = null)
        {
            directoryExists ??= Directory.Exists;
            string installation = Path.GetFullPath(installationDirectory);
            string legacy = Path.GetFullPath(legacyDirectory);

            if (!string.IsNullOrWhiteSpace(explicitDirectory))
                return Path.GetFullPath(explicitDirectory);

            string local = Path.Combine(installation, "ClaudestrapData");
            if (directoryExists(Path.Combine(local, "RblxVersions"))) return local;
            if (directoryExists(Path.Combine(installation, "RblxVersions"))) return installation;
            if (directoryExists(Path.Combine(legacy, "RblxVersions"))) return legacy;

            // The version subdirectory is unreachable through a dangling
            // junction. A custom install must not keep writing through it.
            return string.Equals(Path.TrimEndingDirectorySeparator(installation),
                Path.TrimEndingDirectorySeparator(legacy), StringComparison.OrdinalIgnoreCase)
                ? legacy : local;
        }
    }
}
