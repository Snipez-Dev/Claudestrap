using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Claudestrap;
using Claudestrap.Models.Entities;
using Claudestrap.UI.Elements.Dialogs;
using Claudestrap.UI.ViewModels.ContextMenu;
using Claudestrap.Utility;

namespace Claudestrap.UI.ViewModels
{
    public sealed class SavedAccountItem : INotifyPropertyChanged
    {
        public RobloxAccount Model { get; }

        public SavedAccountItem(RobloxAccount model) => Model = model;

        public string Username => string.IsNullOrEmpty(Model.Username) ? "Unknown" : Model.Username;
        public string UserId => Model.UserId;
        public string LastUsedDisplay => Model.LastUsedUtc is { } t
            ? $"Last launched {t.ToLocalTime():g}"
            : $"Added {Model.AddedUtc.ToLocalTime():g}";

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(nameof(IsBusy)); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    /// <summary>
    /// Real multi-account manager: sign in through an embedded Roblox login page
    /// (username/password, passkeys, 2FA -- whatever Roblox itself asks for) or
    /// paste a .ROBLOSECURITY cookie directly, then launch any saved account on
    /// demand. Each launch fetches a fresh auth ticket for that specific account
    /// and spawns Roblox directly with it (see AccountLauncher/RobloxAuthApi) --
    /// unlike the older cookie-file swap approach, this never touches the shared
    /// RobloxCookies.dat, so it composes cleanly with multi-instance launching.
    /// </summary>
    public class AccountManagerViewModel : INotifyPropertyChanged
    {
        public AccountBackupsViewModel Backups { get; } = new();

        public ObservableCollection<SavedAccountItem> Accounts { get; } = new();

        private string _cookieInput = "";
        public string CookieInput
        {
            get => _cookieInput;
            set { _cookieInput = value; OnPropertyChanged(nameof(CookieInput)); }
        }

        private string _status = "Ready";
        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(nameof(Status)); }
        }

        public bool MultiInstanceLaunching => App.Settings.Prop.MultiInstanceLaunching;

        private string _lastUsedAccountDisplay = "";
        public string LastUsedAccountDisplay
        {
            get => _lastUsedAccountDisplay;
            set { _lastUsedAccountDisplay = value; OnPropertyChanged(nameof(LastUsedAccountDisplay)); }
        }

        public ICommand LoginCommand { get; }
        public ICommand AddCookieCommand { get; }
        public ICommand LaunchAccountCommand { get; }
        public ICommand RemoveAccountCommand { get; }

        public event PropertyChangedEventHandler? PropertyChanged;
        private void OnPropertyChanged(string n) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public AccountManagerViewModel()
        {
            LoginCommand = new RelayCommand(async _ => await LoginAsync());
            AddCookieCommand = new RelayCommand(async _ => await AddCookieAsync());
            LaunchAccountCommand = new RelayCommand(async param => await LaunchAsync(param as SavedAccountItem));
            RemoveAccountCommand = new RelayCommand(param => Remove(param as SavedAccountItem));

            LoadAccounts();
        }

        private void LoadAccounts()
        {
            Accounts.Clear();

            foreach (var account in App.Accounts.Prop.Accounts.OrderByDescending(a => a.LastUsedUtc ?? a.AddedUtc))
                Accounts.Add(new SavedAccountItem(account));

            RefreshLastUsedDisplay();
        }

        private void RefreshLastUsedDisplay()
        {
            var lastUsed = App.Accounts.Prop.Accounts
                .Where(a => a.LastUsedUtc.HasValue)
                .OrderByDescending(a => a.LastUsedUtc!.Value)
                .FirstOrDefault();

            LastUsedAccountDisplay = lastUsed is not null
                ? $"Last used: {lastUsed.Username}"
                : "No recent account";
        }

        private async Task LoginAsync()
        {
            Status = "Opening sign-in window...";

            var dialog = new RobloxLoginDialog();
            var result = await dialog.ShowAndWaitAsync();

            if (!result.Ok || result.Cookie is null)
            {
                Status = result.Error ?? "Sign-in cancelled.";
                return;
            }

            SaveAccount(result.Cookie, result.Username, result.UserId);
        }

        private async Task AddCookieAsync()
        {
            string cookie = CookieInput.Trim();
            if (string.IsNullOrEmpty(cookie))
            {
                Status = "Paste a .ROBLOSECURITY cookie first.";
                return;
            }

            Status = "Validating cookie...";
            var info = await RobloxAuthApi.FetchUserInfoAsync(cookie);

            if (!info.Ok)
            {
                Status = $"Invalid cookie: {info.Reason}";
                return;
            }

            CookieInput = "";
            SaveAccount(cookie, info.Username, info.UserId);
        }

        private void SaveAccount(string cookie, string? username, string? userId)
        {
            var existing = App.Accounts.Prop.Accounts.FirstOrDefault(a => a.UserId == userId);

            if (existing is not null)
            {
                existing.EncryptedCookie = AccountCookieProtector.Protect(cookie);
                existing.Username = username ?? existing.Username;
            }
            else
            {
                existing = new RobloxAccount
                {
                    Username = username ?? "Unknown",
                    UserId = userId ?? "",
                    EncryptedCookie = AccountCookieProtector.Protect(cookie)
                };
                App.Accounts.Prop.Accounts.Add(existing);
            }

            App.Accounts.Save();
            LoadAccounts();
            Status = $"Added {existing.Username}.";
        }

        private async Task LaunchAsync(SavedAccountItem? item)
        {
            if (item is null)
                return;

            item.IsBusy = true;
            Status = $"Launching {item.Username}...";

            var result = await AccountLauncher.LaunchAsync(item.Model, App.Settings.Prop.MultiInstanceLaunching);

            item.IsBusy = false;
            Status = result.Ok ? $"Launched {item.Username}." : $"Launch failed: {result.Error}";

            if (result.Ok)
            {
                LoadAccounts();
            }
        }

        private void Remove(SavedAccountItem? item)
        {
            if (item is null)
                return;

            App.Accounts.Prop.Accounts.Remove(item.Model);

            if (App.Accounts.Prop.LastSelectedAccountId == item.Model.Id)
                App.Accounts.Prop.LastSelectedAccountId = null;

            App.Accounts.Save();
            Accounts.Remove(item);
            Status = $"Removed {item.Username}.";
        }
    }
}
