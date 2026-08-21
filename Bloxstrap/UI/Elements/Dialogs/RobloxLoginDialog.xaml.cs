using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using Claudestrap;
using Claudestrap.Utility;

namespace Claudestrap.UI.Elements.Dialogs
{
    /// <summary>
    /// Embedded-browser Roblox login. Points a fresh WebView2 profile at
    /// Roblox's own login page -- username/password, passkey, 2FA, whatever
    /// challenge Roblox throws, all handled by Roblox's real page, not us --
    /// then polls for the .ROBLOSECURITY cookie the same way MultiRoblox-RAM's
    /// login.rs does (try_get_cookie: named cookie, roblox.com domain, value
    /// over 100 chars) and validates it against the identity endpoint before
    /// handing it back.
    /// </summary>
    public partial class RobloxLoginDialog
    {
        public sealed record LoginResult(bool Ok, string? Cookie, string? Username, string? UserId, string? Error);

        private readonly TaskCompletionSource<LoginResult> _completionSource = new();
        private readonly DispatcherTimer _pollTimer;
        private readonly string _profileDir;
        private bool _isCompleted;

        public RobloxLoginDialog()
        {
            InitializeComponent();

            _profileDir = Path.Combine(Path.GetTempPath(), "Claudestrap-login-" + Guid.NewGuid().ToString("N"));

            _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
            _pollTimer.Tick += async (_, _) => await PollForCookieAsync();

            Loaded += OnLoaded;
            Closing += OnClosing;
        }

        public Task<LoginResult> ShowAndWaitAsync()
        {
            Show();
            return _completionSource.Task;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            const string LOG_IDENT = "RobloxLoginDialog::OnLoaded";

            try
            {
                var env = await CoreWebView2Environment.CreateAsync(userDataFolder: _profileDir);
                await LoginWebView.EnsureCoreWebView2Async(env);

                LoginWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;

                LoginWebView.Source = new Uri("https://www.roblox.com/login");

                _pollTimer.Start();

                // Safety timeout so a login nobody finishes doesn't leave the
                // dialog (and the profile directory) hanging around forever.
                await Task.Delay(TimeSpan.FromMinutes(5));
                if (!_isCompleted)
                    CompleteWithError("Timed out waiting for login. Please try again.");
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Failed to initialize WebView2: {ex.Message}");
                CompleteWithError("Failed to load the embedded browser. Please try again, or add the account by pasting its cookie instead.");
            }
        }

        private void LoginWebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
            LoginWebView.Visibility = Visibility.Visible;
        }

        private async Task PollForCookieAsync()
        {
            const string LOG_IDENT = "RobloxLoginDialog::PollForCookieAsync";

            if (_isCompleted || LoginWebView.CoreWebView2 is null)
                return;

            try
            {
                var cookies = await LoginWebView.CoreWebView2.CookieManager.GetCookiesAsync("https://www.roblox.com");
                var securityCookie = cookies.FirstOrDefault(c =>
                    c.Name == ".ROBLOSECURITY" && c.Domain.Contains("roblox.com") && c.Value.Length > 100);

                if (securityCookie is null)
                    return;

                _pollTimer.Stop();

                var info = await RobloxAuthApi.FetchUserInfoAsync(securityCookie.Value);
                if (!info.Ok)
                {
                    App.Logger.WriteLine(LOG_IDENT, $"Cookie found but validation failed: {info.Reason}");
                    CompleteWithError("Signed in, but the account could not be verified. Please try again.");
                    return;
                }

                CompleteWithSuccess(securityCookie.Value, info.Username, info.UserId);
            }
            catch (Exception ex)
            {
                App.Logger.WriteLine(LOG_IDENT, $"Poll failed: {ex.Message}");
            }
        }

        private void CompleteWithSuccess(string cookie, string? username, string? userId)
        {
            _isCompleted = true;
            _pollTimer.Stop();
            _completionSource.TrySetResult(new LoginResult(true, cookie, username, userId, null));
            Dispatcher.Invoke(Close);
        }

        private void CompleteWithError(string message)
        {
            _isCompleted = true;
            _pollTimer.Stop();
            _completionSource.TrySetResult(new LoginResult(false, null, null, null, message));

            Dispatcher.Invoke(() =>
            {
                Frontend.ShowMessageBox(message, MessageBoxImage.Warning);
                Close();
            });
        }

        private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _pollTimer.Stop();

            if (!_isCompleted)
                _completionSource.TrySetResult(new LoginResult(false, null, null, null, null));

            try
            {
                if (Directory.Exists(_profileDir))
                    Directory.Delete(_profileDir, recursive: true);
            }
            catch
            {
                // best-effort cleanup -- WebView2 may still hold file locks briefly after close
            }
        }

        private void Header_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            if (e.LeftButton != MouseButtonState.Pressed) return;

            try { DragMove(); }
            catch (InvalidOperationException) { }
        }

        private void Minimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = System.Windows.WindowState.Minimized;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
