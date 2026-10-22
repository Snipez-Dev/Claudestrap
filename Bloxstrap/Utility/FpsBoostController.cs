using System;
using System.Collections.Generic;
using System.Linq;

namespace Claudestrap.Utility;

public sealed class FpsBoostController(Func<string, string?> read, Action<string, string?> write)
{
    private static readonly Dictionary<string, string> Balanced = new()
    {
        ["FFlagFastGPULightCulling3"] = "True",
        ["FFlagDebugForceFSMCPULightCulling"] = "True",
        ["DFFlagTaskSchedulerAvoidSleep"] = "True",
        ["FFlagDisablePostFx"] = "True",
        ["DFFlagEnableMeshPreloading2"] = "True"
    };

    private readonly Dictionary<string, string?> _applied = new(StringComparer.Ordinal);
    public IReadOnlyDictionary<string, string?> PreviousValues { get; private set; } = new Dictionary<string, string?>();

    public string Detect()
    {
        if (_applied.Count == 0) return "Off";
        if (!Balanced.All(x => read(x.Key) == x.Value)) return "Off";
        return Maximum.All(x => read(x.Key) == x.Value) ? "Maximum" : "Balanced";
    }

    public static IReadOnlyDictionary<string, string?> Maximum { get; } = new Dictionary<string, string?>
    {
        ["DFIntDebugFRMQualityLevelOverride"] = "1",
        ["FIntDebugForceMSAASamples"] = "0",
        ["FIntDebugFRMOptionalMSAALevelOverride"] = "0",
        ["FIntRenderShadowIntensity"] = "0",
        ["FIntFRMMinGrassDistance"] = "0",
        ["FIntFRMMaxGrassDistance"] = "0",
        ["FIntRenderGrassDetailStrands"] = "0",
        ["DFFlagDebugRenderForceTechnologyVoxel"] = "True",
        ["FFlagDebugForceFutureIsBrightPhase2"] = null,
        ["FFlagDebugForceFutureIsBrightPhase3"] = null,
        ["FFlagRenderUnifiedLighting14"] = null
    };

    public void RestoreState(IReadOnlyDictionary<string, string?> previous, IReadOnlyDictionary<string, string?> applied)
    {
        PreviousValues = new Dictionary<string, string?>(previous);
        _applied.Clear();
        foreach (var pair in applied) _applied[pair.Key] = pair.Value;
    }

    public IReadOnlyDictionary<string, string?> AppliedValues => _applied;

    public void Select(string level)
    {
        if (level is not ("Off" or "Balanced" or "Maximum")) throw new ArgumentOutOfRangeException(nameof(level));
        Restore();
        if (level == "Off") return;
        var values = Balanced.ToDictionary(pair => pair.Key, pair => (string?)pair.Value);
        if (level == "Maximum") foreach (var pair in Maximum) values[pair.Key] = pair.Value;
        PreviousValues = values.Keys.ToDictionary(key => key, read);
        foreach (var pair in values) { write(pair.Key, pair.Value); _applied[pair.Key] = pair.Value; }
    }

    private void Restore()
    {
        foreach (var pair in _applied)
            if (read(pair.Key) == pair.Value && PreviousValues.TryGetValue(pair.Key, out var original)) write(pair.Key, original);
        _applied.Clear();
        PreviousValues = new Dictionary<string, string?>();
    }
}
