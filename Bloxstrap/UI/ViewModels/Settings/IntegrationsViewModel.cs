using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Xml.Linq;
using Claudestrap.Integrations;
using Claudestrap.RobloxInterfaces;
using Claudestrap.UI.Elements.ContextMenu;
using Wpf.Ui.Appearance;

namespace Claudestrap.UI.ViewModels.Settings
{
    public class IntegrationsViewModel : NotifyPropertyChangedViewModel
    {
        public ICommand AddIntegrationCommand => new RelayCommand(AddIntegration);
        public ICommand DeleteIntegrationCommand => new RelayCommand(DeleteIntegration);
        public ICommand BrowseIntegrationLocationCommand => new RelayCommand(BrowseIntegrationLocation);
        public ICommand OpenHistoryWindowCommand { get; }
        public ICommand MusicWindowCommand { get; }
        public ICommand ChatModeWindowCommand { get; }
        public ICommand RPCWindowCommand { get; }
        public ICommand AccountWindowCommand { get; }

        private readonly string _appStoragePath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        @"Roblox\LocalStorage\appStorage.json");
        private JsonObject _jsonData;
        private readonly ActivityWatcher _watcher;

        public IntegrationsViewModel(ActivityWatcher watcher)
        {
            _watcher = watcher;

            LoadSettings();

            OpenHistoryWindowCommand = new RelayCommand(OpenHistoryWindow);
            MusicWindowCommand = new RelayCommand(MusicPlayerWindow);
            ChatModeWindowCommand = new RelayCommand(ChatModeWindow);
            RPCWindowCommand = new RelayCommand(RPCUIWindow);
            AccountWindowCommand = new RelayCommand(AccountWindow);

            _ = LoadExecutorListAsync();
        }

        #region Executor Version Sync (WEAO + RDD)

        public ObservableCollection<WeaoAPI.Executor> AvailableExecutors { get; } = new();

        private bool _executorListLoading;
        public bool ExecutorListLoading
        {
            get => _executorListLoading;
            private set
            {
                if (_executorListLoading != value)
                {
                    _executorListLoading = value;
                    OnPropertyChanged(nameof(ExecutorListLoading));
                }
            }
        }

        private string _executorStatusText = "Loading executors...";
        public string ExecutorStatusText
        {
            get => _executorStatusText;
            private set
            {
                if (_executorStatusText != value)
                {
                    _executorStatusText = value;
                    OnPropertyChanged(nameof(ExecutorStatusText));
                }
            }
        }

        public bool ExecutorSyncEnabled
        {
            get => App.Settings.Prop.ExecutorSyncEnabled;
            set
            {
                if (App.Settings.Prop.ExecutorSyncEnabled == value)
                    return;

                App.Settings.Prop.ExecutorSyncEnabled = value;
                OnPropertyChanged(nameof(ExecutorSyncEnabled));
                _ = RefreshExecutorStatusAsync();
            }
        }

        public string SelectedExecutor
        {
            get => App.Settings.Prop.SelectedExecutor ?? string.Empty;
            set
            {
                var normalized = value ?? string.Empty;
                if (App.Settings.Prop.SelectedExecutor == normalized)
                    return;

                App.Settings.Prop.SelectedExecutor = normalized;
                OnPropertyChanged(nameof(SelectedExecutor));
                _ = RefreshExecutorStatusAsync();
            }
        }

        public ICommand RefreshExecutorsCommand =>
            new RelayCommand(() => _ = LoadExecutorListAsync(forceRefresh: true));

        private async Task LoadExecutorListAsync(bool forceRefresh = false)
        {
            ExecutorListLoading = true;
            try
            {
                var executors = await WeaoAPI.GetExecutorsAsync(forceRefresh).ConfigureAwait(true);

                AvailableExecutors.Clear();
                foreach (var executor in executors)
                    AvailableExecutors.Add(executor);

                await RefreshExecutorStatusAsync();
            }
            catch
            {
                ExecutorStatusText = "Could not reach weao.xyz.";
            }
            finally
            {
                ExecutorListLoading = false;
            }
        }

        private async Task RefreshExecutorStatusAsync()
        {
            if (!App.Settings.Prop.ExecutorSyncEnabled)
            {
                ExecutorStatusText = "Sync disabled — using live Roblox version.";
                return;
            }

            var title = App.Settings.Prop.SelectedExecutor;
            if (string.IsNullOrWhiteSpace(title))
            {
                ExecutorStatusText = "Sync enabled — pick an executor.";
                return;
            }

            var executor = await WeaoAPI.FindExecutorAsync(title).ConfigureAwait(true);
            if (executor == null)
            {
                ExecutorStatusText = $"'{title}' isn't on the executor list.";
                return;
            }

            // "Updated" = executor works on the newest Roblox version.
            // "Downgrade" = executor targets the previous Roblox version.
            var supportLabel = executor.UpdateStatus ? "Updated (works for the newest)" : "Downgrade (works for the last)";
            ExecutorStatusText =
                $"{executor.Title} → {executor.RbxVersion} — {supportLabel}. " +
                $"Claudestrap will install this Roblox version on every launch.";
        }

        #endregion

        private void AddIntegration()
        {
            CustomIntegrations.Add(new CustomIntegration()
            {
                Name = Strings.Menu_Integrations_Custom_NewIntegration
            });

            SelectedCustomIntegrationIndex = CustomIntegrations.Count - 1;

            OnPropertyChanged(nameof(SelectedCustomIntegrationIndex));
            OnPropertyChanged(nameof(IsCustomIntegrationSelected));
        }

        private bool _robloxSystemTray;
        public bool RobloxSystemTray
        {
            get => _robloxSystemTray;
            set
            {
                if (_robloxSystemTray != value)
                {
                    _robloxSystemTray = value;
                    OnPropertyChanged(nameof(RobloxSystemTray));
                    SaveSetting("MinimizeToTray", value);
                }
            }
        }

        private bool _launchStartup;
        public bool LaunchStartup
        {
            get => _launchStartup;
            set
            {
                if (_launchStartup != value)
                {
                    _launchStartup = value;
                    OnPropertyChanged(nameof(LaunchStartup));
                    SaveSetting("LaunchAtStartup", value);
                }
            }
        }

        private void LoadSettings()
        {
            if (!File.Exists(_appStoragePath))
                return;

            var jsonText = File.ReadAllText(_appStoragePath);
            _jsonData = JsonNode.Parse(jsonText)?.AsObject();

            if (_jsonData == null)
                return;

            RobloxSystemTray = GetBool("MinimizeToTray");
            LaunchStartup = GetBool("LaunchAtStartup");
        }

        private bool GetBool(string key)
        {
            if (_jsonData[key] == null)
                return false;

            return bool.TryParse(_jsonData[key]?.ToString(), out bool result) && result;
        }

        private void SaveSetting(string key, bool value)
        {
            if (_jsonData == null)
                _jsonData = new JsonObject();

            _jsonData[key] = value.ToString().ToLower();

            Directory.CreateDirectory(Path.GetDirectoryName(_appStoragePath)!);
            File.WriteAllText(_appStoragePath,
                _jsonData.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        }

        private void OpenHistoryWindow()
        {
            var historyWindow = new ServerHistory(_watcher);
            historyWindow.Show();
        }

        private void ChatModeWindow()
        {
            var historyWindow = new DiscordChatWindow();
            historyWindow.Show();
        }

        private void MusicPlayerWindow()
        {
            var musicPlayerWindow = new MusicPlayer(_watcher);
            musicPlayerWindow.Show();
        }

        private void RPCUIWindow()
        {
            var rpcUIWindow = new RPCWindow();
            rpcUIWindow.Show();
        }

        private void DeleteIntegration()
        {
            if (SelectedCustomIntegration is null)
                return;

            CustomIntegrations.Remove(SelectedCustomIntegration);

            if (CustomIntegrations.Count > 0)
            {
                SelectedCustomIntegrationIndex = CustomIntegrations.Count - 1;
                OnPropertyChanged(nameof(SelectedCustomIntegrationIndex));
            }

            OnPropertyChanged(nameof(IsCustomIntegrationSelected));
        }

        private void BrowseIntegrationLocation()
        {
            if (SelectedCustomIntegration is null)
                return;

            var dialog = new OpenFileDialog
            {
                Filter = $"{Strings.Menu_AllFiles}|*.*"
            };

            if (dialog.ShowDialog() != true)
                return;

            SelectedCustomIntegration.Name = dialog.SafeFileName;
            SelectedCustomIntegration.Location = dialog.FileName;
            OnPropertyChanged(nameof(SelectedCustomIntegration));
        }

        public bool ActivityTrackingEnabled
        {
            get => App.Settings.Prop.EnableActivityTracking;

            set
            {
                App.Settings.Prop.EnableActivityTracking = value;

                if (!value)
                {
                    ShowServerDetailsEnabled = value;
                    DisableAppPatchEnabled = value;
                    DiscordActivityEnabled = value;
                    DiscordActivityJoinEnabled = value;

                    OnPropertyChanged(nameof(ShowServerDetailsEnabled));
                    OnPropertyChanged(nameof(DisableAppPatchEnabled));
                    OnPropertyChanged(nameof(DiscordActivityEnabled));
                    OnPropertyChanged(nameof(DiscordActivityJoinEnabled));
                }
            }
        }

        public bool ShowServerDetailsEnabled
        {
            get => App.Settings.Prop.ShowServerDetails;
            set => App.Settings.Prop.ShowServerDetails = value;
        }

        public bool IngameChatDiscord
        {
            get => App.Settings.Prop.IngameChatDiscord;
            set => App.Settings.Prop.IngameChatDiscord = value;
        }

        public bool joinGameNotify
        {
            get => App.Settings.Prop.NotificationWindowShow;
            set => App.Settings.Prop.NotificationWindowShow = value;
        }

        public bool exitondissy
        {
            get => App.Settings.Prop.exitondissy;
            set => App.Settings.Prop.exitondissy = value;
        }

        public string gamename
        {
            get => App.Settings.Prop.CustomGameName;
            set => App.Settings.Prop.CustomGameName = value;
        }

        public bool GameWIP
        {
            get => App.Settings.Prop.GameWIP;
            set => App.Settings.Prop.GameWIP = value;
        }

        public bool FFlagAmountRPC
        {
            get => App.Settings.Prop.FFlagRPCDisplayer;
            set => App.Settings.Prop.FFlagRPCDisplayer = value;
        }

        public bool ServerUptimeBetterBLOXcuzitsbetterXD
        {
            get => App.Settings.Prop.ServerUptimeBetterBLOXcuzitsbetterXD;
            set => App.Settings.Prop.ServerUptimeBetterBLOXcuzitsbetterXD = value;
        }

        private void AccountWindow()
        {
            var accountWindow = new AccountManagerWindow();
            accountWindow.Show();
        }

        public string gameimage
        {
            get => App.Settings.Prop.UseCustomIcon;
            set => App.Settings.Prop.UseCustomIcon = value;
        }

        public bool PlayerLogsEnabled
        {
            get => App.FastFlags.GetPreset("Players.LogLevel") == "trace";
            set
            {
                App.FastFlags.SetPreset("Players.LogLevel", value ? "trace" : null);
                App.FastFlags.SetPreset("Players.LogPattern", value ? "ExpChat/mountClientApp" : null);
            }
        }

        public bool DiscordActivityEnabled
        {
            get => App.Settings.Prop.UseDiscordRichPresence;
            set
            {
                App.Settings.Prop.UseDiscordRichPresence = value;

                if (!value)
                {
                    DiscordActivityJoinEnabled = value;
                    DiscordAccountOnProfile = value;
                    GameIconChecked = value;
                    ServerLocationGame = value;
                    OnPropertyChanged(nameof(DiscordActivityJoinEnabled));
                    OnPropertyChanged(nameof(DiscordAccountOnProfile));
                    OnPropertyChanged(nameof(GameIconChecked));
                    OnPropertyChanged(nameof(ServerLocationGame));
                }
            }
        }

        public bool UncapFPS
        {
            get => RobloxSettings.IsUncapped();
            set => RobloxSettings.SetUncapped(value);
        }

        public bool StartInDarkMode
        {
            get => RobloxSettings.IsDarkMode();
            set => RobloxSettings.SetDarkMode(value);
        }

        public bool DiscordActivityJoinEnabled
        {
            get => !App.Settings.Prop.HideRPCButtons;
            set => App.Settings.Prop.HideRPCButtons = !value;
        }

        public bool DiscordAccountOnProfile
        {
            get => App.Settings.Prop.ShowAccountOnRichPresence;
            set => App.Settings.Prop.ShowAccountOnRichPresence = value;
        }

        public bool GameIconChecked
        {
            get => App.Settings.Prop.GameIconChecked;
            set => App.Settings.Prop.GameIconChecked = value;
        }
        public bool GameNameChecked
        {
            get => App.Settings.Prop.GameNameChecked;
            set => App.Settings.Prop.GameNameChecked = value;
        }

        public bool GameCreatorChecked
        {
            get => App.Settings.Prop.GameCreatorChecked;
            set => App.Settings.Prop.GameCreatorChecked = value;
        }
        public bool GameStatusChecked
        {
            get => App.Settings.Prop.GameStatusChecked;
            set => App.Settings.Prop.GameStatusChecked = value;
        }

        public bool ServerLocationGame
        {
            get => App.Settings.Prop.ServerLocationGame;
            set => App.Settings.Prop.ServerLocationGame = value;
        }

        public bool DisableAppPatchEnabled
        {
            get => App.Settings.Prop.UseDisableAppPatch;
            set => App.Settings.Prop.UseDisableAppPatch = value;
        }

        public ObservableCollection<CustomIntegration> CustomIntegrations
        {
            get => App.Settings.Prop.CustomIntegrations;
            set => App.Settings.Prop.CustomIntegrations = value;
        }

        public CustomIntegration? SelectedCustomIntegration { get; set; }
        public int SelectedCustomIntegrationIndex { get; set; }
        public bool IsCustomIntegrationSelected => SelectedCustomIntegration is not null;
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}