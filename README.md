# StarCraft II Campaign Ultrawide

Play StarCraft II single-player campaigns in ultrawide with an adjustable HUD, farther camera zoom, and wider hub views.

[Download the latest release](https://github.com/jrgray933/StarCraft-2-Campaign-Ultrawide/releases/latest)

## Features

- Automatic ultrawide setup when you enter a campaign mission or hub.
- Resolution selection from your display's available modes.
- Centered bottom HUD with **50–100% scaling** in **5% increments**. Objectives and dialogs keep their normal size.
- Aligned minimap, unit panel, command card, menu buttons, cargo slots, and production queues. The bottom corners show the battlefield.
- **Max zoom out:** Default or **+10% to +100%** additional camera distance, in **10% increments**.
- **Zoom steps:** **5 to 20** evenly spaced positions between the closest view and your selected maximum.
- Shadow distance adjusts automatically with the selected maximum zoom.
- **Hub Scale:** Off, Fit, or Expanded. Fit shows more scenery without cropping the normal framing; Expanded adds another 20 degrees of horizontal field of view. Mission framing is controlled separately.
- Light and dark themes, saved settings, and a transparent app icon.
- Detects an already-running game and includes **Recheck game**.

## Getting started

1. Download the **windows-x64.zip** release and extract it to a writable folder.
2. Run **Campaign Ultrawide.exe**.
3. Choose your resolution, HUD size, and camera settings, then click **Enable**.
4. Launch StarCraft II through Battle.net and load a campaign mission. If the game is already running, setup starts automatically.
5. Return to the game for a few seconds to let the changes apply.

Use fullscreen mode. The helper does not launch Battle.net or the game, and you do not need to change the game's resolution manually.

You can adjust HUD size, zoom, zoom steps, and hub framing while playing. Close the game before choosing a different resolution. **Default** restores the normal maximum zoom distance; **Off** restores normal hub framing.

## Controls

- **Recheck game** restarts setup if the game is not detected or changes have stopped applying.
- **Stop automatic setup** stops monitoring and restores normal camera settings. The current resolution and HUD remain until you restore them or close the game.
- **More options** provides HUD and resolution restore controls and the activity log.
- Closing the window while monitoring keeps the helper in the notification area. Use its **Exit** command to quit.

## Requirements

- 64-bit Windows 10 or 11, .NET Framework 4.8, and Windows PowerShell 5.1.
- The 64-bit version of StarCraft II, in fullscreen mode.
- A widescreen resolution supported by your display.

**Single-player campaigns only.** Signing into Battle.net is fine; multiplayer is not supported. This tool modifies game memory, so account enforcement cannot be ruled out. Use at your own risk. It is not affiliated with or endorsed by Blizzard Entertainment.

Game updates may require an updated helper. StarCraft II can be installed in any folder.

## Updating

Close the game and helper before replacing the application with a newer release. Your settings are saved under `%LOCALAPPDATA%\SC2CampaignUltrawide`. The helper does not change your saves or game installation files. Closing the game removes its session changes.

## License

[MIT](LICENSE). StarCraft II and related names are trademarks of their respective owners.
