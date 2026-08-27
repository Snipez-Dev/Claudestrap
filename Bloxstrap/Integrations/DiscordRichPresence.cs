using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using DiscordRPC;
using Claudestrap.Models.RobloxApi;
using Claudestrap.Models.ClaudestrapRPC;

namespace Claudestrap.Integrations
{
    public class DiscordRichPresence : IDisposable
    {
        private readonly DiscordRpcClient _rpcClient = new(
            string.IsNullOrWhiteSpace(App.Settings.Prop.DiscordRpcAppId)
                ? "1527407497311158522"
                : App.Settings.Prop.DiscordRpcAppId);
        private readonly ActivityWatcher _activityWatcher;
        private readonly ConcurrentQueue<Message> _messageQueue = new();
        private readonly SemaphoreSlim _updateLock = new(1, 1);

        private DiscordRPC.RichPresence? _currentPresence;
        private DiscordRPC.RichPresence? _originalPresence;

        private bool _visible = true;
        private long? _previousPlaceId;
        private DateTime _lastPresenceUpdate = DateTime.MinValue;
        private readonly TimeSpan _updateCooldown = TimeSpan.FromSeconds(5);

        public DiscordRichPresence(ActivityWatcher activityWatcher)
        {
            const string LOG_IDENT = "DiscordRichPresence";
            _activityWatcher = activityWatcher;

            _activityWatcher.OnGameJoin += async (_, _) => await SetCurrentGameAsync();
            _activityWatcher.OnGameLeave += async (_, _) => await SetCurrentGameAsync();
            _activityWatcher.OnRPCMessage += (_, message) => ProcessRPCMessage(message);

            _rpcClient.OnReady += (_, e) =>
                App.Logger.WriteLine(LOG_IDENT, $"Ready: {e.User} ({e.User.ID})");

            _rpcClient.OnPresenceUpdate += (_, _) =>
                App.Logger.WriteLine(LOG_IDENT, "Presence updated");

            _rpcClient.OnError += (_, e) =>
                App.Logger.WriteLine(LOG_IDENT, $"RPC Error: {e.Message}");

            _rpcClient.OnConnectionEstablished += (_, _) =>
                App.Logger.WriteLine(LOG_IDENT, "Connected to Discord RPC");

            _rpcClient.OnClose += (_, e) =>
            {
                App.Logger.WriteLine(LOG_IDENT, $"Connection closed: {e.Reason} ({e.Code})");
                Task.Run(async () =>
                {
                    await Task.Delay(3000);
                    try
                    {
                        _rpcClient.Initialize();
                        App.Logger.WriteLine(LOG_IDENT, "Reinitialized Discord RPC after closure.");
                    }
                    catch (Exception ex)
                    {
                        App.Logger.WriteLine(LOG_IDENT, $"Failed to reinitialize RPC: {ex.Message}");
                    }
                });
            };

            _rpcClient.Initialize();
        }

        public void ProcessRPCMessage(Message message, bool implicitUpdate = true)
        {
            if (message.Command != "SetRichPresence" && message.Command != "SetLaunchData") return;

            if (_currentPresence is null || _originalPresence is null)
            {
                _messageQueue.Enqueue(message);
                return;
            }

            if (message.Command == "SetLaunchData")
            {
                _currentPresence.Buttons = GetButtons();
            }
            else if (message.Command == "SetRichPresence")
            {
                if (!TryDeserializePresence(message.Data, out Claudestrap.Models.ClaudestrapRPC.RichPresence? presenceData))
                    return;

                _currentPresence.Details = UpdateField(_currentPresence.Details, presenceData.Details, _originalPresence.Details, 128);
                _currentPresence.State = UpdateField(_currentPresence.State, presenceData.State, _originalPresence.State, 128);

                UpdateAssets(_currentPresence.Assets, _originalPresence.Assets, presenceData.SmallImage, true);
                UpdateAssets(_currentPresence.Assets, _originalPresence.Assets, presenceData.LargeImage, false);
            }

            if (implicitUpdate)
                UpdatePresence();
        }

        private static bool TryDeserializePresence(JsonElement data, out Claudestrap.Models.ClaudestrapRPC.RichPresence? presence)
        {
            try
            {
                presence = data.Deserialize<Claudestrap.Models.ClaudestrapRPC.RichPresence>();
                return presence != null;
            }
            catch
            {
                presence = null;
                return false;
            }
        }

        private static string? UpdateField(string? current, string? newValue, string? original, int maxLength)
        {
            if (string.IsNullOrEmpty(newValue)) return current;
            if (newValue == "<reset>") return original;
            if (newValue.Length > maxLength) return current;
            return newValue;
        }

        private void UpdateAssets(Assets current, Assets original, RichPresenceImage? data, bool small)
        {
            if (data == null) return;

            if (data.Clear)
            {
                if (small) current.SmallImageKey = "";
                else current.LargeImageKey = "";
                return;
            }

            if (data.Reset)
            {
                if (small)
                {
                    current.SmallImageKey = original.SmallImageKey;
                    current.SmallImageText = original.SmallImageText;
                }
                else
                {
                    current.LargeImageKey = original.LargeImageKey;
                    current.LargeImageText = original.LargeImageText;
                }
                return;
            }

            if (!string.IsNullOrEmpty(data.CustomKey))
            {
                if (small) current.SmallImageKey = data.CustomKey;
                else current.LargeImageKey = data.CustomKey;
                return;
            }

            if (data.AssetId.HasValue)
            {
                var url = $"https://assetdelivery.roblox.com/v1/asset/?id={data.AssetId.Value}";
                if (small) current.SmallImageKey = url;
                else current.LargeImageKey = url;
            }

            if (!string.IsNullOrEmpty(data.HoverText))
            {
                if (small) current.SmallImageText = data.HoverText;
                else current.LargeImageText = data.HoverText;
            }
        }

        public void SetVisibility(bool visible)
        {
            _visible = visible;
            if (_visible) UpdatePresence();
            else _rpcClient.ClearPresence();
        }

        private async Task SetCurrentGameAsync()
        {
            if (!await _updateLock.WaitAsync(0)) return;
            try
            {
                bool changed = await SetCurrentGame();
                if (changed) RobloxMemoryCleaner.CleanRobloxMemory();
            }
            finally
            {
                _updateLock.Release();
            }
        }

        private int LoadFlags()
        {
            try
            {
                string modsPath = Path.Combine(Paths.Mods, "ClientSettings");
                string settingsFile = Path.Combine(modsPath, "ClientAppSettings.json");

                if (!File.Exists(settingsFile))
                {
                    return 0;
                }

                string json = File.ReadAllText(settingsFile);

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json, options);
                int totalFlags = dict?.Count ?? 0;

                return totalFlags;
            }
            catch (Exception ex)
            {
                return -1;
            }
        }

        public async Task<bool> SetCurrentGame()
        {
            const string LOG_IDENT = "DiscordRichPresence";

            if (!_activityWatcher.InGame)
            {
                _currentPresence = _originalPresence = null;
                _messageQueue.Clear();
                _previousPlaceId = null;
                UpdatePresence();
                App.Logger.WriteLine(LOG_IDENT, "Not in game, cleared presence.");
                return true;
            }

            var activity = _activityWatcher.Data;
            var timeStarted = activity.RootActivity?.TimeJoined ?? activity.TimeJoined;
            var placeId = activity.PlaceId;
            bool teleported = _previousPlaceId.HasValue && _previousPlaceId.Value != placeId;
            _previousPlaceId = placeId;

            int totalFlags = 0;

            if (App.Settings.Prop.FFlagRPCDisplayer)
            {
                totalFlags = LoadFlags();
            }

            if (activity.UniverseDetails is null)
            {
                try
                {
                    await UniverseDetails.FetchSingle(activity.UniverseId);
                    activity.UniverseDetails = UniverseDetails.LoadFromCache(activity.UniverseId);
                }
                catch (Exception ex)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Failed to fetch universe details: {ex.Message}");
                    return false;
                }
            }

            var universe = activity.UniverseDetails!;
            var (smallImage, smallText) = await GetSmallImageAsync(activity);

            string serverPrivacy = activity.ServerType switch
            {
                ServerType.Private => "Private Server",
                ServerType.Reserved => "Reserved Server",
                _ => "Public Server"
            };

            var (cleanName, betaTag) = ExtractBetaTag(universe.Data.Name, universe.Data.Description);
            string universeName = !string.IsNullOrWhiteSpace(App.Settings.Prop.CustomGameName)
                ? App.Settings.Prop.CustomGameName!
                : cleanName.Length < 2 ? cleanName + "\x2800\x2800\x2800" : cleanName;

            if (teleported)
                universeName = $"Teleported to {universeName}";

            string serverLocation = string.Empty;
            if (App.Settings.Prop.ServerLocationGame)
            {
                try { serverLocation = await activity.QueryServerLocation(); }
                catch { serverLocation = "Unknown Location"; }
            }

            var detailsParts = new List<string>();
            if (App.Settings.Prop.GameNameChecked) detailsParts.Add(universeName);
            if (App.Settings.Prop.GameStatusChecked) detailsParts.Add(serverPrivacy);
            if (App.Settings.Prop.ServerLocationGame) detailsParts.Add(serverLocation);
            string details = string.Join(" • ", detailsParts);
            if (!string.IsNullOrEmpty(betaTag))
                details = $"{details} {betaTag}";

            string state = App.Settings.Prop.GameCreatorChecked
                ? $"by {universe.Data.Creator.Name}{(universe.Data.Creator.HasVerifiedBadge ? " ☑️" : "")}"
                : "";

            if (App.Settings.Prop.FFlagRPCDisplayer)
            {
                state = string.IsNullOrWhiteSpace(state)
                    ? $"FFlags: {totalFlags}"
                    : $"{state} • FFlags: {totalFlags}";
            }

            string largeImageKey = !string.IsNullOrWhiteSpace(App.Settings.Prop.UseCustomIcon)
                ? App.Settings.Prop.UseCustomIcon
                : (App.Settings.Prop.GameIconChecked ? universe.Thumbnail.ImageUrl : "");

            // Large Image Text: Eigener Text hat Vorrang, sonst Spielname
            string largeImageText = !string.IsNullOrWhiteSpace(App.Settings.Prop.RpcCustomLargeImageText)
                ? App.Settings.Prop.RpcCustomLargeImageText
                : (!string.IsNullOrWhiteSpace(App.Settings.Prop.UseCustomIcon)
                    ? ""
                    : (App.Settings.Prop.GameIconChecked && App.Settings.Prop.GameNameChecked ? universe.Data.Name : ""));

            // Timestamps nur wenn aktiviert
            Timestamps? timestamps = App.Settings.Prop.RpcShowTimestamp
                ? new Timestamps { Start = timeStarted.ToUniversalTime() }
                : null;

            if (_currentPresence != null)
            {
                _currentPresence.Details = details;
                _currentPresence.State = state;
                _currentPresence.Assets.LargeImageKey = largeImageKey;
                _currentPresence.Assets.LargeImageText = largeImageText;
                _currentPresence.Assets.SmallImageKey = smallImage;
                _currentPresence.Assets.SmallImageText = smallText;
                _currentPresence.Buttons = GetButtons();
                _currentPresence.Timestamps = timestamps;
            }
            else
            {
                _currentPresence = new DiscordRPC.RichPresence
                {
                    Details = details,
                    State = state,
                    Timestamps = timestamps,
                    Buttons = GetButtons(),
                    Assets = new Assets
                    {
                        LargeImageKey = largeImageKey,
                        LargeImageText = largeImageText,
                        SmallImageKey = smallImage,
                        SmallImageText = smallText
                    }
                };
                _originalPresence = _currentPresence;
            }

            while (_messageQueue.TryDequeue(out var msg))
                ProcessRPCMessage(msg, false);

            UpdatePresence();
            App.Logger.WriteLine(LOG_IDENT, $"Updated presence for {details} with FFlags: {totalFlags}");
            return true;
        }

        private static (string CleanName, string? Tag) ExtractBetaTag(string gameName, string? description)
        {
            if (string.IsNullOrWhiteSpace(gameName))
                return (gameName ?? "", null);

            if (!App.Settings.Prop.GameWIP)
                return (gameName, null);

            string combined = (gameName + " " + (description ?? "")).ToUpperInvariant();
            var keywords = new Dictionary<string, string>
    {
        { "BETA", "[BETA]" },
        { "TESTING", "[TESTING]" },
        { "IN WORKS", "[IN WORKS]" },
        { "ALPHA", "[ALPHA]" },
        { "PREVIEW", "[PREVIEW]" },
    };

            foreach (var kvp in keywords)
            {
                string pattern = @"[\[\{]?\s*" + Regex.Escape(kvp.Key) + @"\s*[\]\}]?";
                if (Regex.IsMatch(combined, pattern, RegexOptions.IgnoreCase))
                {
                    string cleanName = Regex.Replace(gameName, pattern, "", RegexOptions.IgnoreCase).Trim();
                    return (string.IsNullOrEmpty(cleanName) ? gameName : cleanName, kvp.Value);
                }
            }

            return (gameName, null);
        }

        private async Task<(string key, string text)> GetSmallImageAsync(ActivityData activity)
        {
            // Eigener Small-Image-Text aus den Einstellungen
            string customText = App.Settings.Prop.RpcCustomSmallImageText;

            if (!App.Settings.Prop.ShowAccountOnRichPresence)
            {
                string text = string.IsNullOrWhiteSpace(customText) ? "Claudestrap" : customText;
                return ("claudestrap", text);
            }

            try
            {
                var user = await UserDetails.Fetch(activity.UserId);
                string displayText = string.IsNullOrWhiteSpace(customText)
                    ? $"{user.Data.DisplayName} (@{user.Data.Name})"
                    : customText;
                return (user.Thumbnail.ImageUrl, displayText);
            }
            catch
            {
                string text = string.IsNullOrWhiteSpace(customText) ? "Claudestrap" : customText;
                return ("claudestrap", text);
            }
        }

        public Button[] GetButtons()
        {
            var data = _activityWatcher.Data;
            var buttons = new List<Button>();

            // Join-Server-Button
            if (!App.Settings.Prop.HideRPCButtons && App.Settings.Prop.RpcShowJoinButton)
            {
                string? inviteUrl = null;
                if (data.ServerType == ServerType.Public ||
                    (data.ServerType == ServerType.Reserved && !string.IsNullOrEmpty(data.RPCLaunchData)))
                {
                    inviteUrl = data.GetInviteDeeplink();
                }

                if (!string.IsNullOrEmpty(inviteUrl))
                {
                    string joinLabel = string.IsNullOrWhiteSpace(App.Settings.Prop.RpcJoinButtonLabel)
                        ? "Join server"
                        : App.Settings.Prop.RpcJoinButtonLabel;
                    buttons.Add(new Button { Label = joinLabel, Url = inviteUrl });
                }
            }

            // Game-Page-Button
            if (App.Settings.Prop.RpcShowGamePageButton)
            {
                string gamePageLabel = string.IsNullOrWhiteSpace(App.Settings.Prop.RpcGamePageButtonLabel)
                    ? "Game Page"
                    : App.Settings.Prop.RpcGamePageButtonLabel;
                buttons.Add(new Button { Label = gamePageLabel, Url = $"https://www.roblox.com/games/{data.PlaceId}" });
            }

            return buttons.ToArray();
        }

        public void UpdatePresence()
        {
            if (_currentPresence is null)
            {
                _rpcClient.ClearPresence();
                return;
            }

            if (!_visible) return;
            if ((DateTime.UtcNow - _lastPresenceUpdate) < _updateCooldown) return;

            _lastPresenceUpdate = DateTime.UtcNow;
            _rpcClient.SetPresence(_currentPresence);
        }

        public void Dispose()
        {
            _rpcClient.ClearPresence();
            _rpcClient.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    public static class RobloxMemoryCleaner
    {
        public static void CleanRobloxMemory()
        {
            var robloxProcesses = Process.GetProcessesByName("Roblox")
                .Concat(Process.GetProcessesByName("RobloxPlayerBeta"));

            foreach (var proc in robloxProcesses)
            {
                try
                {
                    proc.Refresh();
                    if (!proc.HasExited)
                        EmptyWorkingSet(proc.Handle);
                }
                catch { }
            }
        }

        [System.Runtime.InteropServices.DllImport("psapi.dll")]
        private static extern bool EmptyWorkingSet(IntPtr hProcess);
    }
}
