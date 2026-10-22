using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace Claudestrap.Utility;

public sealed record JsonFileSnapshot<T>(T Value, string? Hash, bool Recovered);

public static class AtomicJsonFile
{
    public static string? Hash(string path) => File.Exists(path)
        ? Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))) : null;

    public static JsonFileSnapshot<T> Read<T>(string path) where T : class
    {
        using var lease = Acquire(path);
        string? hash = Hash(path);
        try
        {
            return new(ReadValue<T>(path), hash, false);
        }
        catch (Exception ex) when (ex is JsonException or FileNotFoundException)
        {
            return new(ReadValue<T>(path + ".bak"), hash, true);
        }
    }

    public static string Write<T>(string path, T value, string? expectedHash, bool checkForChanges = true) where T : class
    {
        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        using var lease = Acquire(fullPath);
        string? currentHash = Hash(fullPath);
        if (checkForChanges && !string.Equals(currentHash, expectedHash, StringComparison.Ordinal))
            throw new IOException("This file changed in another window or process. Reload it before saving: " + fullPath);

        byte[] data = JsonSerializer.SerializeToUtf8Bytes(value, new JsonSerializerOptions { WriteIndented = true });
        _ = JsonSerializer.Deserialize<T>(data) ?? throw new JsonException("Cannot save a null JSON document.");
        string temp = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(data);
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(fullPath))
            {
                string backup = fullPath + ".bak";
                try { _ = ReadValue<T>(fullPath); }
                catch (JsonException) { backup = fullPath + ".corrupt-" + Guid.NewGuid().ToString("N"); }
                File.Replace(temp, fullPath, backup);
            }
            else
                File.Move(temp, fullPath);

            return Convert.ToHexString(SHA256.HashData(data));
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    private static T ReadValue<T>(string path) where T : class
        => JsonSerializer.Deserialize<T>(File.ReadAllBytes(path)) ?? throw new JsonException("The JSON document is null.");

    private static IDisposable Acquire(string path)
    {
        string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(path).ToUpperInvariant())));
        var mutex = new Mutex(false, "Local\\Claudestrap-Json-" + key);
        try
        {
            try
            {
                if (!mutex.WaitOne(TimeSpan.FromSeconds(5))) throw new IOException("Timed out waiting for another settings writer.");
            }
            catch (AbandonedMutexException) { }
            return new MutexLease(mutex);
        }
        catch { mutex.Dispose(); throw; }
    }

    private sealed class MutexLease(Mutex mutex) : IDisposable
    {
        public void Dispose() { mutex.ReleaseMutex(); mutex.Dispose(); }
    }
}
