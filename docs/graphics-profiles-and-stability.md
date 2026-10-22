# Graphics profiles and client stability

The FastFlags page now exposes **Performance**, **Normal** and **Quality**.
Choose a profile, save settings, then restart Roblox.

| Profile | Textures | Anti-aliasing | Mesh detail / grass |
| --- | --- | --- | --- |
| Performance | Lowest override | 1 sample | Shorter CSG LOD distances; grass distances zero |
| Normal | Roblox defaults | Roblox defaults | Roblox defaults |
| Quality | Highest override | 4 samples | Roblox defaults |

Profiles replace only the nine graphics keys listed in `GraphicsProfiles.OwnedFlags`.
Other custom flags, render-backend preferences, audio, networking, FPS limits and
CPU settings are preserved. Selecting a profile enables the FastFlag manager.
Normal removes the managed overrides rather than hardcoding guessed engine defaults.
Manual changes to managed settings show the configuration as Custom until a matching
profile is selected again. An existing installation starts as Custom; opening the
page does not replace its graphics configuration.

The flags are drawn from Roblox's [published local configuration allowlist](https://devforum.roblox.com/t/allowlist-for-local-client-configuration-via-fast-flags/3966569).
Roblox can change the allowlist; unsupported flags may be ignored. The graphics
slider inside Roblox remains independent of these profiles.

## Stability mode

**Client stability mode** defaults to enabled, including when loading older settings
that do not contain this field. For the next launch it skips:

- Automatic memory cleaning, including the multi-account launch cleanup.
- The background optimizer's working-set, CPU affinity and priority changes.
- The separate CPU limiter and configured process-priority worker.
- Launcher-triggered exclusive fullscreen.
- The option that terminates Roblox's crash-handler process.

Saved tuning values are retained and become available again when stability mode
is disabled. The unrelated hidden working-set trim on Discord presence updates
has been removed entirely. The explicit memory cleaner no longer lowers Roblox's
priority to Below Normal.

These changes remove launcher interference; they do not patch Roblox's engine,
disable its internal hang timeout, or guarantee that a driver/engine bug is fixed.
The reported older dumps contained `Unable to resize DXGI swapchain: 887a0001`.
The newer occurrence ended with `Terminated due it was hanging past timeout.`
Neither observation proves that Claudestrap's memory cleaner caused those failures.

## Optional local hang diagnostics

Enable **Capture hang diagnostics**, save, and restart Roblox. It defaults to off.
The watcher waits through a 45-second startup grace period and then checks for a
window that remains unresponsive for at least 15 seconds. A recovering or windowless
client resets the timer. One capture attempt is allowed per watched session.

Reports are stored under `<Claudestrap installation>/Diagnostics/<UTC timestamp>_<PID>/`:

- `report.json`: active audio devices at watcher startup and at capture, profile,
  stability setting, selected diagnostic log categories, and dump success/error.
- `Roblox-hang.dmp`: thread/module and selected process-memory information, when
  Windows grants the necessary access. A failed capture still records a report.

The watcher does not kill, restart or upload the client. Dumps can contain private
session data; inspect them locally before sharing. Captures can take time and an
unresponsive window alone does not prove a permanent deadlock. A real Roblox process
may deny dump access even when the test helper permits it. Captures are attempts,
not guaranteed full dumps of a protected client.

## Roblox storage paths

Existing version caches in the installation's `ClaudestrapData/RblxVersions`,
beside the launcher, or under the legacy AppData directory are reused in that
order. A new custom installation uses `<installation>/ClaudestrapData`; it does
not require an AppData junction. An explicitly supplied Roblox storage directory
is honored. A dangling legacy junction is ignored when selecting storage for a
custom installation.

Portable installations are recognized by `AppSettings.json` (or legacy
`Settings.json`) together with `State.json`, even when additional files are present.

Failure to create the version directory is reported during initialization. Failed
or cancelled package installation stops the launch before mod deployment, and an
installation is not marked successful without a client executable. With updates
disabled, an existing installed version can still be used.

The path-fix preview is versioned `1.1.1.8-preview.1` (assembly/file `1.1.1.8`),
above the official `1.1.1.7` build observed during diagnosis, so the updater does
not immediately offer that older binary as an upgrade.

## Validation

```powershell
dotnet build Claudestrap.sln -c Release
dotnet test Claudestrap.Tests/Claudestrap.Tests.csproj -c Release
dotnet publish Bloxstrap/Claudestrap.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

Regression tests cover profile transitions, preservation of unrelated flags,
serialization/manual edits, startup grace, recovery, window loss and one-shot hang
capture decisions. A separate local smoke test exercised the compiled view model,
settings serialization, audio snapshots and native dump creation against a helper
process. Roblox gameplay/audio reconnect behavior still requires a live comparison.
