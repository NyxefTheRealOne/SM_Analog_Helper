# Analog Drive Controller Helper

Windows controller reader for the **Analog Controller Input** Scrap Mechanic mod. Full stick and trigger values are read through Windows XInput and sent to the mod at 120 Hz. No administrator access, extra SDK, game-file patch or input injection is required.

## Start

1. Subscribe to/install Analog Drive and enable it in a Creative world.
2. Enter an Analog Driver Seat or Saddle once. If the helper is missing, a download prompt appears.
3. Download the helper **ZIP**, extract **all** files together, then double-click **Start Controller.cmd**. Downloading the CMD alone is insufficient.
4. Keep the console open. Return to Scrap Mechanic and release both triggers once. The prompt closes when fresh helper frames arrive.
5. Press Ctrl+C or close the console to stop. Vehicle controls fail safe if the reader stops.

Requires Windows PowerShell 5.1 (included with Windows) and an XInput-compatible controller. Xbox Series controllers work over a supported Windows USB/Bluetooth/wireless connection. The helper does not configure Steam Input mappings.

The helper finds local mods and subscribed Workshop copies in Steam libraries, including secondary libraries. If several copies exist, it chooses the copy whose seat was most recently entered. The console prints the chosen path. If it chose the wrong copy, enter the intended seat and restart, or select the folder explicitly:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Controller\Start-Controller.ps1 -ModPath "D:\SteamLibrary\steamapps\workshop\content\387990\WORKSHOP-ID"
```

The folder must contain Analog Drive's description.json. Each multiplayer driver needs their own helper. Keep only one helper running for a mod installation. A second writer is refused rather than overwriting the first reader's input.

## Controls and state

Both sticks and triggers are sent independently. The default engine axis is RT - LT; equal trigger values cancel. LB/RB shift down/up in manual mode, A operates toolbar slot 1, and X exits the seat. Axis assignments are configured in the mod's seat menu using U.

**READY** means a controller is connected, the game has focus, the triggers have been released and input is being written. **Focus game**, **Release triggers**, **No Xbox controller** and **OUTPUT BLOCKED** describe the specific condition preventing control. Focus loss disarms output; release both triggers after returning.

## Repository / releases

This folder is the complete production helper repository. Commit its contents to GitHub. Create a release and upload `AnalogDrive-Helper-1.5.0.zip` from the project's dist folder as a release asset. Users should download/extract the ZIP and run its CMD. Do not publish only Start Controller.cmd.

The mod currently uses a requested placeholder:
`https://github.com/YOUR-ACCOUNT/analog-drive-helper/releases/latest`

Replace `helperDownloadUrl` in the mod's Scripts/Config.lua with your real release URL, then rebuild the Workshop folder before publishing. The helper makes no web requests; this URL is only displayed by the mod.

License: MIT, see LICENSE. Version: 1.5.0.
