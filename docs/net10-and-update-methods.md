# .NET 10 and update methods

The application and tests target .NET 10. `global.json` selects SDK 10.0.402. The bundled runtime is 10.0.13. The existing vendored WPF UI library remains a compatible net9 class-library reference; the application runs on the .NET 10 runtime.

On this machine the toolchain is stored under the requested shared directory:

- `R:\Entwicklung\Dependencies\dotnet-10`
- `R:\Entwicklung\Dependencies\velopack` (vpk 1.2.161)
- `R:\Entwicklung\Dependencies\nuget-packages`

Use `scripts/build.ps1 -Mode Test` or `scripts/build.ps1 -Mode Publish`. Override `-Dependencies` for another machine. The scripts set process-scoped DOTNET_ROOT and NUGET_PACKAGES; no global PATH change is required.

Periodic forced garbage collection has been removed from the launcher. Optional Discord initialization is deferred to dispatcher idle, and automatic update checks begin after the launch menu has rendered. Diagnostics report settings readiness and launch-menu rendering time after managed entry. External process-to-window measurements also include work before managed entry.

Single-file publishing keeps native-library extraction enabled and full-content extraction disabled. The comparison switch `scripts/build.ps1 -Mode Publish -ExtractAll -OutputDirectory <folder>` is available for compatibility measurements. Full extraction was slower at first launch in the local comparison; the small warm-start sample was similar. This is a startup measurement, not a claim about Roblox frame rates or lower process memory.

The FastFlag editor and settings expose a dated Roblox allowlist reference and explicitly distinguish listing from observed client effect. Unknown entries are not labelled currently supported. The reference is based on the published announcement dated 2025-09-29; it is not a verified live allowlist and is never used to silently remove settings. No unmeasured crash percentages are calculated from the number of flags.

## Persistent standalone downloads

The standalone updater uses BITS when available. A digest- and destination-specific Windows job retains its transfer state independently of the launcher. A small PowerShell helper reports progress, reconnects to an existing matching job, and exits when its parent process ends. The Windows job remains resumable. Only matching jobs are inspected or completed; downloads are verified against the release size and SHA-256 before installation. If BITS is unavailable or rejects the transfer, the HTTP downloader uses a separate persistent file. Partial downloads survive a launcher restart. Existing completed downloads are verified and reused.

The normal local-build protection still blocks replacement with Snipez's standard binary. Adding a transport does not change the selected update source or opt a customized build into upstream replacement.

## Package/delta updates

Velopack 1.2.161 is bootstrapped before WPF initialization. `scripts/package.ps1 -Version <semver> -PublishDirectory <folder> -OutputDirectory <release-folder>` builds a portable package, installer, release index and deltas from the previous full package in that folder. The default channel is `local`. Packages are generated locally; the script does not publish them.

Packaged instances use their configured package feed and channel under Settings → Eigener Paket-Updatekanal. HTTPS feeds and existing local release directories are accepted. Ordinary standalone instances retain the existing EXE path and their protection against stock updates. Existing configuration and Roblox storage stay outside versioned package contents. A fresh packaged instance uses a UserData directory outside its current application folder.

The package pipeline was checked by creating two local test package versions, reconstructing the newer package from the delta, comparing all entry hashes, and using Velopack's test locator to check the feed and prepare an update without modifying an installed application.
