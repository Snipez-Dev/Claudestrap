using System;
using System.Collections.Generic;
using System.Linq;

namespace Claudestrap.Utility;

public static class FastFlagSupportCatalog
{
    public const string SourceUrl = "https://devforum.roblox.com/t/allowlist-for-local-client-configuration-via-fast-flags/3966569";
    public const string ReferenceDate = "2025-09-29";
    // Published Roblox reference, not a claim about the currently running client.
    private static readonly HashSet<string> Reference = new(StringComparer.Ordinal)
    {
        "DFIntCSGLevelOfDetailSwitchingDistance", "DFIntCSGLevelOfDetailSwitchingDistanceL12",
        "DFIntCSGLevelOfDetailSwitchingDistanceL23", "DFIntCSGLevelOfDetailSwitchingDistanceL34",
        "FFlagHandleAltEnterFullscreenManually", "DFFlagTextureQualityOverrideEnabled", "DFIntTextureQualityOverride",
        "FIntDebugForceMSAASamples", "DFFlagDisableDPIScale", "FFlagDebugGraphicsPreferD3D11", "FFlagDebugSkyGray",
        "DFFlagDebugPauseVoxelizer", "DFIntDebugFRMQualityLevelOverride", "FIntFRMMaxGrassDistance",
        "FIntFRMMinGrassDistance", "FFlagDebugGraphicsPreferVulkan", "FFlagDebugGraphicsPreferOpenGL",
        "FIntGrassMovementReducedMotionFactor"
    };

    public static bool InPublishedReference(string name) => Reference.Contains(name);
    public static string Describe(string name) => InPublishedReference(name)
        ? "Auf Roblox-Referenzliste; Wirkung ungeprüft"
        : "Nicht auf Referenzliste; aktuelle Freigabe ungeprüft";

    public static string Summary(IEnumerable<string> names)
    {
        string[] all = names.Distinct(StringComparer.Ordinal).ToArray();
        int listed = all.Count(InPublishedReference);
        return $"{listed}/{all.Length} auf veröffentlichter Roblox-Referenzliste ({ReferenceDate}). "
            + "Gespeichert bedeutet nicht wirksam. Die aktuelle Freigabe und die Wirkung im Client sind ungeprüft.";
    }
}
