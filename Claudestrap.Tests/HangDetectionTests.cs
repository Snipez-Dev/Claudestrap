using Claudestrap.Utility;

namespace Claudestrap.Tests;

public class HangDetectionTests
{
    private static readonly DateTimeOffset Started = DateTimeOffset.Parse("2026-10-19T12:00:00Z");
    private static HangDetectionState Create() => new(Started,
        TimeSpan.FromSeconds(45), TimeSpan.FromSeconds(15));

    [Fact]
    public void StartupAndWindowlessClientsDoNotTriggerCapture()
    {
        var state = Create();
        Assert.False(state.Observe(Started.AddSeconds(10), false, true));
        Assert.False(state.Observe(Started.AddSeconds(44), false, true));
        Assert.False(state.Observe(Started.AddSeconds(100), false, false));
        Assert.False(state.Observe(Started.AddSeconds(120), false, false));
    }

    [Fact]
    public void RecoveredClientResetsTheContinuousHangTimer()
    {
        var state = Create();
        Assert.False(state.Observe(Started.AddSeconds(50), false, true));
        Assert.False(state.Observe(Started.AddSeconds(60), true, true));
        Assert.False(state.Observe(Started.AddSeconds(70), false, true));
        Assert.False(state.Observe(Started.AddSeconds(84), false, true));
        Assert.True(state.Observe(Started.AddSeconds(85), false, true));
    }

    [Fact]
    public void PersistentHangCapturesOnceEvenAfterRecovery()
    {
        var state = Create();
        Assert.False(state.Observe(Started.AddSeconds(45), false, true));
        Assert.False(state.Observe(Started.AddSeconds(59), false, true));
        Assert.True(state.Observe(Started.AddSeconds(60), false, true));
        Assert.False(state.Observe(Started.AddSeconds(80), false, true));
        Assert.False(state.Observe(Started.AddSeconds(90), true, true));
        Assert.False(state.Observe(Started.AddSeconds(120), false, true));
        Assert.False(state.Observe(Started.AddSeconds(150), false, true));
    }

    [Fact]
    public void LosingWindowResetsHangTimer()
    {
        var state = Create();
        Assert.False(state.Observe(Started.AddSeconds(50), false, true));
        Assert.False(state.Observe(Started.AddSeconds(60), false, false));
        Assert.False(state.Observe(Started.AddSeconds(70), false, true));
        Assert.False(state.Observe(Started.AddSeconds(84), false, true));
        Assert.True(state.Observe(Started.AddSeconds(85), false, true));
    }
}
