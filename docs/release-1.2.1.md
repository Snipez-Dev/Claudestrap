# Claudestrap 1.2.1

## Changed

- Updated the standalone Windows build to .NET 10.0.13 and the .NET 10 SDK toolchain.
- Open the launch menu before the network update check; defer optional Discord initialization and remove periodic forced garbage collection.
- Publish without extracting every managed assembly before startup.
- Show the published FastFlag reference separately from unverified client behavior. Rename the RAM-limit control to a memory-cache target.
- Add local diagnostics for build/update channel, paths, startup timing and the latest error.
- Add an optional Velopack package/update path with release channels and delta packaging. The standalone EXE remains available.

## Fixed

- Save JSON with replacement, backups and concurrent-change detection. Preserve unreadable originals and recover from valid backups instead of overwriting data with defaults.
- Resume update downloads at their actual saved position, validate release size/SHA-256 and retain partial transfers across launcher restarts through BITS or HTTP fallback.
- Handle locked updater backups and restore the executable if its replacement fails startup confirmation.
- Give simultaneous launcher/helper starts separate log files.
- Apply cleaner retention in days instead of hours.
- Restore previous lighting and graphics values when leaving FPS Boost Maximum, while preserving later manual/profile changes.
- Preserve original launch arguments through an update restart.
- Retain the graphics profiles, stability mode, optional local hang diagnostics and storage-path recovery improvements.

## Validation

- 59 automated tests passed on .NET 10.
- Successful standalone Windows-x64 build and startup handoff.
- Persistent BITS transfer interrupted and reattached; final SHA-256 verified.
- Local package feed/update preparation and delta reconstruction checked.
- Settings, saved accounts and active FastFlags retained during local installation checks.

The standalone release uses the normal Snipez-Dev/Claudestrap update channel. Source builds remain protected by the default local-build setting. FastFlag reference entries do not guarantee effect in the running Roblox client.
