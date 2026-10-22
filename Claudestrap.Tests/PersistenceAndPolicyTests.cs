using Claudestrap.Utility;

namespace Claudestrap.Tests;

public sealed class PersistenceAndPolicyTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "claudestrap-tests", Guid.NewGuid().ToString("N"));
    private string FilePath => Path.Combine(_folder, "settings.json");
    public PersistenceAndPolicyTests() => Directory.CreateDirectory(_folder);

    [Fact]
    public void CorruptPrimaryRecoversBackupWithoutReplacingOriginal()
    {
        string first = AtomicJsonFile.Write(FilePath, new Sample("first"), null);
        AtomicJsonFile.Write(FilePath, new Sample("second"), first);
        File.WriteAllText(FilePath, "{broken");
        var recovered = AtomicJsonFile.Read<Sample>(FilePath);
        Assert.True(recovered.Recovered);
        Assert.Equal("first", recovered.Value.Name);
        Assert.Equal("{broken", File.ReadAllText(FilePath));
        AtomicJsonFile.Write(FilePath, recovered.Value, recovered.Hash);
        Assert.Equal("first", AtomicJsonFile.Read<Sample>(FilePath).Value.Name);
        Assert.Single(Directory.GetFiles(_folder, "settings.json.corrupt-*"));
        Assert.Equal("first", AtomicJsonFile.Read<Sample>(FilePath + ".bak").Value.Name);
    }

    [Fact]
    public void StaleWriterCannotReplaceNewerSettings()
    {
        string original = AtomicJsonFile.Write(FilePath, new Sample("original"), null);
        AtomicJsonFile.Write(FilePath, new Sample("newer"), original);
        Assert.Throws<IOException>(() => AtomicJsonFile.Write(FilePath, new Sample("stale"), original));
        Assert.Equal("newer", AtomicJsonFile.Read<Sample>(FilePath).Value.Name);
    }

    [Fact]
    public async Task ConcurrentWritersLeaveOneCompleteDocument()
    {
        string original = AtomicJsonFile.Write(FilePath, new Sample("original"), null);
        async Task<bool> Attempt(string name) => await Task.Run(() =>
        {
            try { AtomicJsonFile.Write(FilePath, new Sample(name), original); return true; }
            catch (IOException) { return false; }
        });
        bool[] results = await Task.WhenAll(Attempt("one"), Attempt("two"));
        Assert.Single(results, result => result);
        Assert.Contains(AtomicJsonFile.Read<Sample>(FilePath).Value.Name, new[] { "one", "two" });
        Assert.Empty(Directory.GetFiles(_folder, "*.tmp"));
    }

    [Fact]
    public void MissingPrimaryCanRecoverAnExistingBackup()
    {
        AtomicJsonFile.Write(FilePath + ".bak", new Sample("backup"), null);
        var value = AtomicJsonFile.Read<Sample>(FilePath);
        Assert.True(value.Recovered);
        Assert.Null(value.Hash);
        AtomicJsonFile.Write(FilePath, value.Value, value.Hash);
        Assert.Equal("backup", AtomicJsonFile.Read<Sample>(FilePath).Value.Name);
    }

    [Fact]
    public void InvalidPrimaryAndBackupArePreservedOnReadFailure()
    {
        File.WriteAllText(FilePath, "broken");
        File.WriteAllText(FilePath + ".bak", "also broken");
        Assert.Throws<System.Text.Json.JsonException>(() => AtomicJsonFile.Read<Sample>(FilePath));
        Assert.Equal("broken", File.ReadAllText(FilePath));
        Assert.Equal("also broken", File.ReadAllText(FilePath + ".bak"));
    }

    [Theory]
    [InlineData(1)] [InlineData(7)] [InlineData(30)] [InlineData(60)]
    public void CleanerUsesDays(int days)
    {
        var now = new DateTime(2026, 10, 22, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(TimeSpan.FromDays(days), now - CleanerPolicy.CutoffUtc(now, days)!.Value);
    }

    [Fact]
    public void CleanerNeverDoesNotComputeAnInvalidDate()
    {
        Assert.Null(CleanerPolicy.CutoffUtc(DateTime.UtcNow, 0));
        Assert.Null(CleanerPolicy.CutoffUtc(DateTime.UtcNow, int.MaxValue));
        Assert.False(CleanerPolicy.IsExpired(DateTime.UtcNow.AddDays(-30), DateTime.UtcNow, DateTime.UtcNow.AddDays(-7)));
    }

    [Fact]
    public void SimultaneousStartsHaveIndependentLogFiles()
    {
        var now = DateTime.UtcNow;
        var names = Enumerable.Range(0, 50).Select(_ => LogFileNames.Create("Claudestrap", now, 100)).ToArray();
        Assert.Equal(names.Length, names.Distinct().Count());
        using var first = new FileStream(Path.Combine(_folder, names[0]), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        using var second = new FileStream(Path.Combine(_folder, names[1]), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
    }

    [Theory]
    [InlineData(null, false, false)] [InlineData("local", false, false)]
    [InlineData("upstream", true, false)] [InlineData("upstream", false, true)]
    public void CustomAndMultiFileBuildsCannotBeOverwritten(string? channel, bool multiFile, bool allowed)
        => Assert.Equal(allowed, UpdateBuildPolicy.CanReplace(channel, multiFile));

    public sealed record Sample(string Name);
    [Fact]
    public async Task StartupHandoffSignalsReadiness()
    {
        string name = "Local\\Claudestrap-Update-" + Guid.NewGuid().ToString("N");
        using var signal = new EventWaitHandle(false, EventResetMode.ManualReset, name);
        await UpdateHandoff.ConfirmAndWaitAsync(name, int.MaxValue.ToString());
        Assert.True(signal.WaitOne(0));
    }

    [Fact]
    public async Task InvalidStartupHandoffIsRejected()
        => await Assert.ThrowsAsync<IOException>(() => UpdateHandoff.ConfirmAndWaitAsync("unrelated-event", "123"));

    public void Dispose() => Directory.Delete(_folder, true);
}
