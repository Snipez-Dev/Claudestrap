using System.Runtime.InteropServices;

namespace Claudestrap
{
    /// <summary>
    /// Lets more than one Roblox client run at once. Ported from MultiRoblox-RAM's
    /// RobloxNative.cs "mutex"/"closehandles" commands.
    ///
    /// Roblox refuses to start a second copy because it checks for two named kernel
    /// objects at startup: a mutex "ROBLOX_singletonMutex" and an event
    /// "ROBLOX_singletonEvent". Holding the mutex ourselves (so Roblox never becomes
    /// its owner) and squatting the event's name with a Mutex object of the wrong
    /// kernel-object type (so Roblox can never create or open a working event under
    /// that name) makes every launch fall through to "no other instance found"
    /// instead of exiting. Any singleton event handle a still-running Roblox process
    /// already owns is force-closed the same way, via handle duplication with
    /// DUPLICATE_CLOSE_SOURCE — NtQuerySystemInformation is the only way to reach a
    /// handle owned by another process.
    /// </summary>
    public static class MultiInstance
    {
        private static Mutex? _singletonMutex;
        private static Mutex? _singletonEventMutex;

        /// <summary>
        /// Blocks forever holding the two singleton objects. Only ever called in a
        /// detached background process started via the "-multiinstanceholder" flag
        /// (see LaunchHandler.LaunchMultiInstanceHolder) so it never ties up a launch
        /// or the main bootstrapper.
        /// </summary>
        public static void RunHolder()
        {
            const string LOG_IDENT = "MultiInstance::RunHolder";

            using var ipl = new InterProcessLock("MultiInstanceHolder", TimeSpan.FromSeconds(2));

            if (!ipl.IsAcquired)
            {
                App.Logger.WriteLine(LOG_IDENT, "Another holder is already running, exiting");
                return;
            }

            try
            {
                _singletonMutex = new Mutex(true, "ROBLOX_singletonMutex", out bool created);
                if (!created)
                {
                    try { _singletonMutex.WaitOne(0); }
                    catch (AbandonedMutexException) { }
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"HoldMutex failed: {ex.Message}");
            }

            CloseExistingSingletonEventHandles();

            try
            {
                _singletonEventMutex = new Mutex(true, "ROBLOX_singletonEvent", out bool created);
                if (!created)
                {
                    try { _singletonEventMutex.WaitOne(0); }
                    catch (AbandonedMutexException) { }
                }
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"HoldEventMutex failed: {ex.Message}");
            }

            App.Logger.WriteLine(LOG_IDENT, "Holding Roblox singleton objects, multi-instance launching is active");

            // Keep the mutexes rooted and the process alive for as long as the user
            // wants multi-instance launching available. ipl (and the two singleton
            // objects) are only released when this process exits.
            Thread.Sleep(Timeout.Infinite);
        }

        /// <summary>
        /// Called right before every multi-instance-enabled launch: starts the holder
        /// process if it isn't already running, and force-closes any singleton event
        /// handle a currently-running Roblox instance still owns.
        /// </summary>
        public static void PrepareForLaunch()
        {
            const string LOG_IDENT = "MultiInstance::PrepareForLaunch";

            EnsureHolderRunning();
            CloseExistingSingletonEventHandles();

            App.Logger.WriteLine(LOG_IDENT, "Ready for multi-instance launch");
        }

        private static void EnsureHolderRunning()
        {
            const string LOG_IDENT = "MultiInstance::EnsureHolderRunning";

            using var ipl = new InterProcessLock("MultiInstanceHolder");

            if (!ipl.IsAcquired)
                return; // a holder process already owns the lock

            ipl.Dispose(); // release immediately so the spawned holder can claim it for real

            try
            {
                Process.Start(Paths.Process, "-multiinstanceholder");
                App.Logger.WriteLine(LOG_IDENT, "Started multi-instance mutex holder");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to start holder: {ex.Message}");
            }
        }

        // ---- singleton event handle closer (ported from HandleCloser in RobloxNative.cs) ----

        [DllImport("ntdll.dll")] private static extern int NtQuerySystemInformation(int cls, IntPtr buf, int size, out int ret);
        [DllImport("kernel32.dll")] private static extern IntPtr OpenProcess(int access, bool inherit, int pid);
        [DllImport("kernel32.dll")] private static extern bool DuplicateHandle(IntPtr srcProc, IntPtr srcHandle, IntPtr tgtProc, out IntPtr tgtHandle, int access, bool inherit, int opts);
        [DllImport("kernel32.dll")] private static extern IntPtr GetCurrentProcess();
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr h);
        [DllImport("ntdll.dll")] private static extern int NtQueryObject(IntPtr h, int cls, IntPtr buf, int size, out int ret);

        private const int SystemExtendedHandleInformation = 64;
        private const int PROCESS_DUP_HANDLE = 0x0040;
        private const int DUPLICATE_CLOSE_SOURCE = 0x1;
        private const int DUPLICATE_SAME_ACCESS = 0x2;

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_HANDLE_TABLE_ENTRY_INFO_EX
        {
            public IntPtr Object;
            public IntPtr UniqueProcessId;
            public IntPtr HandleValue;
            public int GrantedAccess;
            public short CreatorBackTraceIndex;
            public short ObjectTypeIndex;
            public int HandleAttributes;
            public int Reserved;
        }

        public static void CloseExistingSingletonEventHandles()
        {
            const string LOG_IDENT = "MultiInstance::CloseExistingSingletonEventHandles";

            var robloxPids = new HashSet<int>();
            foreach (var p in Process.GetProcessesByName(App.RobloxPlayerAppName))
                robloxPids.Add(p.Id);

            if (robloxPids.Count == 0)
                return;

            int size = 1 << 20;
            IntPtr buf = IntPtr.Zero;

            try
            {
                while (true)
                {
                    buf = Marshal.AllocHGlobal(size);
                    int status = NtQuerySystemInformation(SystemExtendedHandleInformation, buf, size, out _);
                    if (status == 0)
                        break;

                    Marshal.FreeHGlobal(buf);
                    buf = IntPtr.Zero;

                    if (status == unchecked((int)0xC0000004)) // STATUS_INFO_LENGTH_MISMATCH
                    {
                        size *= 2;
                        continue;
                    }

                    return;
                }

                long count = Marshal.ReadInt64(buf);
                int entrySize = Marshal.SizeOf<SYSTEM_HANDLE_TABLE_ENTRY_INFO_EX>();
                IntPtr entries = buf + IntPtr.Size * 2; // skip NumberOfHandles + Reserved

                IntPtr self = GetCurrentProcess();
                int closed = 0;

                for (long i = 0; i < count; i++)
                {
                    var entry = Marshal.PtrToStructure<SYSTEM_HANDLE_TABLE_ENTRY_INFO_EX>(entries + (int)(i * entrySize));

                    int pid = (int)entry.UniqueProcessId;
                    if (!robloxPids.Contains(pid))
                        continue;

                    IntPtr srcProc = OpenProcess(PROCESS_DUP_HANDLE, false, pid);
                    if (srcProc == IntPtr.Zero)
                        continue;

                    try
                    {
                        if (!DuplicateHandle(srcProc, entry.HandleValue, self, out IntPtr dupHandle, 0, false, DUPLICATE_SAME_ACCESS))
                            continue;

                        try
                        {
                            int nameBufSize = 1024;
                            IntPtr nameBuf = Marshal.AllocHGlobal(nameBufSize);
                            try
                            {
                                NtQueryObject(dupHandle, 1, nameBuf, nameBufSize, out _); // ObjectNameInformation = 1
                                short len = Marshal.ReadInt16(nameBuf);
                                if (len > 0)
                                {
                                    IntPtr strPtr = Marshal.ReadIntPtr(nameBuf, IntPtr.Size == 8 ? 8 : 4);
                                    string? name = Marshal.PtrToStringUni(strPtr, len / 2);
                                    if (name is not null && name.Contains("ROBLOX_singletonEvent"))
                                    {
                                        DuplicateHandle(srcProc, entry.HandleValue, IntPtr.Zero, out _, 0, false, DUPLICATE_CLOSE_SOURCE);
                                        closed++;
                                    }
                                }
                            }
                            finally { Marshal.FreeHGlobal(nameBuf); }
                        }
                        finally { CloseHandle(dupHandle); }
                    }
                    finally { CloseHandle(srcProc); }
                }

                if (closed > 0)
                    App.Logger.WriteLine(LOG_IDENT, $"Closed {closed} singleton event handle(s)");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Handle scan failed: {ex.Message}");
            }
            finally
            {
                if (buf != IntPtr.Zero)
                    Marshal.FreeHGlobal(buf);
            }
        }
    }
}
