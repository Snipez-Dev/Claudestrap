using System;

namespace Claudestrap.Utility;

public static class LogFileNames
{
    public static string Create(string project, DateTime timestampUtc, int processId)
        => $"{project}_{timestampUtc:yyyyMMdd'T'HHmmssfffffff'Z'}_{processId}_{Guid.NewGuid():N}.log";
}
