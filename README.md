# Starcraft-2-Campaign-Ultrawide

A portable Windows helper for playing StarCraft II single-player campaigns in ultrawide, with a centered, adjustable bottom HUD.

## Features

- Automatic resolution and HUD setup when a supported campaign mission is detected.
- Resolution selection using the primary display's available modes.
- Bottom HUD scaling from **50% to 125%**, in **5% increments**.
- Centered minimap, unit panel, command card, and menu buttons, with the battlefield visible in the bottom corners.
- Scaled cargo slots and correctly positioned production queues.
- Detection of an already-running game, plus a **Recheck game** button.
- Session-only changes and options to restore the original HUD and resolution.

## Requirements and compatibility

- 64-bit Windows 10 or 11, .NET Framework 4.8, and Windows PowerShell 5.1.
- **StarCraft II (64-bit)**, running as `SC2_x64.exe` in fullscreen mode.
- A supported widescreen display mode. Larger HUD sizes need enough horizontal space to fit.

The helper does not reject a game solely because its version number changed. It retains executable, hook, and campaign-state checks. The current hooks use fixed memory addresses, so a game update that changes the internal layout can still stop the fix from working or cause a crash. Future-update compatibility is not guaranteed. Install StarCraft II wherever you prefer; the helper identifies the running executable rather than assuming an installation directory.

Objectives and dialogs retain their normal size.

**Single-player campaigns only.** Application requires both an offline gameplay-session flag and a map path under `Maps/Campaign/`. Signing into Battle.net is separate from that gameplay-session flag. These checks are not a guarantee against detection or account enforcement: this tool modifies game memory. It is not affiliated with or endorsed by Blizzard Entertainment.

## Use

1. Download the Windows release ZIP from this repository's Releases page and extract it to a writable folder.
2. Run **Campaign Ultrawide.exe**. No installation in the game directory is necessary.
3. Choose your resolution and bottom HUD size, then click **Enable**. If the game is already open, setup starts automatically.
4. Launch StarCraft II yourself, load a campaign mission, and leave the mission visible for a few seconds.

The helper applies the resolution and HUD without manual resolution toggles. Change HUD size while enabled; return to the mission to see the result. Close StarCraft II before changing the selected resolution.

If setup stalls, choose **Recheck game**. **Stop automatic setup** stops monitoring but leaves the current session changes in place. **More options** includes restore controls and the activity log. Closing the window while monitoring keeps the helper in the notification area; use its **Exit** command to quit.

Before upgrading the helper, close both the game and the helper. Existing injected session changes cannot be transferred automatically between engine versions. Closing the game removes all session changes.

Settings and extracted runtime files are stored under `%LOCALAPPDATA%\SC2CampaignUltrawide`. Save games and game installation files are not changed. Leaving campaign blocks further application; an existing resolution can remain until the next graphics reset or game exit.

## Build

From a checkout, run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Build.ps1
```

Output: `build/Campaign Ultrawide.exe`. The build uses the .NET Framework compiler included with Windows and embeds exactly the required engine files. There are no downloaded package dependencies.

For Visual Studio, open `Starcraft-2-Campaign-Ultrawide.csproj` with the .NET Framework 4.8 targeting pack installed. Use the **x64 / Release** configuration. `Build.ps1` is the reference release build.

## Test and package

```powershell
# Native mock tests, eligibility checks, and interactive layout tests
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tests\Run.ps1

# Headless environment (no interactive desktop)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Tests\Run.ps1 -SkipUi

# Build, test, then produce release ZIPs and SHA-256 checksums
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Release.ps1
```

Tests execute against local mock memory, not a running game's memory. Layout tests open a temporary test window. Test output and release packages go in ignored build directories. GitHub Actions builds, runs the headless checks, and uploads packages as workflow artifacts; it does not publish a release automatically.

## Project layout

- `Launcher.cs`: Windows Forms interface, settings, status, and worker management.
- `Engine/`: campaign eligibility, resolution refresh, native HUD callbacks, and worker scripts. C# engine sources are embedded and compiled by the worker at runtime.
- `Tests/`: native eligibility tests and the test runner. Additional low-level mock tests live beside the engine code they exercise.
- `Build.ps1` / `Release.ps1`: repeatable build and packaging commands.

See [CONTRIBUTING.md](CONTRIBUTING.md) for changes and test expectations, and [CHANGELOG.md](CHANGELOG.md) for release notes.

## License

[MIT](LICENSE). StarCraft II and related names are trademarks of their respective owners. No game assets or game binaries are included.
