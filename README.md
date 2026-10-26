# Analog Controller Input - Controller Helper

Windows helper for the **Analog Controller Input** Scrap Mechanic mod. It reads Xbox controllers through XInput and Sony **DualShock 4** controllers through native Windows Raw Input, then sends analog controls to the mod at 120 Hz. No administrator access, extra SDK, game-file patch or input injection is required.

## Start

1. Install/subscribe to the mod and enable it in a Creative world.
2. Enter an Analog Driver Seat/Saddle once. A missing-helper prompt shows the download address.
3. Download this repository as a ZIP (GitHub **Code > Download ZIP**) or download its helper Release ZIP. Extract **all** files, then double-click **Start Controller.cmd**. The CMD alone is not enough.
4. Keep the console open. Return to Scrap Mechanic and release both triggers. Fresh helper input closes the prompt automatically.
5. Ctrl+C stops the reader. The mod fails safe if input stops.

Requires Windows PowerShell 5.1, included with Windows. Source is compiled with Windows' included .NET Framework on launch; no .NET SDK or separate driver installer is needed.

Repository/download page: https://github.com/NyxefTheRealOne/SM_Analog_Helper

## Supported controllers

- **Xbox / XInput**: Xbox Series and other XInput-compatible controllers over a supported Windows USB/Bluetooth/wireless connection.
- **Sony DualShock 4 (PS4)**: original and revision 2 Sony models, USB and Bluetooth report formats. Both sticks and L2/R2 remain independent analog axes. Native support does not require DS4Windows or Steam Input emulation.
- DualShock 3, DualSense and third-party PlayStation clones are not claimed as native DS4 devices. A controller exposed by an existing Windows mapper as XInput can still use the XInput backend.

Auto mode prefers a live native DS4, then XInput. If multiple controller types are connected, select a backend explicitly. ControllerIndex selects Xbox slots 0-3 and is left at -1 for DS4. If HidHide/DS4Windows hides the physical controller, use XInput for its mapped Xbox device, or expose the physical DS4 to this helper.

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Controller\Start-Controller.ps1 -Backend DualShock4
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Controller\Start-Controller.ps1 -Backend XInput -ControllerIndex 0
```

## Controls

Left stick steers by default; right stick moves the camera. RT/R2 accelerates, LT/L2 brakes; equal amounts cancel under the default combined trigger axis. LB/L1 shifts down and RB/R1 shifts up in manual mode. A/Cross operates toolbar slot 1. X/Square exits the seat. U on a seat/saddle opens its axis-assignment menu.

**READY** means connected, focused on Scrap Mechanic, triggers released and input being written. **Focus game**, **Release triggers**, **No controller** and **OUTPUT BLOCKED** identify the specific condition preventing control. A source change, disconnect, stale native report or focus loss disarms input; release both triggers after returning.

## Finding the mod

The helper finds local Mods and subscribed Workshop copies across Steam libraries. With multiple copies, it selects the most recently entered seat's mod. The console prints the path. Enter the intended seat and restart if needed, or override:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Controller\Start-Controller.ps1 -ModPath "D:\SteamLibrary\steamapps\workshop\content87990\WORKSHOP-ID"
```

The folder must contain this mod's description.json. Each multiplayer driver needs their own reader. Only one reader can own an installation's input files; close the normal helper before running the recording helper.

## Publishing this helper

This folder is the complete helper repository. Upload its contents to **NyxefTheRealOne/SM_Analog_Helper** and optionally attach `AnalogControllerInput-Helper-1.6.0.zip` to a GitHub Release. The mod already links to this repository. No upload was performed by the local builder.

Native DS4 protocol/Windows listener tests pass; a physical DS4 session remains to be verified on USB and Bluetooth. Code is MIT, see LICENSE. Version 1.6.0.
