using Claudestrap.Utility;

namespace Claudestrap.Tests;

public sealed class FpsBoostControllerTests
{
    private readonly Dictionary<string, string> _flags = new()
    {
        ["FIntDebugForceMSAASamples"] = "4",
        ["FFlagDebugForceFutureIsBrightPhase3"] = "True",
        ["FFlagDisablePostFx"] = "False",
        ["Unrelated"] = "keep"
    };

    private FpsBoostController Create() => new(
        key => _flags.GetValueOrDefault(key),
        (key, value) => { if (value is null) _flags.Remove(key); else _flags[key] = value; });

    [Fact]
    public void MaximumOffRestoresLightingAndManualSettings()
    {
        var before = new Dictionary<string, string>(_flags);
        var boost = Create();
        boost.Select("Maximum");
        Assert.Equal("Maximum", boost.Detect());
        Assert.False(_flags.ContainsKey("FFlagDebugForceFutureIsBrightPhase3"));
        boost.Select("Off");
        Assert.Equal(before.OrderBy(p => p.Key), _flags.OrderBy(p => p.Key));
        Assert.Equal("Off", boost.Detect());
    }

    [Fact]
    public void MaximumBalancedRestoresLightingAndAntiAliasing()
    {
        var boost = Create();
        boost.Select("Maximum");
        boost.Select("Balanced");
        Assert.Equal("4", _flags["FIntDebugForceMSAASamples"]);
        Assert.Equal("True", _flags["FFlagDebugForceFutureIsBrightPhase3"]);
        Assert.False(_flags.ContainsKey("DFFlagDebugRenderForceTechnologyVoxel"));
        Assert.Equal("Balanced", boost.Detect());
    }

    [Fact]
    public void TurningOffDoesNotUndoAProfileSelectedAfterBoost()
    {
        var boost = Create();
        boost.Select("Maximum");
        _flags["FIntDebugForceMSAASamples"] = "8";
        boost.Select("Off");
        Assert.Equal("8", _flags["FIntDebugForceMSAASamples"]);
    }

    [Fact]
    public void PreviousSettingsSurviveReopeningTheSettingsWindow()
    {
        var boost = Create();
        boost.Select("Maximum");
        var reopened = Create();
        reopened.RestoreState(boost.PreviousValues, boost.AppliedValues);
        reopened.Select("Off");
        Assert.Equal("4", _flags["FIntDebugForceMSAASamples"]);
        Assert.Equal("True", _flags["FFlagDebugForceFutureIsBrightPhase3"]);
    }
}
