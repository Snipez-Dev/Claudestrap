<p align="center">
  <img src="Bloxstrap/Claudestrap.png" alt="Claudestrap" width="100px"/>
</p>

<h1 align="center">Claudestrap</h1>

<p align="center">
  A better way to launch Roblox on Windows — multiple accounts, performance tuning,
  mods and themes, all from one app.
</p>

<p align="center">
  <a href="https://github.com/Snipez-Dev/Claudestrap/releases/latest"><b>Download</b></a> ·
  <a href="#features">Features</a> ·
  <a href="#installation">Installation</a> ·
  <a href="#faq">FAQ</a> ·
  <a href="https://github.com/Snipez-Dev/Claudestrap/issues">Report a problem</a>
</p>

> [!IMPORTANT]
> Claudestrap runs on **Windows 10 and 11**. It is not available for Mac, Linux, or mobile.

## What is Claudestrap?

Claudestrap takes over the job of Roblox's own launcher. You still play the same
Roblox — the difference is what happens around it: you pick which account to play on,
Claudestrap manages the Roblox installation, applies your settings and mods, and
starts the game.

It's a fork of [Bloxstrap](https://github.com/bloxstraplabs/bloxstrap) with a lot of
extra features built on top.

## Features

### Multiple accounts

Save as many Roblox accounts as you like and switch between them from the launch
menu. Sign in through the built-in Roblox login window — username and password,
passkeys and 2FA all work, exactly like on the website.

Your logins are stored encrypted on your own PC, tied to your Windows account. They
are never uploaded anywhere.

### Performance settings

Ready-made tweaks you can turn on with a switch, no config files to edit:

- **Graphics presets** for AMD and NVIDIA cards
- **Performance tweaks** — lighting, textures, rendering and network options
- **RAM limit** — cap how much memory Roblox is allowed to use
- **FPS unlocker** and CPU priority controls
- **FastFlag editor** for anyone who wants to change specific values by hand

### Mods and looks

- Custom **crosshairs**, cursors, textures, sounds and death sounds
- **Themes** for Claudestrap itself, plus an editor for building your own launch screen
- Preset mod packs you can install in one click

### Executor Sync

Roblox updates often, and script executors usually need a few days to catch up. With
Executor Sync turned on, you pick your executor and Claudestrap keeps Roblox on the
version that executor currently supports, instead of updating to the newest one. When
the executor catches up, Claudestrap moves you forward automatically.

It's off by default, and it only decides which Roblox version gets installed —
Claudestrap doesn't run or contain any executor itself.

### Everything else

- **Discord Rich Presence** — show what you're playing, with a join button
- **Activity history** — the games and servers you've been in, with a server browser
- **Shortcuts** to launch straight into a specific game
- **Multi-instance** — run several Roblox clients at once
- **Auto-updates** — Claudestrap tells you when a new version is out and updates itself
  if you say yes

## Installation

1. Download `Claudestrap.exe` from the
   [latest release](https://github.com/Snipez-Dev/Claudestrap/releases/latest)
2. Run it and follow the setup
3. Start Claudestrap — from then on it handles launching Roblox for you

Windows SmartScreen may warn about an unknown publisher on first run. That's because
the app isn't code-signed (certificates cost money); click **More info → Run anyway**,
or build it yourself from the source in this repo if you'd rather not take our word
for it.

## Updating

Claudestrap checks for a newer version each time it starts and asks whether you want
it. Nothing is downloaded or installed without your confirmation.

You can check manually under **Settings → Channel → Check for Updates**, or turn the
automatic check off on the same page.

## FAQ

**Is this a cheat, exploit, or executor?**
No. Claudestrap is a launcher and a settings app. It doesn't inject anything into
Roblox, doesn't touch the anti-cheat, and contains no executor.

**Can I get banned for using it?**
Claudestrap doesn't do anything that breaks Roblox's rules by itself. What you do with
mods, FastFlags or third-party tools is your own responsibility — as with any
third-party Roblox tool, use it at your own discretion.

**Is my Roblox login safe?**
Saved accounts are encrypted with Windows' own protection (DPAPI), tied to your
Windows user, and stored only on your PC. Nothing is sent to us or anyone else — the
login happens directly against Roblox, in a real Roblox login page.

**Do I still need the normal Roblox installation?**
No. Claudestrap downloads and manages its own copy of Roblox.

**Something broke / a game won't start.**
Turn off any FastFlags and mods you've enabled and try again — that's the cause the
vast majority of the time. If it persists,
[open an issue](https://github.com/Snipez-Dev/Claudestrap/issues) and include the log
file from `%LOCALAPPDATA%\Claudestrap\Logs`.

## Building from source

You need the **.NET 9 SDK** and **Windows** — this is a WPF app and won't build on
other platforms.

```sh
git clone https://github.com/Snipez-Dev/Claudestrap.git
cd Claudestrap
dotnet build Claudestrap.sln -c Release
```

The `wpfui` UI library is vendored under [`wpfui/`](wpfui/), so a plain clone is
enough — no submodule setup needed.

To produce the same single-file build the releases ship:

```sh
dotnet publish Bloxstrap/Claudestrap.csproj -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

## License

MIT — see [`LICENSE`](LICENSE). Claudestrap is a fork of Bloxstrap (by way of
Fishstrap), both also MIT-licensed. The vendored [`wpfui`](wpfui/) library is
separately MIT-licensed — see [`wpfui/LICENSE`](wpfui/LICENSE).

> [!NOTE]
> Claudestrap is under active development. Features may change and some things may
> still be unfinished.
