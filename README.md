<p align="center">
  <img src="Bloxstrap/Claudestrap.png" alt="Claudestrap" width="100px"/>
</p>

<h1 align="center">Claudestrap</h1>

<p align="center">
  A modified, feature-extended fork of the <a href="https://github.com/bloxstraplabs/bloxstrap">Bloxstrap</a> Roblox bootstrapper for Windows.
</p>

<p align="center">
  <a href="https://github.com/Snipez-Dev/Claudestrap/releases/latest">Latest release</a> ·
  <a href="https://github.com/Snipez-Dev/Claudestrap/issues">Issues</a> ·
  <a href="#building-from-source">Building from source</a>
</p>

> [!IMPORTANT]
> Claudestrap supports **Windows 10 and above** only.

## What is this?

Claudestrap replaces the standard Roblox bootstrapper with one that manages its own
Roblox installation, gives you a custom launch UI, and adds a pile of extra features
on top of stock Bloxstrap:

- Multi-account manager — sign in through an embedded Roblox login page or a pasted
  `.ROBLOSECURITY` cookie, save multiple accounts, and switch between them from the
  launch menu without leaving the app
- Executor version sync — optionally pin the installed Roblox build to whatever
  version your selected script executor currently supports (via WEAO/RDD), instead
  of always tracking the live release
- Discord Rich Presence, an activity/server history log, FastFlag and mod editors,
  a custom bootstrapper style/theme editor, and the usual Bloxstrap settings surface
- Self-updating: checks a plain [`version.txt`](version.txt) in this repo on launch
  and prompts to upgrade when a newer build is available (see [Updating](#updating))

## Installation

1. Download the latest installer from the
   [releases page](https://github.com/Snipez-Dev/Claudestrap/releases/latest)
2. Run it and finish the setup
3. Launch Claudestrap

## Updating

Claudestrap checks [`version.txt`](version.txt) in this repo against its own build
version on every launch. If a newer version is available, you'll be asked whether to
upgrade now or stay on your current version — nothing downloads or installs without
that confirmation. You can also trigger a check manually from **Settings → Channel →
Check for Updates**, and turn the automatic check off entirely from the same page.

## Building from source

Requirements: **.NET 9 SDK** and **Windows** (this is a WPF app; it won't build on
other platforms).

```sh
git clone https://github.com/Snipez-Dev/Claudestrap.git
cd Claudestrap
dotnet build Claudestrap.sln -c Release
```

The `wpfui` UI library this project depends on is vendored directly under
[`wpfui/`](wpfui/) — no submodule init step needed, a plain clone is enough to build.

To produce a self-contained single-file build the same way the release workflow does:

```sh
dotnet publish Bloxstrap/Claudestrap.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

## Is it safe?

Claudestrap doesn't inject cheats or bypass Roblox's anti-cheat on its own — it's a
launcher and configuration manager. The executor-sync feature is opt-in and only
pins which Roblox build gets installed; it doesn't do anything by itself. As with any
third-party Roblox tool, use it at your own discretion, and feel free to read the
source yourself — that's what it's here for.

## License

MIT — see [`LICENSE`](LICENSE). Claudestrap is a fork of Bloxstrap (by way of
Fishstrap), both also MIT-licensed. The vendored [`wpfui`](wpfui/) library is
separately MIT-licensed — see [`wpfui/LICENSE`](wpfui/LICENSE).

> [!NOTE]
> Claudestrap is under active development. Features may change and some things may
> still be unfinished.
