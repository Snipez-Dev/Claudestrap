namespace Claudestrap.Utility
{
    public static class GraphicsProfiles
    {
        // Only these graphics overrides belong to the presets. Audio, renderer,
        // networking and scheduler settings remain independent.
        public static IReadOnlyList<string> OwnedFlags { get; } = Array.AsReadOnly(new[]
        {
            "DFFlagTextureQualityOverrideEnabled",
            "DFIntTextureQualityOverride",
            "FIntDebugForceMSAASamples",
            "FIntFRMMinGrassDistance",
            "FIntFRMMaxGrassDistance",
            "DFIntCSGLevelOfDetailSwitchingDistance",
            "DFIntCSGLevelOfDetailSwitchingDistanceL12",
            "DFIntCSGLevelOfDetailSwitchingDistanceL23",
            "DFIntCSGLevelOfDetailSwitchingDistanceL34"
        });

        public static IReadOnlyDictionary<string, string?> GetOverrides(GraphicsProfile profile)
        {
            if (!Enum.IsDefined(profile))
                throw new ArgumentOutOfRangeException(nameof(profile));

            var flags = OwnedFlags.ToDictionary(key => key, _ => (string?)null);

            if (profile == GraphicsProfile.Performance)
            {
                flags["DFFlagTextureQualityOverrideEnabled"] = "True";
                flags["DFIntTextureQualityOverride"] = "0";
                flags["FIntDebugForceMSAASamples"] = "1";
                flags["FIntFRMMinGrassDistance"] = "0";
                flags["FIntFRMMaxGrassDistance"] = "0";
                flags["DFIntCSGLevelOfDetailSwitchingDistance"] = "30";
                flags["DFIntCSGLevelOfDetailSwitchingDistanceL12"] = "15";
                flags["DFIntCSGLevelOfDetailSwitchingDistanceL23"] = "10";
                flags["DFIntCSGLevelOfDetailSwitchingDistanceL34"] = "5";
            }
            else if (profile == GraphicsProfile.Quality)
            {
                flags["DFFlagTextureQualityOverrideEnabled"] = "True";
                flags["DFIntTextureQualityOverride"] = "3";
                flags["FIntDebugForceMSAASamples"] = "4";
            }

            return flags;
        }

        public static bool Matches(GraphicsProfile profile, Func<string, string?> getValue)
            => GetOverrides(profile).All(pair =>
                string.Equals(getValue(pair.Key), pair.Value, StringComparison.OrdinalIgnoreCase));

        public static GraphicsProfile? GetEffectiveProfile(GraphicsProfile? selected, Func<string, string?> getValue)
            => selected.HasValue && Enum.IsDefined(selected.Value) && Matches(selected.Value, getValue)
                ? selected : null;

        public static string Describe(GraphicsProfile? profile) => profile switch
        {
            GraphicsProfile.Performance => "Low-resolution textures, minimal anti-aliasing, reduced mesh detail and no grass. Applies on the next launch.",
            GraphicsProfile.Normal => "Roblox defaults for textures, anti-aliasing, mesh detail and grass. Applies on the next launch.",
            GraphicsProfile.Quality => "High-resolution textures and 4x anti-aliasing, with Roblox defaults for mesh detail and grass. Applies on the next launch.",
            _ => "Custom graphics settings. Select a profile to replace texture, anti-aliasing, mesh detail and grass overrides. Other settings are kept."
        };
    }
}
