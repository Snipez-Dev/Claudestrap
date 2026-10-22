using System;

namespace Claudestrap.Utility;

public static class CleanerPolicy
{
    public static DateTime? CutoffUtc(DateTime nowUtc, int retentionDays)
        => retentionDays > 0 && retentionDays <= 3650 ? nowUtc.AddDays(-retentionDays) : null;

    public static bool IsExpired(DateTime createdUtc, DateTime modifiedUtc, DateTime cutoffUtc)
        => createdUtc <= cutoffUtc && modifiedUtc <= cutoffUtc;
}
