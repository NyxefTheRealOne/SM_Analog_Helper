# Changelog

## 1.9.2

Complete helper package paired with Analog Controller Input 1.9.2.

- Includes descriptor-driven Windows HID device discovery, reported axes/buttons and device identities, alongside XInput and DualShock 4 input.
- Includes HidDevices.cs and all launcher dependencies. Replace older downloaded helper folders in full.
- Supports discovery of local and Workshop mod installations, additional Steam libraries and explicit -ModPath selection.
- Buffered input snapshots, neutral shutdown, focus-loss handling and duplicate-reader protection are included.
- Updates the download/setup instructions and release documentation.

The helper implementation is unchanged from the immediately preceding 1.9.x bundles. The optional Pause Proxy, zero piston initialization and paused-piston hold fix are changes to the mod, delivered through Steam Workshop.
