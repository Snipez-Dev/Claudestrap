using System.Diagnostics;
using System.Threading;

namespace Claudestrap.Utility;

public static class LauncherStartup
{
    private static readonly Stopwatch Clock = new();
    private static long _settingsReady;
    private static long _firstWindow;
    public static void Start() => Clock.Restart();
    public static long ElapsedMilliseconds => Clock.ElapsedMilliseconds;
    public static long SettingsReadyMilliseconds => Interlocked.Read(ref _settingsReady);
    public static long FirstWindowMilliseconds => Interlocked.Read(ref _firstWindow);
    public static void SettingsReady() => Interlocked.Exchange(ref _settingsReady, Clock.ElapsedMilliseconds);
    public static void FirstWindowReady() => Interlocked.CompareExchange(ref _firstWindow, Clock.ElapsedMilliseconds, 0);
}
