namespace Claudestrap.Utility;

public static class UpdateBuildPolicy
{
    public static bool CanReplace(string? channel, bool multiFileBuild)
        => !multiFileBuild && string.Equals(channel, "upstream", System.StringComparison.Ordinal);
}
