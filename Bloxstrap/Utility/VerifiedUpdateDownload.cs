using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Claudestrap.Utility;

public sealed class VerifiedUpdateDownload(HttpClient client, TimeSpan? idleTimeout = null, TimeSpan? retryDelay = null)
{
    private readonly TimeSpan _idleTimeout = idleTimeout ?? TimeSpan.FromSeconds(30);
    private readonly TimeSpan _retryDelay = retryDelay ?? TimeSpan.FromSeconds(2);

    public async Task DownloadAsync(Uri uri, string destination, long size, string sha256,
        Action<long, long>? progress = null, Action<string>? status = null, CancellationToken cancellationToken = default)
    {
        if (size <= 0 || sha256.Length != 64 || !IsHex(sha256)) throw new InvalidDataException("Release size or SHA-256 is missing.");
        for (int attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            long offset = File.Exists(destination) ? new FileInfo(destination).Length : 0;
            if (offset > size) throw new InvalidDataException("The partial update is larger than the release.");
            if (offset == size) break;
            try
            {
                await TransferAsync(uri, destination, offset, size, progress, cancellationToken).ConfigureAwait(false);
                break;
            }
            catch (Exception ex) when (attempt < 4 && IsConnectionFailure(ex) && !cancellationToken.IsCancellationRequested)
            {
                long saved = File.Exists(destination) ? new FileInfo(destination).Length : 0;
                status?.Invoke($"Connection interrupted; resuming at {saved:N0} bytes (attempt {attempt + 1}/4)...");
                await Task.Delay(_retryDelay * attempt, cancellationToken).ConfigureAwait(false);
            }
        }
        await VerifyFileAsync(destination, size, sha256, cancellationToken).ConfigureAwait(false);
    }

    public static async Task VerifyFileAsync(string destination, long size, string sha256, CancellationToken cancellationToken = default)
    {
        using var file = File.OpenRead(destination);
        string actualHash = Convert.ToHexString(await SHA256.HashDataAsync(file, cancellationToken).ConfigureAwait(false));
        if (file.Length != size || !actualHash.Equals(sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The update failed the release size/SHA-256 check. The installed version has not been changed.");
    }

    private async Task TransferAsync(Uri uri, string destination, long offset, long size, Action<long, long>? progress, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(_idleTimeout);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        if (offset > 0) request.Headers.Range = new RangeHeaderValue(offset, null);
        HttpResponseMessage response;
        try { response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw new TimeoutException("The update connection timed out."); }
        using (response)
        {
            response.EnsureSuccessStatusCode();
            bool partial = response.StatusCode == HttpStatusCode.PartialContent;
            if (partial && (response.Content.Headers.ContentRange?.From != offset || response.Content.Headers.ContentRange?.To != size - 1 || response.Content.Headers.ContentRange?.Length != size))
                throw new InvalidDataException("The server returned an inconsistent download range.");
            if (!partial && response.StatusCode != HttpStatusCode.OK) throw new InvalidDataException("Unexpected update response.");
            long received = partial ? offset : 0;
            long expectedBody = size - received;
            if (response.Content.Headers.ContentLength is long length && length != expectedBody)
                throw new InvalidDataException("The server's update length does not match the release.");
            using var source = await response.Content.ReadAsStreamAsync(timeout.Token).ConfigureAwait(false);
            using var target = new FileStream(destination, partial && offset > 0 ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.None);
            byte[] buffer = new byte[81920];
            var lastReport = DateTime.MinValue;
            while (true)
            {
                timeout.CancelAfter(_idleTimeout);
                int read;
                try { read = await source.ReadAsync(buffer.AsMemory(), timeout.Token).ConfigureAwait(false); }
                catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw new TimeoutException("The update download stopped receiving data."); }
                catch (IOException ex) { throw new DownloadReadException(ex); }
                if (read == 0) break;
                if (received + read > size) throw new InvalidDataException("The server sent more bytes than expected.");
                timeout.CancelAfter(Timeout.InfiniteTimeSpan);
                // Local disk failures must not be retried as network failures.
                await target.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false);
                received += read;
                if (DateTime.UtcNow - lastReport > TimeSpan.FromMilliseconds(100))
                {
                    progress?.Invoke(received, size);
                    lastReport = DateTime.UtcNow;
                }
            }
            await target.FlushAsync(token).ConfigureAwait(false);
            if (received != size) throw new DownloadReadException(new EndOfStreamException("The update ended early."));
            progress?.Invoke(received, size);
        }
    }

    private static bool IsConnectionFailure(Exception ex) => ex is TimeoutException or DownloadReadException
        || ex is HttpRequestException http && (http.StatusCode is null || (int)http.StatusCode >= 500 || http.StatusCode == HttpStatusCode.RequestTimeout);

    private static bool IsHex(string text)
    {
        foreach (char c in text) if (!Uri.IsHexDigit(c)) return false;
        return true;
    }

    private sealed class DownloadReadException(Exception inner) : IOException("The update connection was interrupted.", inner);
}
