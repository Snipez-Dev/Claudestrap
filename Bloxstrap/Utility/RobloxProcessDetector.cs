namespace Claudestrap.Utility
{
    /// <summary>
    /// Answers "is there actually a Roblox client up right now?".
    ///
    /// A plain <c>Process.GetProcessesByName("RobloxPlayerBeta").Length > 0</c> lies
    /// constantly, because Roblox regularly leaves processes behind that the user
    /// never sees: a client that crashed or was killed mid-shutdown keeps a
    /// windowless process alive, a failed launch leaves one hanging, and a process
    /// that has genuinely exited still shows up in the enumeration for as long as
    /// something in the system holds a handle to it. The Roblox singleton mutex is
    /// no better -- it outlives abnormal exits, so <c>Mutex.TryOpenExisting</c> keeps
    /// answering "yes" long after Roblox is gone.
    ///
    /// So a process only counts as a live client when it is not exited, belongs to
    /// this desktop session, and has an actual top-level window -- or is young
    /// enough that it hasn't had time to open one yet.
    /// </summary>
    public static class RobloxProcessDetector
    {
        /// <summary>Player process names, including the eurotrucks2 rename option.</summary>
        private static readonly string[] PlayerProcessNames = { "RobloxPlayerBeta", "eurotrucks2" };

        private static readonly string[] StudioProcessNames = { "RobloxStudioBeta" };

        /// <summary>
        /// How long a windowless Roblox process is still given the benefit of the
        /// doubt. A real client puts its window up within a couple of seconds; past
        /// this it's a ghost, not something the user is looking at.
        /// </summary>
        private static readonly TimeSpan StartupGracePeriod = TimeSpan.FromSeconds(60);

        /// <summary>
        /// How long a windowless process is left alone before it counts as a
        /// background resident rather than a client that's still starting up.
        /// </summary>
        private static readonly TimeSpan BackgroundGracePeriod = TimeSpan.FromSeconds(15);

        public static bool IsPlayerRunning() => HasLiveProcess(PlayerProcessNames);

        public static bool IsStudioRunning() => HasLiveProcess(StudioProcessNames);

        public static bool IsAnyRunning() => IsPlayerRunning() || IsStudioRunning();

        /// <summary>Live Roblox processes (player and studio), ghosts excluded.</summary>
        public static List<Process> GetLiveProcesses()
            => GetLiveProcesses(PlayerProcessNames.Concat(StudioProcessNames));

        /// <summary>Live Roblox player processes only (excludes Studio), ghosts excluded.</summary>
        public static List<Process> GetLivePlayerProcesses()
            => GetLiveProcesses(PlayerProcessNames);

        /// <summary>
        /// Player processes with no window of their own: Roblox's tray-mode resident
        /// (the app closed to the systray, still holding the last account's session and
        /// the client singleton) plus leftovers from crashed launches. The live-client
        /// checks deliberately ignore these because the user can't see them -- but an
        /// account switch has to clear them out, or the tray resident absorbs the launch
        /// and comes back up as its own account instead of the one that was picked.
        /// </summary>
        public static List<Process> GetBackgroundPlayerProcesses()
        {
            const string LOG_IDENT = "RobloxProcessDetector::GetBackgroundPlayerProcesses";

            var background = new List<Process>();
            int? sessionId = GetCurrentSessionId();

            foreach (string name in PlayerProcessNames)
            {
                Process[] processes;

                try
                {
                    processes = Process.GetProcessesByName(name);
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to enumerate {name}: {ex.Message}");
                    continue;
                }

                foreach (var process in processes)
                {
                    bool resident;

                    try
                    {
                        resident = IsBackgroundClient(process, sessionId);
                    }
                    catch (Exception ex)
                    {
                        // Can't inspect it -- leave it alone rather than kill something
                        // the user is actually looking at.
                        App.Logger.WriteLine(LOG_IDENT, $"Skipping {name} pid {SafeGetId(process)}: {ex.Message}");
                        resident = false;
                    }

                    if (resident)
                        background.Add(process);
                    else
                        process.Dispose();
                }
            }

            return background;
        }

        private static bool IsBackgroundClient(Process process, int? sessionId)
        {
            if (process.HasExited)
                return false;

            if (sessionId is not null && process.SessionId != sessionId)
                return false;

            if (process.MainWindowHandle != IntPtr.Zero)
                return false; // a client the user can see -- not a background resident

            DateTime startTime;

            try
            {
                startTime = process.StartTime;
            }
            catch
            {
                return false; // can't age it, so don't assume it's disposable
            }

            return DateTime.Now - startTime >= BackgroundGracePeriod;
        }

        private static bool HasLiveProcess(IEnumerable<string> names)
        {
            var live = GetLiveProcesses(names);

            foreach (var process in live)
                process.Dispose();

            return live.Count > 0;
        }

        private static List<Process> GetLiveProcesses(IEnumerable<string> names)
        {
            const string LOG_IDENT = "RobloxProcessDetector::GetLiveProcesses";

            var live = new List<Process>();
            int? sessionId = GetCurrentSessionId();
            int ghosts = 0;

            foreach (string name in names)
            {
                Process[] processes;

                try
                {
                    processes = Process.GetProcessesByName(name);
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to enumerate {name}: {ex.Message}");
                    continue;
                }

                foreach (var process in processes)
                {
                    bool alive;

                    try
                    {
                        alive = IsLiveClient(process, sessionId);
                    }
                    catch (Exception ex)
                    {
                        // Can't inspect it (access denied, exited mid-check) -- if we
                        // can't prove it's a real client, don't claim it is one.
                        App.Logger.WriteLine(LOG_IDENT, $"Skipping {name} pid {SafeGetId(process)}: {ex.Message}");
                        alive = false;
                    }

                    if (alive)
                    {
                        live.Add(process);
                    }
                    else
                    {
                        ghosts++;
                        process.Dispose();
                    }
                }
            }

            if (ghosts > 0)
                App.Logger.WriteLine(LOG_IDENT, $"Ignored {ghosts} dead/ghost Roblox process(es), {live.Count} live");

            return live;
        }

        private static bool IsLiveClient(Process process, int? sessionId)
        {
            if (process.HasExited)
                return false; // already dead, just still enumerated because a handle is open

            if (sessionId is not null && process.SessionId != sessionId)
                return false; // another user's desktop, not ours to worry about

            if (process.MainWindowHandle != IntPtr.Zero)
                return true;

            DateTime startTime;

            try
            {
                startTime = process.StartTime;
            }
            catch
            {
                return true; // can't age it, so assume it's a real client and warn
            }

            return DateTime.Now - startTime < StartupGracePeriod;
        }

        /// <summary>Our own session id, or null when it can't be read (skips the check).</summary>
        private static int? GetCurrentSessionId()
        {
            try
            {
                using var self = Process.GetCurrentProcess();
                return self.SessionId;
            }
            catch
            {
                return null;
            }
        }

        private static string SafeGetId(Process process)
        {
            try { return process.Id.ToString(); }
            catch { return "?"; }
        }
    }
}
