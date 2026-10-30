# Analog Controller Input helper 1.9.2

Windows XInput + descriptor-driven Raw Input for wheels, joysticks, pedals and controllers. The helper exposes every available control on every detected gaming HID collection. Windows PowerShell 5.1 and .NET Framework are used; no extra SDK, admin access or game patches are required.

Download the complete helper as .zip , extract it into a writable folder and keep the folder structure intact. The launcher needs every file in `Controller`, including `HidDevices.cs`.

Extract all files and run **Start Controller.cmd**. Keep it running. Press **U on an Analog Seat/Saddle in game**, select the device and assign its axes/buttons. Choose Xbox/XInput or the DS4 gamepad layout for independent named triggers. Generic HID layouts also expose descriptor controls. Device identities use Windows paths, allowing reconnects to the same port to retain assignments. Devices whose Windows path changes need to be selected again.

Generic analog controls are normalized to -1..1; buttons are 0/1. Use the seat's **AXIS CONFIG MODE** to drag points on the input/output response graph. Flat center sections create deadzones; PEDAL 0 TO 1 and FLIP INPUT provide pedal presets. The seat waits for assigned pedals and buttons to release after selection/reconnect. Camera control is removed; controller buttons can be assigned to connected switches/buttons. Manual gamepad shifts still use LB/RB.

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
