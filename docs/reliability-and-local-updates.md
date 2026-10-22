# Reliability and local updates

Local builds carry `ClaudestrapUpdateChannel=local` as assembly metadata. The default blocks in-place replacement by upstream releases, including single-file builds. Version checks remain available. Only builds intentionally produced with `-p:ClaudestrapUpdateChannel=upstream` may use the upstream binary updater; that channel must not be used for builds whose custom features must survive an update.

The updater reads the requested release's metadata, pins the asset URL to that release, and requires its size and SHA-256 digest. `VerifiedUpdateDownload` handles partial downloads, validates HTTP ranges, times out stalled reads and checks the resulting file. Each transaction has its own download directory and an exclusive installation lock. `ExecutableUpdate` stages the file beside the installed executable. `UpdateHandoff` requires the new managed application to finish initialization and signal readiness before the old application exits. Failed confirmation restores the old executable. The signal precedes menu locks; the new process waits for its parent to exit before opening its menu.

`AtomicJsonFile` uses a per-path process mutex, a flushed temporary file, an expected content hash and replacement with a backup. A stale writer fails rather than overwriting a newer file. A malformed primary can be read from its valid `.bak`; the original remains in place until an explicit save, which preserves malformed content separately. `JsonManager` blocks saving after an unrecoverable load failure. Settings Apply/Save-and-launch does not report success after a failed save.

The FPS boost controller records both previous and applied values in settings. It restores a value only if it still matches the boost's applied value, so later manual/profile changes survive. Leaving Maximum restores lighting and anti-aliasing. The separate graphics profile and boost controls notify each other when shared flags change. The former RAM-limit control is called a memory-cache target: it writes a requested cache FastFlag, not a proven limit for the entire Roblox process. The general cache preset no longer writes that same flag.

Cleaner retention values are days in UTC. Never returns without calculating a cutoff. Files must be old by both creation and modification time. Logs use a timestamp, process ID and unique suffix; logging I/O failures no longer masquerade as duplicate launcher starts. About contains a local diagnostic section showing the executable, settings and Roblox directories, build/update channel, current log and latest process-local error.

Build on Windows using the SDK selected by `global.json`:

```powershell
dotnet test Claudestrap.Tests/Claudestrap.Tests.csproj -c Release
dotnet publish Bloxstrap/Claudestrap.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeSourceRevisionInInformationalVersion=false -o publish
```

The tests exercise recovery, stale/concurrent JSON writers, simultaneous log names, retention boundaries, protected build channels, FPS transitions, interrupted and stalled HTTP streams, invalid ranges/hashes, cancellation and failed-start executable rollback. HTTP tests use controlled response streams and do not replace an installed application.
