using Claudestrap.Utility;

namespace Claudestrap.Tests;

public class GraphicsProfilesTests
{
    private static void Apply(Dictionary<string, string> flags, GraphicsProfile profile)
    {
        foreach (var pair in GraphicsProfiles.GetOverrides(profile))
        {
            if (pair.Value is null) flags.Remove(pair.Key);
            else flags[pair.Key] = pair.Value;
        }
    }

    [Fact]
    public void SwitchingProfilesRemovesPreviousOverridesAndPreservesOtherSettings()
    {
        var flags = new Dictionary<string, string>
        {
            ["FFlagDebugGraphicsPreferVulkan"] = "True",
            ["DFFlagDisableDPIScale"] = "True",
            ["UnrelatedCustomSetting"] = "123"
        };
        var original = new Dictionary<string, string>(flags);

        Apply(flags, GraphicsProfile.Performance);
        Assert.Equal("0", flags["FIntFRMMaxGrassDistance"]);
        Apply(flags, GraphicsProfile.Quality);
        Assert.False(flags.ContainsKey("FIntFRMMaxGrassDistance"));
        Assert.False(flags.ContainsKey("DFIntCSGLevelOfDetailSwitchingDistance"));
        Assert.Equal("3", flags["DFIntTextureQualityOverride"]);

        Apply(flags, GraphicsProfile.Normal);
        Assert.Equal(original.OrderBy(x => x.Key), flags.OrderBy(x => x.Key));
    }

    [Theory]
    [InlineData(GraphicsProfile.Performance)]
    [InlineData(GraphicsProfile.Normal)]
    [InlineData(GraphicsProfile.Quality)]
    public void RoundTripDetectsProfileAndManualEdits(GraphicsProfile profile)
    {
        var flags = new Dictionary<string, string>();
        Apply(flags, profile);
        var roundTrip = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(
            System.Text.Json.JsonSerializer.Serialize(flags))!;
        string? Get(string key) => roundTrip.GetValueOrDefault(key);

        Assert.True(GraphicsProfiles.Matches(profile, Get));
        roundTrip["DFIntTextureQualityOverride"] = "2";
        Assert.False(GraphicsProfiles.Matches(profile, Get));
    }

    [Fact]
    public void InvalidStoredProfileDoesNotProduceOverrides()
        => Assert.Throws<ArgumentOutOfRangeException>(() =>
            GraphicsProfiles.GetOverrides((GraphicsProfile)999));

    [Fact]
    public void EffectiveProfileDoesNotMislabelLegacyOrManuallyEditedSettings()
    {
        var flags = new Dictionary<string, string>();
        string? Get(string key) => flags.GetValueOrDefault(key);
        Assert.Null(GraphicsProfiles.GetEffectiveProfile(null, Get));
        Assert.Null(GraphicsProfiles.GetEffectiveProfile((GraphicsProfile)999, Get));
        Apply(flags, GraphicsProfile.Quality);
        Assert.Equal(GraphicsProfile.Quality, GraphicsProfiles.GetEffectiveProfile(GraphicsProfile.Quality, Get));
        flags["DFIntTextureQualityOverride"] = "1";
        Assert.Null(GraphicsProfiles.GetEffectiveProfile(GraphicsProfile.Quality, Get));
    }

    [Fact]
    public void RepeatedSwitchesLeaveNoStalePerformanceFlags()
    {
        var flags = new Dictionary<string, string>();
        for (int i = 0; i < 10; i++)
        {
            Apply(flags, GraphicsProfile.Performance);
            Apply(flags, GraphicsProfile.Quality);
            Apply(flags, GraphicsProfile.Normal);
        }
        Assert.Empty(flags);
    }
}
