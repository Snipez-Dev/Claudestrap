using System.Windows;
using Claudestrap.AppData;

namespace Claudestrap.Utility
{
    /// <summary>
    /// Launches Roblox as a specific saved account: cookie -> CSRF token ->
    /// auth ticket -> roblox-player launch URI, spawned directly against the
    /// already-installed client. Mirrors MultiRoblox-RAM's do_launch, minus
    /// the game-target resolution (this is always a home/app launch) since
    /// account switching, not game joining, is the point here.
    /// </summary>
    public static class AccountLauncher
    {
        public sealed record LaunchResult(bool Ok, string? Error);
        public sealed record LaunchUriResult(bool Ok, string? Error, string? Uri);

        private static async Task<LaunchUriResult> GetLaunchUriAsync(RobloxAccount account)
        {
            const string LOG_IDENT = "AccountLauncher::GetLaunchUriAsync";

            string? cookie = AccountCookieProtector.Unprotect(account.EncryptedCookie);
            if (string.IsNullOrEmpty(cookie))
                return new LaunchUriResult(false, "Could not decrypt this account's saved cookie. Try logging in again.", null);

            string? csrf = await RobloxAuthApi.GetCsrfTokenAsync(cookie).ConfigureAwait(false);
            var ticketResult = await RobloxAuthApi.GetAuthTicketAsync(cookie, csrf).ConfigureAwait(false);

            if (!ticketResult.Ok || string.IsNullOrEmpty(ticketResult.Ticket))
                return new LaunchUriResult(false, ticketResult.Error ?? "Failed to get an auth ticket. The saved cookie may be expired.", null);

            string uri = RobloxAuthApi.BuildLaunchUri(ticketResult.Ticket);

            account.LastUsedUtc = DateTime.UtcNow;

            // Whichever entry point launched this account is the one to preselect next
            // start -- covers the account manager as well as the launch menu dropdown.
            App.Accounts.Prop.LastSelectedAccountId = account.Id;

            App.Accounts.Save();

            App.Logger.WriteLine(LOG_IDENT, $"Prepared launch URI for {account.Username}");
            return new LaunchUriResult(true, null, uri);
        }

        /// <summary>
        /// Resolves a fresh per-account launch URI without spawning anything -- used by
        /// the main Start flow so the launch still goes through Bootstrapper (update
        /// check, mods, and the loading screen) instead of skipping straight to Roblox.
        /// </summary>
        public static Task<LaunchUriResult> PrepareLaunchUriAsync(RobloxAccount account) => GetLaunchUriAsync(account);

        public static async Task<LaunchResult> LaunchAsync(RobloxAccount account, bool multiInstance)
        {
            const string LOG_IDENT = "AccountLauncher::LaunchAsync";

            var uriResult = await GetLaunchUriAsync(account).ConfigureAwait(false);
            if (!uriResult.Ok || string.IsNullOrEmpty(uriResult.Uri))
                return new LaunchResult(false, uriResult.Error);

            // No client on disk -- a fresh profile, or a reset that cleared the recorded
            // version. Spawning it directly is not an option, but the normal launch flow
            // downloads Roblox and then starts it with whatever arguments are set, so hand
            // the ticket to that instead of telling the user to go and install Roblox by
            // hand. This is the same route the launch menu's play button takes.
            if (!File.Exists(new RobloxPlayerData().ExecutablePath))
            {
                App.Logger.WriteLine(LOG_IDENT, "Roblox isn't installed, launching through the bootstrapper");

                App.LaunchSettings.RobloxLaunchArgs = uriResult.Uri;

                Application.Current.Dispatcher.Invoke(() => LaunchHandler.LaunchRoblox(LaunchMode.Player));

                return new LaunchResult(true, null);
            }

            // Always first: Roblox's tray resident holds the previous account's session
            // and the client singleton even though no window is open, so it would take
            // over this launch and come back up as its own account.
            CloseBackgroundPlayers();

            if (multiInstance)
            {
                App.Logger.WriteLine(LOG_IDENT, "Multi-instance launching enabled, preparing singleton bypass");
                MultiInstance.PrepareForLaunch();
            }
            else if (RobloxProcessDetector.IsPlayerRunning())
            {
                // Without multi-instance bypass, Roblox's own singleton check makes a new
                // launch just hand its args to whichever client is already running and
                // exit -- so the still-open client (signed in as whatever account it
                // started with) never sees this account's fresh auth ticket. Close it
                // first so the process we spawn below is the one that actually claims
                // the singleton and signs in as the requested account.
                App.Logger.WriteLine(LOG_IDENT, "Another account is already running, closing it before switching");
                CloseRunningPlayer();
            }

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = new RobloxPlayerData().ExecutablePath,
                    UseShellExecute = false
                };
                psi.ArgumentList.Add(uriResult.Uri);
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Spawn failed: {ex.Message}");
                return new LaunchResult(false, $"Failed to start Roblox: {ex.Message}");
            }

            App.Logger.WriteLine(LOG_IDENT, $"Launched Roblox for {account.Username}");
            return new LaunchResult(true, null);
        }

        /// <summary>
        /// Closes every live Roblox client. Needed before any account switch that isn't
        /// multi-instance: Roblox's singleton check makes a second launch hand its args
        /// to the client that's already running and exit, so the fresh auth ticket never
        /// gets redeemed and the old account stays signed in.
        /// </summary>
        public static void CloseRunningPlayer()
            => CloseAll(RobloxProcessDetector.GetLivePlayerProcesses(), "AccountLauncher::CloseRunningPlayer");

        /// <summary>
        /// Closes Roblox's tray-mode resident and any windowless leftovers. Closing the
        /// Roblox window doesn't end the process -- it drops to the systray still holding
        /// the account it was signed in as, and it's the process that Windows hands the
        /// next roblox-player launch to. It then restores its own session instead of
        /// redeeming our auth ticket, which is why a picked account could still come up
        /// as whichever one was open last. Safe alongside multi-instance: these have no
        /// window, so they aren't clients the user is playing on.
        /// </summary>
        public static void CloseBackgroundPlayers()
            => CloseAll(RobloxProcessDetector.GetBackgroundPlayerProcesses(), "AccountLauncher::CloseBackgroundPlayers");

        private static void CloseAll(List<Process> processes, string logIdent)
        {
            foreach (var process in processes)
            {
                try
                {
                    if (process.HasExited)
                        continue;

                    App.Logger.WriteLine(logIdent, $"Closing Roblox pid {process.Id}");

                    process.CloseMainWindow();
                    if (!process.WaitForExit(3000))
                        process.Kill(entireProcessTree: true);
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(logIdent, $"Failed to close pid {process.Id}: {ex.Message}");
                }
                finally
                {
                    process.Dispose();
                }
            }
        }
    }
}
