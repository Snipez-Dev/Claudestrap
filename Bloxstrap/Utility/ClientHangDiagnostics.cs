using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using NAudio.CoreAudioApi;

namespace Claudestrap.Utility
{
    public sealed class ClientHangDiagnostics
    {
        private const string LogIdent = "ClientHangDiagnostics";
        private readonly HangDetectionState _state = new(DateTimeOffset.UtcNow,
            TimeSpan.FromSeconds(45), TimeSpan.FromSeconds(15));
        private readonly string? _logFile;
        private readonly object _initialAudioDevices;

        public ClientHangDiagnostics(string? logFile)
        {
            _logFile = logFile;
            _initialAudioDevices = SnapshotAudioDevices();
        }

        public async Task ObserveAsync(Process process)
        {
            try
            {
                process.Refresh();
                if (process.HasExited) return;

                if (!_state.Observe(DateTimeOffset.UtcNow, process.Responding,
                    process.MainWindowHandle != IntPtr.Zero)) return;

                // DbgHelp runs on one worker, once per watched client. No
                // termination, automatic restart or network upload.
                await Task.Run(() => Capture(process));
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LogIdent, $"Could not inspect client: {ex.Message}");
            }
        }

        private void Capture(Process process)
        {
            string directory = Path.Combine(Paths.Base, "Diagnostics",
                $"{DateTime.UtcNow:yyyyMMddTHHmmssfffZ}_{process.Id}");

            try
            {
                Directory.CreateDirectory(directory);
                string dumpPath = Path.Combine(directory, "Roblox-hang.dmp");
                string? dumpError = null;
                bool dumpWritten = false;

                App.Logger.WriteLine(LogIdent, $"Client stopped responding; saving diagnostics to {directory}");

                try
                {
                    using var handle = OpenProcess(0x0410, false, (uint)process.Id);
                    if (handle.IsInvalid) throw new Win32Exception(Marshal.GetLastWin32Error());

                    using var file = new FileStream(dumpPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    // Thread information, unloaded modules, thread environment and
                    // indirectly referenced memory for debugging blocked threads.
                    dumpWritten = MiniDumpWriteDump(handle, (uint)process.Id, file.SafeFileHandle,
                        0x00001160, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                    if (!dumpWritten)
                    {
                        int error = Marshal.GetLastWin32Error();
                        dumpError = $"0x{error:X8}: {new Win32Exception(error).Message}";
                    }
                }
                catch (Exception ex)
                {
                    dumpError = ex.Message;
                }

                if (!dumpWritten && File.Exists(dumpPath)) File.Delete(dumpPath);

                var report = new
                {
                    CapturedAtUtc = DateTimeOffset.UtcNow,
                    ProcessId = process.Id,
                    Reason = "Window unresponsive for at least 15 seconds after a 45-second startup grace period",
                    AppVersion = typeof(ClientHangDiagnostics).Assembly.GetName().Version?.ToString(),
                    WindowsVersion = Environment.OSVersion.VersionString,
                    GraphicsProfile = GraphicsProfiles.GetEffectiveProfile(
                        App.Settings.Prop.SelectedGraphicsProfile, App.FastFlags.GetValue)?.ToString() ?? "Custom",
                    App.Settings.Prop.ClientStabilityMode,
                    DumpWritten = dumpWritten,
                    DumpError = dumpError,
                    InitialAudioDevices = _initialAudioDevices,
                    CurrentAudioDevices = SnapshotAudioDevices(),
                    // Capture only diagnostic categories, not full logs with join
                    // URLs, account identifiers or authentication arguments.
                    RelevantLogLines = ReadRelevantLogLines()
                };

                File.WriteAllText(Path.Combine(directory, "report.json"),
                    JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
                App.Logger.WriteLine(LogIdent, dumpWritten
                    ? "Report and dump saved locally."
                    : $"Report saved; dump unavailable: {dumpError}");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LogIdent, $"Could not save diagnostics: {ex.Message}");
            }
        }

        private string[] ReadRelevantLogLines()
        {
            if (string.IsNullOrEmpty(_logFile)) return Array.Empty<string>();
            try
            {
                using var file = new FileStream(_logFile, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(file);
                var lines = new Queue<string>();
                string? line;
                while ((line = reader.ReadLine()) is not null)
                {
                    if (!line.Contains("[FLog::D3D11SwapChain]")
                        && !line.Contains("[FLog::CrashReportLog]")
                        && !line.Contains("[DFLog::ClientBlockedImage]")
                        && !line.Contains("[FLog::Audio]")) continue;

                    lines.Enqueue(line);
                    if (lines.Count > 40) lines.Dequeue();
                }
                return lines.ToArray();
            }
            catch (Exception ex)
            {
                return new[] { $"Log unavailable: {ex.Message}" };
            }
        }

        private static object SnapshotAudioDevices()
        {
            try
            {
                using var enumerator = new MMDeviceEnumerator();
                var devices = new List<object>();
                foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.All, DeviceState.Active))
                {
                    using (device)
                        devices.Add(new { device.ID, device.FriendlyName, Flow = device.DataFlow.ToString() });
                }
                return devices;
            }
            catch (Exception ex)
            {
                return new { Error = ex.Message };
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern SafeProcessHandle OpenProcess(uint access, bool inheritHandle, uint processId);

        [DllImport("dbghelp.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool MiniDumpWriteDump(SafeProcessHandle process, uint processId,
            SafeFileHandle file, uint dumpType, IntPtr exception, IntPtr userStream, IntPtr callback);
    }
}
