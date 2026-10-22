using Claudestrap.Utility;

namespace Claudestrap.Tests;

public sealed class UpdateBackupPathsTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "claudestrap-update-tests", Guid.NewGuid().ToString("N"));
    private string ExecutablePath => Path.Combine(_directory, "Claudestrap.exe");

    public UpdateBackupPathsTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void UnlockedBackupIsRemovedWithoutTouchingCurrentExecutable()
    {
        File.WriteAllText(ExecutablePath, "current");
        File.WriteAllText(ExecutablePath + ".old", "previous");

        Assert.Equal(ExecutablePath + ".old", UpdateBackupPaths.Prepare(ExecutablePath));
        Assert.False(File.Exists(ExecutablePath + ".old"));
        Assert.Equal("current", File.ReadAllText(ExecutablePath));
    }

    [Fact]
    public void LockedBackupDoesNotPreventTheNextExecutableSwap()
    {
        File.WriteAllText(ExecutablePath, "current");
        File.WriteAllText(ExecutablePath + ".old", "previous");
        using var heldBackup = new FileStream(ExecutablePath + ".old", FileMode.Open, FileAccess.Read, FileShare.Read);

        string backup = UpdateBackupPaths.Prepare(ExecutablePath);
        Assert.NotEqual(ExecutablePath + ".old", backup);
        Assert.Equal(_directory, Path.GetDirectoryName(backup));

        File.Move(ExecutablePath, backup);
        File.WriteAllText(ExecutablePath, "updated");

        Assert.Equal("current", File.ReadAllText(backup));
        Assert.Equal("updated", File.ReadAllText(ExecutablePath));
        Assert.True(File.Exists(ExecutablePath + ".old"));
        Assert.NotEqual(backup, UpdateBackupPaths.Prepare(ExecutablePath));
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);
}
