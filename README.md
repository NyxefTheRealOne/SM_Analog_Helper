# Analog Controller Input helper 1.9.2

Windows XInput + descriptor-driven Raw Input for wheels, joysticks, pedals and controllers. The helper exposes every available control on every detected gaming HID collection. Windows PowerShell 5.1 and .NET Framework are used; no extra SDK, admin access or game patches are required.

**[Download the complete helper from Releases](https://github.com/NyxefTheRealOne/SM_Analog_Helper/releases/latest)**. Use the attached `AnalogControllerInput-Helper-1.9.2.zip`, extract it into a writable folder and keep the folder structure intact. The launcher needs every file in `Controller`, including `HidDevices.cs`.

Extract all files and run **Start Controller.cmd**. Keep it running. Press **U on an Analog Seat/Saddle in game**, select the device and assign its axes/buttons. Choose Xbox/XInput or the DS4 gamepad layout for independent named triggers. Generic HID layouts also expose descriptor controls. Device identities use Windows paths, allowing reconnects to the same port to retain assignments. Devices whose Windows path changes need to be selected again.

Generic analog controls are normalized to -1..1; buttons are 0/1. Use the seat's **AXIS CONFIG MODE** to drag points on the input/output response graph. Flat center sections create deadzones; PEDAL 0 TO 1 and FLIP INPUT provide pedal presets. The seat waits for assigned pedals and buttons to release after selection/reconnect. Camera control is removed; controller buttons can be assigned to connected switches/buttons. Manual gamepad shifts still use LB/RB.

Default Auto mode publishes all gaming devices, including all four connected XInput slots and HID joystick/gamepad/multi-axis/simulation collections. Sony DS4 USB/compact/enhanced Bluetooth decoding is retained alongside generic descriptors. Vendor-only input absent from the Windows HID descriptor requires a compatible Windows mapper. HID control data must be initialized before the seat arms; move each control if needed. Separate USB devices are separate choices, and each seat currently selects one device.

The helper locates local/Workshop installs and other Steam libraries. Enter the intended seat once so its installation is preferred, or use an explicit override:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Controller\Start-Controller.ps1 -ModPath "D:\SteamLibrary\steamapps\workshop\content\387990\WORKSHOP-ID"
```

Optional legacy filters: `-Backend XInput -ControllerIndex 0` selects an Xbox slot; `-Backend DualShock4` disables Xbox polling. Keep the default Auto setting for all devices. Only one helper may write each mod installation. Close the normal helper before using the trailer helper.

Ctrl+C stops input and publishes neutral snapshots. Losing game focus zeros device values; missing heartbeats disarm the seat. Each multiplayer driver runs their own helper and chooses their device in the seat.

Source and packaging tests pass, including signed/unsigned axes, multi-device serialization, 256 buttons, native DS4 fixtures and the 120 Hz file transport. Physical wheel/joystick models and DS4 USB/Bluetooth sessions still require live testing. No uploads are performed by the builder.

Repository: https://github.com/NyxefTheRealOne/SM_Analog_Helper

Code: MIT, see LICENSE.


## Updating from an older helper

Close the previous helper. Extract the complete new release into a new folder and use that folder's Start Controller.cmd. Do not mix the old Controller folder with the new launcher. Enter the mod's seat once before launching the downloaded helper so it can select that installation. The mod itself updates separately through Steam Workshop.

This package is paired with mod 1.9.2. The new Pause Proxy and piston hold behavior run in the mod's Lua scripts; the helper code is unchanged from the immediately preceding 1.9.x bundles. Older published helper installations should still be replaced with this complete package because it includes descriptor-driven HID support.

## Included files

- Start Controller.cmd
- Controller/Start-Controller.ps1
- Controller/Resolve-InputPath.ps1
- Controller/ControllerBridge.cs
- Controller/DualShockInput.cs
- Controller/HidDevices.cs
- README.md, CHANGELOG.md, VERSION and LICENSE

No installer or precompiled EXE is required. Windows PowerShell compiles the included C# source when the launcher starts. Keep its console window open while playing.
