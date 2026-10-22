using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Claudestrap.Utility;

public static class PersistentUpdateDownload
{
    public static string CacheDirectory(string root, string sha256)
    {
        if (sha256.Length != 64) throw new ArgumentException("A SHA-256 release identity is required.");
        foreach (char c in sha256) if (!Uri.IsHexDigit(c)) throw new ArgumentException("Invalid release identity.");
        return Path.Combine(Path.GetFullPath(root), sha256.ToUpperInvariant());
    }

    public static async Task<string> DownloadAsync(HttpClient client, Uri source, string cacheRoot, long size, string sha256,
        Action<long, long>? progress = null, Action<string>? status = null, CancellationToken token = default)
    {
        string directory = CacheDirectory(cacheRoot, sha256);
        Directory.CreateDirectory(directory);
        string httpFile = Path.Combine(directory, "Claudestrap.http.exe");
        if (File.Exists(httpFile) && new FileInfo(httpFile).Length > 0)
        {
            if (new FileInfo(httpFile).Length >= size)
            {
                try { await VerifiedUpdateDownload.VerifyFileAsync(httpFile, size, sha256, token); return httpFile; }
                catch (InvalidDataException) { File.Delete(httpFile); }
            }
            else
            {
                await new VerifiedUpdateDownload(client).DownloadAsync(source, httpFile, size, sha256, progress, status, token);
                return httpFile;
            }
        }
        string bitsFile = Path.Combine(directory, "Claudestrap.bits.exe");
        if (File.Exists(bitsFile))
        {
            try { await VerifiedUpdateDownload.VerifyFileAsync(bitsFile, size, sha256, token); return bitsFile; }
            catch (InvalidDataException) { File.Delete(bitsFile); }
        }
        bool completed = false;
        if (OperatingSystem.IsWindows())
        {
            try { completed = await RunBitsAsync(source, bitsFile, size, sha256, progress, status, token); }
            catch (BitsUnavailableException ex) { status?.Invoke("BITS unavailable; continuing with HTTP: " + ex.Message); }
        }
        if (completed)
        {
            await VerifiedUpdateDownload.VerifyFileAsync(bitsFile, size, sha256, token);
            return bitsFile;
        }
        await new VerifiedUpdateDownload(client).DownloadAsync(source, httpFile, size, sha256, progress, status, token);
        return httpFile;
    }

    private static async Task<bool> RunBitsAsync(Uri source, string destination, long size, string sha256,
        Action<long, long>? progress, Action<string>? status, CancellationToken token)
    {
        string script;
        try
        {
            using var embedded = Assembly.GetExecutingAssembly().GetManifestResourceStream("Claudestrap.Resources.UpdateDownload.ps1")
                ?? throw new IOException("BITS helper resource is missing.");
            using var memory = new MemoryStream();
            await embedded.CopyToAsync(memory, token);
            byte[] bytes = memory.ToArray();
            script = Path.Combine(Path.GetDirectoryName(destination)!, "Bits-" + Convert.ToHexString(SHA256.HashData(bytes))[..12] + ".ps1");
            await File.WriteAllBytesAsync(script, bytes, token);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { throw new BitsUnavailableException(ex.Message); }
        var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"))
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        using var parent = Process.GetCurrentProcess();
        string destinationId = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(destination.ToUpperInvariant())))[..12];
        foreach (string arg in new[] { "-NoLogo", "-NoProfile", "-NonInteractive", "-File", script,
            "-Source", source.AbsoluteUri, "-Destination", destination, "-JobName", "Claudestrap-Update-" + sha256.ToUpperInvariant() + "-" + destinationId,
            "-Digest", sha256.ToUpperInvariant(), "-ExpectedSize", size.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "-ParentPid", Environment.ProcessId.ToString(), "-ParentStartedTicks", parent.StartTime.ToUniversalTime().Ticks.ToString() }) start.ArgumentList.Add(arg);
        Process process;
        try { process = Process.Start(start) ?? throw new IOException("Could not start the BITS helper."); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or IOException) { throw new BitsUnavailableException(ex.Message); }
        using (process)
        {
            using var stop = token.Register(() => { try { if (!process.HasExited) process.Kill(); } catch (InvalidOperationException) { } });
            Task<string> errors = process.StandardError.ReadToEndAsync(token);
            string? message = null;
            bool reportedState = false;
            while (await process.StandardOutput.ReadLineAsync(token) is string line)
            {
                using var state = JsonDocument.Parse(line);
                reportedState = true;
                message = state.RootElement.GetProperty("Message").GetString();
                progress?.Invoke(state.RootElement.GetProperty("Bytes").GetInt64(), size);
                string phase = state.RootElement.GetProperty("State").GetString() ?? "";
                if (phase is "Attached" or "TransientError") status?.Invoke("Persistent Windows download: " + phase);
            }
            await process.WaitForExitAsync(token);
            string stderr = await errors;
            if (!reportedState && process.ExitCode != 0) throw new BitsUnavailableException(stderr);
            if (process.ExitCode == 2) throw new BitsUnavailableException(message ?? stderr);
            if (process.ExitCode != 0) throw new IOException(message ?? stderr);
            return true;
        }
    }

    private sealed class BitsUnavailableException(string message) : IOException(message);
}
