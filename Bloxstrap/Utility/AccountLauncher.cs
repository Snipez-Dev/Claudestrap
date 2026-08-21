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

            string exePath = new RobloxPlayerData().ExecutablePath;
            if (!File.Exists(exePath))
                return new LaunchUriResult(false, "Roblox isn't installed yet. Launch Roblox normally once first, then try again.", null);

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

            if (multiInstance)
            {
                App.Logger.WriteLine(LOG_IDENT, "Multi-instance launching enabled, preparing singleton bypass");
                MultiInstance.PrepareForLaunch();
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
    }
}
