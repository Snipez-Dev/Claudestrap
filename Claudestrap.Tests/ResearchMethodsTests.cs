using Claudestrap.Utility;

namespace Claudestrap.Tests;

public class ResearchMethodsTests
{
    [Fact]
    public async Task CompletedPersistentDownloadIsVerifiedWithoutStartingAnotherTransfer()
    {
        string root=Path.Combine(Path.GetTempPath(), "claudestrap-cache-tests", Guid.NewGuid().ToString("N"));
        byte[] bytes={1,2,3,4};
        string hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
        string directory=PersistentUpdateDownload.CacheDirectory(root,hash);
        Directory.CreateDirectory(directory);
        string file=Path.Combine(directory,"Claudestrap.http.exe");
        try
        {
            File.WriteAllBytes(file,bytes);
            using var http=new HttpClient(new NoRequests());
            Assert.Equal(file,await PersistentUpdateDownload.DownloadAsync(http,new Uri("https://example.test/update"),root,bytes.Length,hash));
        }
        finally { Directory.Delete(root,true); }
    }

    private sealed class NoRequests : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
            => throw new InvalidOperationException("A valid cached download must not start a network request.");
    }

    [Fact]
    public void PublishedFlagsDoNotClaimProvenClientEffect()
    {
        Assert.True(FastFlagSupportCatalog.InPublishedReference("FIntDebugForceMSAASamples"));
        Assert.Contains("ungeprüft", FastFlagSupportCatalog.Describe("FIntDebugForceMSAASamples"));
    }

    [Fact]
    public void UnknownFlagsDoNotBecomeAllowedBecauseTheyExistInATracker()
    {
        Assert.False(FastFlagSupportCatalog.InPublishedReference("UnlistedFlag"));
        Assert.Contains("aktuelle Freigabe ungeprüft", FastFlagSupportCatalog.Describe("UnlistedFlag"));
        Assert.Contains("1/2", FastFlagSupportCatalog.Summary(new[] { "FIntDebugForceMSAASamples", "UnlistedFlag" }));
    }

    [Fact]
    public void UpdateHandoffArgumentsAreRemovedWithoutLosingTheGameJoin()
    {
        string[] args = { "roblox-player:test", "-updateready", "old-event", "-updateparent", "123", "-quiet" };
        Assert.Equal(new[] { "roblox-player:test", "-quiet" }, RestartArguments.Clean(args));
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("GGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGGG")]
    public void DownloadCacheRejectsInvalidReleaseIdentifiers(string digest)
        => Assert.Throws<ArgumentException>(() => PersistentUpdateDownload.CacheDirectory(Path.GetTempPath(), digest));

    [Fact]
    public void DownloadCacheIsStableAcrossApplicationRestarts()
    {
        string root = Path.Combine(Path.GetTempPath(), "claudestrap-tests");
        Assert.Equal(PersistentUpdateDownload.CacheDirectory(root, new string('a', 64)),
            PersistentUpdateDownload.CacheDirectory(root, new string('A', 64)));
        Assert.Equal(Path.GetFullPath(root), Path.GetDirectoryName(PersistentUpdateDownload.CacheDirectory(root, new string('a', 64))));
    }
}
