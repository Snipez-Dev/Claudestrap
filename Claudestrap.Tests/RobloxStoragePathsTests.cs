using Claudestrap.Utility;

namespace Claudestrap.Tests;

public class RobloxStoragePathsTests
{
    private readonly string _installation = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "strapper-tests", "portable"));
    private readonly string _legacy = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "strapper-tests", "appdata"));

    [Fact]
    public void BrokenAppDataLinkFallsBackToInstallationStorage()
    {
        string result = RobloxStoragePaths.Resolve(_installation, _legacy, directoryExists: _ => false);
        Assert.Equal(Path.Combine(_installation, "ClaudestrapData"), result);
    }

    [Fact]
    public void ExistingLegacyVersionsAreReused()
    {
        string result = RobloxStoragePaths.Resolve(_installation, _legacy,
            directoryExists: path => path == Path.Combine(_legacy, "RblxVersions"));
        Assert.Equal(_legacy, result);
    }

    [Fact]
    public void InstallationLocalVersionsTakePriorityOverLegacyCache()
    {
        string local = Path.Combine(_installation, "ClaudestrapData");
        var existing = new HashSet<string> { Path.Combine(local, "RblxVersions"), Path.Combine(_legacy, "RblxVersions") };
        Assert.Equal(local, RobloxStoragePaths.Resolve(_installation, _legacy, directoryExists: existing.Contains));
    }

    [Fact]
    public void VersionsBesideExecutableAreReused()
    {
        Assert.Equal(_installation, RobloxStoragePaths.Resolve(_installation, _legacy,
            directoryExists: path => path == Path.Combine(_installation, "RblxVersions")));
    }

    [Fact]
    public void ExplicitStorageRootIsHonored()
    {
        string configured = Path.Combine(_installation, "custom-cache");
        Assert.Equal(configured, RobloxStoragePaths.Resolve(_installation, _legacy, configured, _ => true));
    }

    [Fact]
    public void DefaultInstallationDoesNotGetAnExtraDataSubdirectory()
    {
        Assert.Equal(_legacy, RobloxStoragePaths.Resolve(_legacy, _legacy, directoryExists: _ => false));
    }

    [Theory]
    [InlineData("AppSettings.json")]
    [InlineData("Settings.json")]
    public void PortableInstallIsRecognizedWithModernOrLegacySettings(string settingsFile)
    {
        var files = new HashSet<string>
        {
            Path.Combine(_installation, settingsFile), Path.Combine(_installation, "State.json"),
            Path.Combine(_installation, "Claudestrap.exe"), Path.Combine(_installation, "DownloadStats.json"),
            Path.Combine(_installation, "extra-file.txt")
        };
        Assert.True(RobloxStoragePaths.IsPortableInstallation(_installation, files.Contains));
    }

    [Fact]
    public void IncompletePortableSettingsDoNotSelectAnUnrelatedFolder()
    {
        Assert.False(RobloxStoragePaths.IsPortableInstallation(_installation,
            path => path == Path.Combine(_installation, "State.json")));
        Assert.False(RobloxStoragePaths.IsPortableInstallation(_installation,
            path => path == Path.Combine(_installation, "AppSettings.json")));
    }
}
