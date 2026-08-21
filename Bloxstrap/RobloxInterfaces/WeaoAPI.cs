using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Claudestrap;

namespace Claudestrap.RobloxInterfaces
{
    /// <summary>
    /// Client for the WEAO (weao.xyz) public API.
    /// Exposes the current Roblox Windows version hash and the list of tracked
    /// executors along with the Roblox version each executor supports.
    /// Docs: https://docs.weao.xyz/weao-api-reference/roblox-versions
    /// </summary>
    public static class WeaoAPI
    {
        private const string UserAgent = "WEAO-3PService";
        private const string VersionsCurrentUrl = "https://weao.xyz/api/versions/current";
        private const string ExploitsUrl = "https://weao.xyz/api/status/exploits";

        private const string WindowsPlatform = "Windows";

        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

        private static readonly HttpClient Http = new()
        {
            Timeout = TimeSpan.FromSeconds(20)
        };

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private static readonly SemaphoreSlim ExecutorsLock = new(1, 1);
        private static readonly SemaphoreSlim VersionLock = new(1, 1);

        private static List<Executor>? _executorsCache;
        private static DateTime _executorsFetchedAt;

        private static CurrentVersions? _versionsCache;
        private static DateTime _versionsFetchedAt;

        static WeaoAPI()
        {
            Http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", UserAgent);
            Http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
        }

        public class Executor
        {
            [JsonPropertyName("title")]
            public string Title { get; set; } = string.Empty;

            [JsonPropertyName("version")]
            public string Version { get; set; } = string.Empty;

            [JsonPropertyName("rbxversion")]
            public string RbxVersion { get; set; } = string.Empty;

            [JsonPropertyName("updateStatus")]
            public bool UpdateStatus { get; set; }

            [JsonPropertyName("platform")]
            public string Platform { get; set; } = string.Empty;

            [JsonPropertyName("extype")]
            public string ExType { get; set; } = string.Empty;

            [JsonPropertyName("free")]
            public bool Free { get; set; }

            [JsonPropertyName("detected")]
            public bool Detected { get; set; }

            [JsonPropertyName("hidden")]
            public bool Hidden { get; set; }

            [JsonPropertyName("slug")]
            public SlugData? Slug { get; set; }

            /// <summary>
            /// Logo URL taken from the "slug.logo" field on the WEAO exploit entry.
            /// Empty when WEAO has no logo for this executor.
            /// </summary>
            public string LogoUrl => Slug?.Logo ?? string.Empty;

            /// <summary>
            /// Human-readable label describing whether this executor supports the
            /// current Roblox version ("Updated") or a previous one ("Downgrade").
            /// </summary>
            public string SupportLabel => UpdateStatus ? "Updated" : "Downgrade";
        }

        public class SlugData
        {
            [JsonPropertyName("logo")]
            public string Logo { get; set; } = string.Empty;
        }

        public class CurrentVersions
        {
            [JsonPropertyName("Windows")]
            public string Windows { get; set; } = string.Empty;

            [JsonPropertyName("WindowsDate")]
            public string WindowsDate { get; set; } = string.Empty;

            [JsonPropertyName("Mac")]
            public string Mac { get; set; } = string.Empty;

            [JsonPropertyName("MacDate")]
            public string MacDate { get; set; } = string.Empty;
        }

        /// <summary>
        /// Returns the current Roblox Windows version hash (e.g. "version-abc123..."),
        /// as tracked by WEAO. Cached for <see cref="CacheTtl"/>.
        /// </summary>
        public static async Task<CurrentVersions?> GetCurrentVersionsAsync(bool forceRefresh = false)
        {
            const string logIdent = "WeaoAPI::GetCurrentVersionsAsync";

            if (!forceRefresh && _versionsCache != null && (DateTime.UtcNow - _versionsFetchedAt) < CacheTtl)
                return _versionsCache;

            await VersionLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!forceRefresh && _versionsCache != null && (DateTime.UtcNow - _versionsFetchedAt) < CacheTtl)
                    return _versionsCache;

                using var resp = await Http.GetAsync(VersionsCurrentUrl).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    App.Logger.WriteLine(logIdent, $"HTTP {(int)resp.StatusCode} from {VersionsCurrentUrl}");
                    return _versionsCache;
                }

                var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                var parsed = JsonSerializer.Deserialize<CurrentVersions>(body, JsonOpts);
                if (parsed == null)
                    return _versionsCache;

                _versionsCache = parsed;
                _versionsFetchedAt = DateTime.UtcNow;
                return _versionsCache;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(logIdent, $"Failed: {ex.Message}");
                return _versionsCache;
            }
            finally
            {
                VersionLock.Release();
            }
        }

        /// <summary>
        /// Returns the tracked executor list from WEAO, filtered to Windows entries
        /// that expose a usable "rbxversion" hash. Cached for <see cref="CacheTtl"/>.
        /// </summary>
        public static async Task<IReadOnlyList<Executor>> GetExecutorsAsync(bool forceRefresh = false)
        {
            const string logIdent = "WeaoAPI::GetExecutorsAsync";

            if (!forceRefresh && _executorsCache != null && (DateTime.UtcNow - _executorsFetchedAt) < CacheTtl)
                return _executorsCache;

            await ExecutorsLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (!forceRefresh && _executorsCache != null && (DateTime.UtcNow - _executorsFetchedAt) < CacheTtl)
                    return _executorsCache;

                using var resp = await Http.GetAsync(ExploitsUrl).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    App.Logger.WriteLine(logIdent, $"HTTP {(int)resp.StatusCode} from {ExploitsUrl}");
                    return _executorsCache ?? new List<Executor>();
                }

                var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                var parsed = JsonSerializer.Deserialize<List<Executor>>(body, JsonOpts) ?? new List<Executor>();

                var filtered = parsed
                    .Where(e =>
                        !e.Hidden
                        && !string.IsNullOrWhiteSpace(e.Title)
                        && string.Equals(e.Platform, WindowsPlatform, StringComparison.OrdinalIgnoreCase)
                        && !string.IsNullOrWhiteSpace(e.RbxVersion)
                        && e.RbxVersion.StartsWith("version-", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(e => e.Title, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                _executorsCache = filtered;
                _executorsFetchedAt = DateTime.UtcNow;
                return _executorsCache;
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(logIdent, $"Failed: {ex.Message}");
                return _executorsCache ?? new List<Executor>();
            }
            finally
            {
                ExecutorsLock.Release();
            }
        }

        /// <summary>
        /// Look up a single executor by its case-insensitive title.
        /// </summary>
        public static async Task<Executor?> FindExecutorAsync(string title, bool forceRefresh = false)
        {
            if (string.IsNullOrWhiteSpace(title))
                return null;

            var list = await GetExecutorsAsync(forceRefresh).ConfigureAwait(false);
            return list.FirstOrDefault(e => string.Equals(e.Title, title, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Resolves the version hash ("version-XYZ") that the selected executor supports.
        /// Returns null when the executor cannot be found or has no usable version.
        /// </summary>
        public static async Task<string?> ResolveSupportedVersionGuidAsync(string executorTitle, bool forceRefresh = false)
        {
            var executor = await FindExecutorAsync(executorTitle, forceRefresh).ConfigureAwait(false);
            if (executor == null)
                return null;

            var hash = executor.RbxVersion?.Trim();
            if (string.IsNullOrEmpty(hash) || !hash.StartsWith("version-", StringComparison.OrdinalIgnoreCase))
                return null;

            return hash;
        }
    }
}
