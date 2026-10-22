using System;
using System.IO;
using System.Threading.Tasks;

namespace Claudestrap.Utility;

public static class ExecutableUpdate
{
    public static async Task ApplyAsync(string downloaded, string executable, Func<string, Task<bool>> startAndConfirm)
    {
        string staged = executable + "." + Guid.NewGuid().ToString("N") + ".new";
        string? backup = null;
        bool moved = false;
        try
        {
            File.Copy(downloaded, staged);
            backup = UpdateBackupPaths.Prepare(executable);
            File.Move(executable, backup);
            moved = true;
            File.Move(staged, executable);
            if (!await startAndConfirm(executable).ConfigureAwait(false))
                throw new IOException("The updated launcher did not confirm startup. The previous executable has been restored.");
        }
        catch
        {
            if (moved)
            {
                if (File.Exists(executable)) File.Delete(executable);
                File.Move(backup!, executable);
            }
            throw;
        }
        finally { if (File.Exists(staged)) File.Delete(staged); }
    }
}
