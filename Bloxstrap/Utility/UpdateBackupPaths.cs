using System;
using System.IO;

namespace Claudestrap.Utility;

public static class UpdateBackupPaths
{
    public static string Prepare(string executablePath)
    {
        string backupPath = executablePath + ".old";

        try
        {
            File.Delete(backupPath);
            return backupPath;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A helper process can still be running from the previous update's backup.
            return executablePath + "." + Guid.NewGuid().ToString("N") + ".old";
        }
    }
}
