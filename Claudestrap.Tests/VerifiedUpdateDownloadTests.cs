using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Claudestrap.Utility;

namespace Claudestrap.Tests;

public sealed class VerifiedUpdateDownloadTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "claudestrap-update-tests", Guid.NewGuid().ToString("N"));
    private readonly byte[] _bytes = Enumerable.Range(0, 100).Select(i => (byte)i).ToArray();
    private string Destination => Path.Combine(_folder, "update.exe");
    private string Digest => Convert.ToHexString(SHA256.HashData(_bytes));
    public VerifiedUpdateDownloadTests() => Directory.CreateDirectory(_folder);

    [Fact]
    public async Task InterruptedTransferResumesAtActualSavedOffset()
    {
        var ranges = new List<long?>();
        using var client = Client(request =>
        {
            long? from = request.Headers.Range?.Ranges.Single().From;
            ranges.Add(from);
            return ranges.Count == 1 ? Response(new InterruptedStream(_bytes)) : RangeResponse(from!.Value);
        });
        await Downloader(client).DownloadAsync(new("https://example.test/update"), Destination, _bytes.Length, Digest);
        Assert.Equal(new long?[] { null, 4 }, ranges);
        Assert.Equal(_bytes, File.ReadAllBytes(Destination));
    }

    [Fact]
    public async Task ServerIgnoringRangeRestartsWithoutAppendingDuplicateBytes()
    {
        File.WriteAllBytes(Destination, _bytes[..4]);
        using var client = Client(_ => Response(new MemoryStream(_bytes)));
        await Downloader(client).DownloadAsync(new("https://example.test/update"), Destination, _bytes.Length, Digest);
        Assert.Equal(_bytes, File.ReadAllBytes(Destination));
    }

    [Fact]
    public async Task IncorrectRangeIsRejected()
    {
        File.WriteAllBytes(Destination, _bytes[..4]);
        using var client = Client(_ => RangeResponse(5));
        await Assert.ThrowsAsync<InvalidDataException>(() => Downloader(client).DownloadAsync(new("https://example.test/update"), Destination, _bytes.Length, Digest));
        Assert.Equal(4, new FileInfo(Destination).Length);
    }

    [Fact]
    public async Task IncorrectHashIsRejected()
    {
        using var client = Client(_ => Response(new MemoryStream(_bytes)));
        await Assert.ThrowsAsync<InvalidDataException>(() => Downloader(client).DownloadAsync(new("https://example.test/update"), Destination, _bytes.Length, new string('0', 64)));
    }

    [Fact]
    public async Task Http404IsNotRetried()
    {
        int calls = 0;
        using var client = Client(_ => { calls++; return new HttpResponseMessage(HttpStatusCode.NotFound); });
        await Assert.ThrowsAsync<HttpRequestException>(() => Downloader(client).DownloadAsync(new("https://example.test/update"), Destination, _bytes.Length, Digest));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task StalledStreamTimesOutInsteadOfHanging()
    {
        int calls = 0;
        using var client = Client(_ => { calls++; return Response(new StalledStream(_bytes)); });
        await Assert.ThrowsAsync<TimeoutException>(() => new VerifiedUpdateDownload(client, TimeSpan.FromMilliseconds(30), TimeSpan.Zero)
            .DownloadAsync(new("https://example.test/update"), Destination, _bytes.Length, Digest));
        Assert.Equal(4, calls);
    }

    [Fact]
    public async Task CancelledDownloadDoesNotStartARequest()
    {
        int calls = 0;
        using var client = Client(_ => { calls++; return Response(new MemoryStream(_bytes)); });
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Downloader(client)
            .DownloadAsync(new("https://example.test/update"), Destination, _bytes.Length, Digest, cancellationToken: cancellation.Token));
        Assert.Equal(0, calls);
    }

    [Fact]
    public async Task FailedStartupRollsBackExecutable()
    {
        string current = Path.Combine(_folder, "launcher.exe");
        File.WriteAllText(current, "old launcher");
        File.WriteAllText(Destination, "new launcher");
        await Assert.ThrowsAsync<IOException>(() => ExecutableUpdate.ApplyAsync(Destination, current, path =>
        {
            Assert.Equal("new launcher", File.ReadAllText(path));
            return Task.FromResult(false);
        }));
        Assert.Equal("old launcher", File.ReadAllText(current));
        Assert.Empty(Directory.GetFiles(_folder, "*.new"));
    }

    [Fact]
    public async Task SuccessfulStartupKeepsNewExecutableAndOldBackup()
    {
        string current = Path.Combine(_folder, "launcher.exe");
        File.WriteAllText(current, "old launcher");
        File.WriteAllText(Destination, "new launcher");
        await ExecutableUpdate.ApplyAsync(Destination, current, _ => Task.FromResult(true));
        Assert.Equal("new launcher", File.ReadAllText(current));
        Assert.Equal("old launcher", File.ReadAllText(current + ".old"));
    }

    private VerifiedUpdateDownload Downloader(HttpClient client) => new(client, retryDelay: TimeSpan.Zero);
    private static HttpClient Client(Func<HttpRequestMessage, HttpResponseMessage> response)
        => new(new Handler(response)) { Timeout = Timeout.InfiniteTimeSpan };
    private static HttpResponseMessage Response(Stream stream) => new(HttpStatusCode.OK) { Content = new StreamContent(stream) };
    private HttpResponseMessage RangeResponse(long from)
    {
        var response = new HttpResponseMessage(HttpStatusCode.PartialContent) { Content = new ByteArrayContent(_bytes[(int)from..]) };
        response.Content.Headers.ContentRange = new ContentRangeHeaderValue(from, _bytes.Length - 1, _bytes.Length);
        return response;
    }
    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => Task.FromResult(response(request));
    }
    private sealed class InterruptedStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            => Position == 0 ? base.ReadAsync(buffer[..4], cancellationToken) : ValueTask.FromException<int>(new IOException("Connection dropped"));
    }
    private sealed class StalledStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
    }
    public void Dispose() => Directory.Delete(_folder, true);
}
