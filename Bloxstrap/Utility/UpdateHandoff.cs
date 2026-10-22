using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Claudestrap.Utility;

public static class UpdateHandoff
{
    public static async Task<bool> StartAndConfirmAsync(string executable)
        => await StartAndConfirmAsync(executable, Array.Empty<string>()).ConfigureAwait(false);

    public static async Task<bool> StartAndConfirmAsync(string executable, string[] restartArguments)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Launcher update handoff requires Windows.");
        string name = "Local\\Claudestrap-Update-" + Guid.NewGuid().ToString("N");
        using var ready = new EventWaitHandle(false, EventResetMode.ManualReset, name);
        var start = new ProcessStartInfo(executable) { UseShellExecute = false };
        foreach (string argument in restartArguments) start.ArgumentList.Add(argument);
        start.ArgumentList.Add("-updateready");
        start.ArgumentList.Add(name);
        start.ArgumentList.Add("-updateparent");
        start.ArgumentList.Add(Environment.ProcessId.ToString());
        using var child = Process.Start(start) ?? throw new IOException("The updated launcher could not be started.");
        bool confirmed = await Task.Run(() => ready.WaitOne(TimeSpan.FromSeconds(30))).ConfigureAwait(false);
        if (confirmed && !child.HasExited) return true;
        if (!child.HasExited)
        {
            child.Kill();
            await child.WaitForExitAsync().ConfigureAwait(false);
        }
        return false;
    }

    public static async Task ConfirmAndWaitAsync(string eventName, string parentId)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Launcher update handoff requires Windows.");
        if (!eventName.StartsWith("Local\\Claudestrap-Update-", StringComparison.Ordinal) || !int.TryParse(parentId, out int pid) || pid == Environment.ProcessId)
            throw new IOException("Invalid update handoff.");
        using var ready = EventWaitHandle.OpenExisting(eventName);
        Process? parent = null;
        try { parent = Process.GetProcessById(pid); } catch (ArgumentException) { }
        ready.Set();
        if (parent is null) return;
        using (parent)
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            await parent.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
    }
}
