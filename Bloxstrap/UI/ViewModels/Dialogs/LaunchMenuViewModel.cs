using System;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

using Claudestrap.Integrations;
using Claudestrap.Models.Entities;
using Claudestrap.UI.Elements.About;
using Claudestrap.UI.Elements.Dialogs;
using Claudestrap.Utility;

namespace Claudestrap.UI.ViewModels.Installer
{
    /// <summary>One entry in the bottom-right account dropdown -- either a saved
    /// account, or the trailing "+ Add Account" action row (Model is null).</summary>
    public sealed class AccountDropdownItem : INotifyPropertyChanged
    {
        public RobloxAccount? Model { get; }

        public bool IsAddAccountOption => Model is null;

        public string DisplayName => Model is null
            ? "+ Add Account"
            : (string.IsNullOrEmpty(Model.Username) ? "Unknown" : Model.Username);

        private bool _isLoggedOut;
        /// <summary>True once a background check finds this account's saved cookie no
        /// longer authenticates -- drives the relogin icon in the dropdown row. Starts
        /// false (unknown/assumed good) until <see cref="LaunchMenuViewModel.RefreshLoginStatusAsync"/> resolves.</summary>
        public bool IsLoggedOut
        {
            get => _isLoggedOut;
            set
            {
                if (_isLoggedOut == value)
                    return;

                _isLoggedOut = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLoggedOut)));
            }
        }

        public AccountDropdownItem(RobloxAccount? model) => Model = model;

        public event PropertyChangedEventHandler? PropertyChanged;

        // ComboBox.SelectionBoxItem falls back to the raw item's ToString() rather
        // than DisplayMemberPath once it's rendered through a custom ControlTemplate
        // -- without this override the closed dropdown showed the class's namespace
        // instead of the account name.
        public override string ToString() => DisplayName;
    }

    public class LaunchMenuViewModel : INotifyPropertyChanged
    {
        public string Version => string.Format(Strings.Menu_About_Version, App.Version);

        /// <summary>Short version pill text — "v1.1.1.1".</summary>
        public string VersionShort => "v" + App.Version;

        /// <summary>Installed Roblox version guid, or "no build installed" when none yet.</summary>
        public string RobloxVersionCommit
        {
            get
            {
                var guid = App.State?.Prop?.Player?.VersionGuid;
                if (string.IsNullOrWhiteSpace(guid))
                    guid = App.State?.Prop?.Studio?.VersionGuid;
                return string.IsNullOrWhiteSpace(guid) ? "no build installed" : guid;
            }
        }

        public Visibility RobloxStudioOptionVisibility => App.IsStudioVisible ? Visibility.Visible : Visibility.Collapsed;

        /// <summary>Saved accounts, always followed by a trailing "+ Add Account" row —
        /// picking an account here and pressing Launch skips the account manager
        /// entirely for the common case.</summary>
        public ObservableCollection<AccountDropdownItem> AccountOptions { get; } = new();

        private AccountDropdownItem? _selectedAccountOption;
        public AccountDropdownItem? SelectedAccountOption
        {
            get => _selectedAccountOption;
            set
            {
                if (value is null || value == _selectedAccountOption)
                    return;

                if (value.IsAddAccountOption)
                {
                    // Action row, not a real selection -- fire the add-account flow and
                    // snap the dropdown back to whatever was actually selected before.
                    OnPropertyChanged(nameof(SelectedAccountOption));
                    _ = AddAccountAsync();
                    return;
                }

                _selectedAccountOption = value;
                RememberSelection(value.Model);
                OnPropertyChanged(nameof(SelectedAccountOption));
            }
        }

        private bool _isLaunching;
        public bool IsLaunching
        {
            get => _isLaunching;
            set { _isLaunching = value; OnPropertyChanged(nameof(IsLaunching)); }
        }

        // Cached rather than allocated per-access: WPF re-reads an ICommand-bound
        // property far more often than once (window activation, focus changes, etc.),
        // and each expression-bodied `=> new RelayCommand(...)` getter used to hand
        // back a fresh instance every time.
        public ICommand LaunchSettingsCommand { get; }

        public ICommand LaunchRobloxCommand { get; }

        public ICommand LaunchRobloxStudioCommand { get; }

        public ICommand LaunchAboutCommand { get; }

        public ICommand CleanTracesCommand { get; }

        public ICommand DeleteAccountCommand { get; }

        public ICommand RelinkAccountCommand { get; }

        /// <summary>Wipes Roblox trace artifacts (logs, http cache, temp) on demand.</summary>
        private void CleanTraces()
        {
            var result = Frontend.ShowMessageBox(
                "Clean Roblox traces (logs, cache, crash dumps)?\nClose Roblox first so files are not locked.",
                MessageBoxImage.Question,
                MessageBoxButton.OKCancel,
                MessageBoxResult.Cancel);

            if (result != MessageBoxResult.OK)
                return;

            try
            {
                int count = Cleaner.CleanRobloxTraces();
                Frontend.ShowMessageBox(
                    count > 0
                        ? $"Cleaned {count} Roblox trace files."
                        : "Nothing to clean — no Roblox traces found.",
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Frontend.ShowMessageBox($"Failed to clean Roblox traces:\n{ex.Message}", MessageBoxImage.Error);
            }
        }

        public event EventHandler<NextAction>? CloseWindowRequest;
        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public LaunchMenuViewModel()
        {
            LaunchSettingsCommand = new RelayCommand(LaunchSettings);
            LaunchRobloxCommand = new AsyncRelayCommand(LaunchRobloxAsync);
            LaunchRobloxStudioCommand = new RelayCommand(LaunchRobloxStudio);
            LaunchAboutCommand = new RelayCommand(LaunchAbout);
            CleanTracesCommand = new RelayCommand(CleanTraces);
            DeleteAccountCommand = new RelayCommand<AccountDropdownItem?>(DeleteAccount);
            RelinkAccountCommand = new AsyncRelayCommand<AccountDropdownItem?>(RelinkAccountAsync);

            ReloadAccountOptions();
        }

        private void ReloadAccountOptions(RobloxAccount? preferSelect = null)
        {
            AccountOptions.Clear();

            foreach (var account in App.Accounts.Prop.Accounts.OrderByDescending(a => a.LastUsedUtc ?? a.AddedUtc))
                AccountOptions.Add(new AccountDropdownItem(account));

            AccountOptions.Add(new AccountDropdownItem(null));

            // Nothing explicitly requested -- fall back to whatever was selected last
            // session so the dropdown reopens on the account the user actually uses.
            string? targetId = preferSelect?.Id ?? App.Accounts.Prop.LastSelectedAccountId;

            _selectedAccountOption = string.IsNullOrEmpty(targetId)
                ? null
                : AccountOptions.FirstOrDefault(o => o.Model?.Id == targetId);

            // The remembered account may have since been removed -- drop the stale id
            // rather than leaving it to fail the same lookup on every start.
            if (_selectedAccountOption is null && preferSelect is null && !string.IsNullOrEmpty(targetId))
                RememberSelection(null);
            else
                RememberSelection(_selectedAccountOption?.Model);

            OnPropertyChanged(nameof(SelectedAccountOption));

            _ = RefreshLoginStatusAsync();
        }

        /// <summary>Stops the background login check. The window calls this when it
        /// closes: each answer flips a dropdown item, and a flip after the window is gone
        /// invalidates hit-testing on a window that no longer has a presentation source,
        /// which WPF answers with a NullReferenceException from inside its own input
        /// plumbing -- a crash with no frame of ours anywhere in it.</summary>
        public void CancelBackgroundWork() => _backgroundWork.Cancel();

        private readonly CancellationTokenSource _backgroundWork = new();

        /// <summary>Checks each saved account's cookie against Roblox in the background
        /// and flips <see cref="AccountDropdownItem.IsLoggedOut"/> so the dropdown can
        /// surface a relogin icon for accounts whose session has expired.</summary>
        private async Task RefreshLoginStatusAsync()
        {
            // One request per account, and they don't depend on each other -- run them
            // together. Checked one after the other, the dropdown took as long as the
            // sum of every account's round trip to settle.
            var checks = AccountOptions
                .Where(o => o.Model is not null)
                .Select(CheckLoginAsync)
                .ToList();

            await Task.WhenAll(checks).ConfigureAwait(true);
        }

        private async Task CheckLoginAsync(AccountDropdownItem item)
        {
            if (_backgroundWork.IsCancellationRequested)
                return;

            string? cookie = AccountCookieProtector.Unprotect(item.Model!.EncryptedCookie);

            if (string.IsNullOrEmpty(cookie))
            {
                item.IsLoggedOut = true;
                return;
            }

            var info = await RobloxAuthApi.FetchUserInfoAsync(cookie).ConfigureAwait(true);

            // The answer can land long after the user has picked an account and the
            // window has closed.
            if (_backgroundWork.IsCancellationRequested)
                return;

            item.IsLoggedOut = !info.Ok;
        }

        private void DeleteAccount(AccountDropdownItem? item)
        {
            if (item?.Model is null)
                return;

            var result = Frontend.ShowMessageBox(
                $"Remove the saved account \"{item.DisplayName}\"?",
                MessageBoxImage.Question,
                MessageBoxButton.YesNo,
                MessageBoxResult.No);

            if (result != MessageBoxResult.Yes)
                return;

            App.Accounts.Prop.Accounts.Remove(item.Model);

            if (App.Accounts.Prop.LastSelectedAccountId == item.Model.Id)
                App.Accounts.Prop.LastSelectedAccountId = null;

            App.Accounts.Save();
            ReloadAccountOptions();
        }

        private async Task RelinkAccountAsync(AccountDropdownItem? item)
        {
            if (item?.Model is null)
                return;

            var dialog = new RobloxLoginDialog();
            var result = await dialog.ShowAndWaitAsync();

            if (!result.Ok || result.Cookie is null)
                return;

            item.Model.EncryptedCookie = AccountCookieProtector.Protect(result.Cookie);

            if (!string.IsNullOrEmpty(result.Username))
                item.Model.Username = result.Username;

            App.Accounts.Save();
            item.IsLoggedOut = false;
            ReloadAccountOptions(preferSelect: item.Model);
        }

        /// <summary>Persists the dropdown's current pick for the next start.</summary>
        private static void RememberSelection(RobloxAccount? account)
        {
            if (App.Accounts.Prop.LastSelectedAccountId == account?.Id)
                return;

            App.Accounts.Prop.LastSelectedAccountId = account?.Id;
            App.Accounts.Save();
        }

        private async Task AddAccountAsync()
        {
            var dialog = new RobloxLoginDialog();
            var result = await dialog.ShowAndWaitAsync();

            if (!result.Ok || result.Cookie is null)
                return;

            var existing = App.Accounts.Prop.Accounts.FirstOrDefault(a => a.UserId == result.UserId);
            if (existing is not null)
            {
                existing.EncryptedCookie = AccountCookieProtector.Protect(result.Cookie);
                existing.Username = result.Username ?? existing.Username;
            }
            else
            {
                existing = new RobloxAccount
                {
                    Username = result.Username ?? "Unknown",
                    UserId = result.UserId ?? "",
                    EncryptedCookie = AccountCookieProtector.Protect(result.Cookie)
                };
                App.Accounts.Prop.Accounts.Add(existing);
            }

            App.Accounts.Save();
            ReloadAccountOptions(preferSelect: existing);
        }

        private void LaunchSettings() => CloseWindowRequest?.Invoke(this, NextAction.LaunchSettings);

        private async Task LaunchRobloxAsync()
        {
            var selected = SelectedAccountOption;

            if (selected is not null && !selected.IsAddAccountOption && selected.Model is not null)
            {
                IsLaunching = true;

                var result = await AccountLauncher.PrepareLaunchUriAsync(selected.Model);

                IsLaunching = false;

                if (!result.Ok || string.IsNullOrEmpty(result.Uri))
                {
                    Frontend.ShowMessageBox(result.Error ?? "Failed to launch Roblox.", MessageBoxImage.Warning);
                    return;
                }

                // Route through the normal bootstrapper flow (update check, mods, loading
                // screen) instead of spawning Roblox directly -- it just launches with a
                // fresh ticket for the selected account instead of the default session.
                App.LaunchSettings.RobloxLaunchArgs = result.Uri;
            }

            CloseWindowRequest?.Invoke(this, NextAction.LaunchRoblox);
        }

        private void LaunchRobloxStudio() => CloseWindowRequest?.Invoke(this, NextAction.LaunchRobloxStudio);

        private void LaunchAbout() => new MainWindow().ShowDialog();
    }
}
