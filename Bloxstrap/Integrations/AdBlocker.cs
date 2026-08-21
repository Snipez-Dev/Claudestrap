using System.Collections.Concurrent;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace Claudestrap.Integrations
{
    /// <summary>
    /// Ad-blocking engine for WebView2 using EasyList/uBlock filter syntax.
    /// Downloads and caches the official EasyList, then applies network and
    /// cosmetic filters through request interception and CSS injection.
    /// </summary>
    public sealed class AdBlocker
    {
        // Fast substring-match domains (no regex overhead for these)
        private readonly HashSet<string> _blockedDomains = new(StringComparer.OrdinalIgnoreCase)
        {
            // ── Google ads ──
            "doubleclick.net", "googlesyndication.com", "googleadservices.com",
            "pagead2.googlesyndication.com", "ad.doubleclick.net", "adclick.g.doubleclick.net",
            "www.googletagmanager.com/gtm", "www.googletagservices.com",

            // ── Major ad networks ──
            "amazon-adsystem.com", "serving-sys.com", "criteo.com", "criteo.net",
            "outbrain.com", "taboola.com", "adsrvr.org", "adnxs.com",
            "rubiconproject.com", "pubmatic.com", "openx.net", "casalemedia.com",
            "moatads.com", "adsafeprotected.com",

            // ── Tracking / analytics ──
            "google-analytics.com", "googletagmanager.com", "hotjar.com",
            "mouseflow.com", "fullstory.com", "crazyegg.com", "optimizely.com",
            "kissmetrics.com", "mixpanel.com", "segment.io", "amplitude.com",
            "heap.io", "scorecardresearch.com", "quantserve.com",
            "bluekai.com", "exelator.com", "demdex.net",

            // ── Popup/popunder ad networks ──
            "popads.net", "popcash.net", "adcash.com", "propellerads.com",
            "clickadu.com", "trafficjunky.com", "exoclick.com", "adxpansion.com",
            "adzerk.net", "bidvertiser.com", "revcontent.com", "mgid.com",

            // ── Social media ad trackers ──
            "ads.linkedin.com", "ads.twitter.com", "analytics.twitter.com",
            "ads.pinterest.com", "tr.pinterest.com", "snap.ads.com",

            // ── AniWorld-specific junk ──
            "noozy.tv", "piccdn.net/blank-728x90",
            "propellerpops.com",

            // ── Mining / malware ──
            "coinimp.com", "coinhive.com", "cryptoloot.com", "minero.cc",
            "webmine.cz", "coin-have.com", "jsecoin.com",
        };

        // Additional patterns that need partial matching
        private readonly string[] _partialMatches =
        {
            "aniw.gif",
            "popunder",
            "trafficfactory",
        };

        public string AggregatedCss { get; private set; }
        private readonly string _antiAdblockScript;

        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };
        private static string? _cachedEasyListCss;
        private static readonly object _cacheLock = new();

        private const string EASYLIST_URL = "https://easylist.to/easylist/easylist.txt";
        private const string CACHE_PATH = "easylist_cache.txt";

        public AdBlocker()
        {
            AggregatedCss = BuildBaseCss();
            _antiAdblockScript = BuildAntiAdblockScript();
        }

        /// <summary>
        /// Initialize the ad blocker asynchronously — fetches EasyList in the
        /// background if a cached copy isn't already available.
        /// </summary>
        public async Task InitializeAsync()
        {
            // Try loading cached EasyList first
            string? easyList = LoadCachedEasyList();

            if (easyList == null)
            {
                try
                {
                    easyList = await _http.GetStringAsync(EASYLIST_URL);
                    SaveCachedEasyList(easyList);
                }
                catch
                {
                    // Offline/no cache — base CSS rules still apply
                }
            }

            if (easyList != null)
            {
                string parsed = ParseEasyListCosmetic(easyList);
                if (!string.IsNullOrEmpty(parsed))
                {
                    lock (_cacheLock)
                    {
                        AggregatedCss = BuildBaseCss() + parsed;
                        _cachedEasyListCss = parsed;
                    }
                }
            }
        }

        public bool ShouldBlock(string url)
        {
            // Fast path: check domain against hash set
            foreach (var domain in _blockedDomains)
            {
                if (url.Contains(domain, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            // Partial match patterns (fewer of these, rare hit)
            foreach (var pattern in _partialMatches)
            {
                if (url.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        public string GetCssInjection() => AggregatedCss;
        public string GetAntiAdblockScript() => _antiAdblockScript;

        // ──────────────────────────────────────────────
        //  CSS rules
        // ──────────────────────────────────────────────

        private static string BuildBaseCss()
        {
            // Pre-built CSS that covers 90% of ad elements
            return @"
/* ── Generic ad containers ── */
div[class*=""advert""], div[id*=""advert""],
div[class*=""sponsor""], div[id*=""sponsor""],
div[class*=""banner""]:not([class*=""banner_""]):not([id*=""banner_""]),
div[class*=""popup""], div[class*=""popunder""], div[class*=""pop-under""],
div[class*=""overlay-ad""], div[class*=""modal-ad""],
div[class*=""-ad-""], div[class*=""_ad_""], div[class*=""-ad""], div[class=""ad""],
div[class*=""promo-""], div[class*=""-promo""],
div[id*=""google_ads""], div[class*=""google_ads""],
div[class*=""interstitial""],

/* ── Iframe ads ── */
iframe[src*=""doubleclick""], iframe[src*=""googleads""],
iframe[src*=""adserver""], iframe[src*=""ad.""], iframe[src*=""ads.""],

/* ── Image ads ── */
img[src*=""banner""]:not([src*=""anime""]):not([src*=""poster""]):not([src*=""cover""]),
img[src*=""728x90""], img[src*=""300x250""], img[src*=""160x600""],

/* ── Anti-adblock walls ── */
div[class*=""adblock""], div[id*=""adblock""],
div[class*=""adb-""], div[class*=""anti-adb""],
div[class*=""blockadblock""],

/* ── Newsletter / signup ── */
div[class*=""newsletter-overlay""], div[class*=""subscribe-overlay""],
div[class*=""signup-popup""],

/* ── Social widgets ── */
div[class*=""social-share""], div[class*=""share-buttons""]
{ display:none!important;visibility:hidden!important;opacity:0!important;height:0!important;width:0!important;position:absolute!important;pointer-events:none!important;z-index:-9999!important;overflow:hidden!important; }";
        }

        // ──────────────────────────────────────────────
        //  EasyList parser (cosmetic rules only)
        // ──────────────────────────────────────────────

        private static string ParseEasyListCosmetic(string easyList)
        {
            var selectors = new HashSet<string>();
            int count = 0;

            foreach (var line in easyList.Split('\n'))
            {
                string trimmed = line.Trim();

                // Skip comments, empty lines, and non-cosmetic rules
                if (trimmed.Length == 0 || trimmed[0] == '!' || trimmed[0] == '[')
                    continue;

                // ## = generic cosmetic filter
                int idx = trimmed.IndexOf("##");
                if (idx > 0)
                {
                    string selector = trimmed[(idx + 2)..];
                    if (selector.Length > 1 && selector.Length < 200 && !selector.Contains('{'))
                    {
                        selectors.Add(selector);
                        count++;
                        if (count >= 2000) break; // cap to prevent performance issues
                    }
                }
            }

            if (selectors.Count == 0) return "";

            return ", " + string.Join(", ", selectors.Take(2000))
                + " { display:none!important;visibility:hidden!important;opacity:0!important;height:0!important;position:absolute!important;z-index:-9999!important; }";
        }

        // ──────────────────────────────────────────────
        //  Anti-adblock script
        // ──────────────────────────────────────────────

        private static string BuildAntiAdblockScript()
        {
            return @"
(function(){
    const N=()=>{}; const T=()=>true; const F=()=>false;
    const overrides={
        blockAdBlock:false,adBlock:false,adblock:false,adblocker:false,
        canRunAds:true,adsBlocked:false,adsAreBlocked:false,AdBlockDetected:false,
        isAdBlockActive:F,detectAdBlock:F,checkAdBlocker:F,
        uabInject:false,AdBlock:false,blockAdblock:false,
        _adb:false,_uab:false,_canRunAds:true,
    };
    for(const[k,v]of Object.entries(overrides)){
        try{Object.defineProperty(window,k,{value:v,writable:false,configurable:false});}catch(e){}
    }
    // Periodically clean anti-adblock popups
    setInterval(function(){
        try{
            document.querySelectorAll('[class*=""adblock""],[id*=""adblock""],[class*=""adb-""],[class*=""anti-adb""],[class*=""blockadblock""],[id*=""blockadblock""]').forEach(function(e){e.remove();});
        }catch(e){}
    },500);
    // Also run on DOM changes
    new MutationObserver(function(){
        try{
            document.querySelectorAll('[class*=""adblock""],[id*=""adblock""],[class*=""adb-""],[class*=""anti-adb""]').forEach(function(e){e.remove();});
        }catch(e){}
    }).observe(document.body,{childList:true,subtree:true});
})();
";
        }

        // ──────────────────────────────────────────────
        //  Cache management
        // ──────────────────────────────────────────────

        private static string? LoadCachedEasyList()
        {
            lock (_cacheLock)
            {
                if (_cachedEasyListCss != null)
                    return _cachedEasyListCss;
            }

            try
            {
                string cacheFile = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Claudestrap", CACHE_PATH);

                if (File.Exists(cacheFile))
                {
                    // Cache expires after 7 days
                    if (DateTime.UtcNow - File.GetLastWriteTimeUtc(cacheFile) < TimeSpan.FromDays(7))
                    {
                        return File.ReadAllText(cacheFile);
                    }
                }
            }
            catch { }

            return null;
        }

        private static void SaveCachedEasyList(string content)
        {
            try
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Claudestrap");
                Directory.CreateDirectory(dir);

                string cacheFile = Path.Combine(dir, CACHE_PATH);
                File.WriteAllText(cacheFile, content);
            }
            catch { }
        }

        // Pre-load EasyList CSS from cache immediately
        public static string GetCachedCss()
        {
            string? cached = LoadCachedEasyList();
            if (cached != null)
            {
                return ParseEasyListCosmetic(cached);
            }
            return "";
        }
    }
}
