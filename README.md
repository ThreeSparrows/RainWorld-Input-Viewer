[README.md](https://github.com/user-attachments/files/28131843/README.md)
# RainWorld Input Viewer

A real-time keyboard / gamepad input visualization tool for **Rain World**.

It displays your inputs (directional keys, jump, throw, etc.) on screen as you play, letting you visually check the timing, offset, and trajectory of your inputs.

**Main uses:**

- Game input analysis
- Input accuracy verification
- Playthrough review assistance

---

## Key Features

- (Pseudo) analog display of input direction
- Visualization of input trajectory
- Timing display for Jump, Throw, Grab, Fast Roll, and Special
- Screenshot function
- Customizable key bindings and tool settings (via `ConfigrationTool.exe`). Changes take effect immediately when the configuration tool is run alongside the Input Viewer.
- Basic Input Log (not synchronized with Rain World)
- Input Display mode (OBS-compatible overlay)

---

## System Requirements

- Windows 11
- .NET Framework 4.7 or later

---

## How to Use

1. **Launch the application.** Your keyboard inputs will be displayed on screen. The window appears on top of all other windows by default (this can be changed in the settings).

2. **Move the window** by left-clicking and dragging it.

3. **Directional keys (WASD)** are shown as a trail line.

4. **Action buttons** are shown as colored dots at the moment they are pressed:
   - Jump — red dot (the trail line also turns red while held)
   - Throw — green dot
   - Grab — yellow dot
   - Special — purple dot
   - Fast Roll *(requires the FastRoll MOD)* — orange dot

5. If there is no input for 0.1 seconds and then a new input occurs, the existing trail is cleared.

6. **Screenshots:** Press `1`, `2`, or `3` to save a screenshot of the application window. Each key saves under a different name (Perfect / Good / Failed). Screenshots are saved in the `screenshot` folder located in the same directory as the executable.

7. Press `M` to exit the application.

8. Press `P` to toggle the basic Input Log display on/off. (The FPS shown reflects the configured input polling interval — 10 ms by default.)

9. To change key bindings or other settings, use the included `ConfigrationTool.exe`.

10. **To use a gamepad,** change the device type to *Gamepad* in `ConfigrationTool.exe`.
    > Note: Even when using a gamepad, screenshots, the Input Log, and quitting the application can only be done via keyboard input.

11. **Input Display mode** (optional) can be enabled from the *Input Display* tab in `ConfigrationTool.exe`.
    - Compatible with OBS and similar software.
    - Screenshot and Input Log features are not available in this mode.
    - To exit, press the key assigned to quit — same as in normal mode.

---

## Installation

1. Download the latest `.zip` from the [Releases](../../releases) page.
2. Extract the ZIP file to any folder.
3. Run the executable.

> **Note on Windows SmartScreen:** Since this application is not code-signed, Windows SmartScreen may display a warning when you first run it. This is expected for unsigned indie tools. The full source code is available in this repository for review. If you prefer, you can build it yourself from source.

---

## Notes & Disclaimer

- Use this software at your own risk. The developer assumes no responsibility for any issues arising from its use.
- This tool includes a feature modeled after the Rain World MOD **InputLog**, but the Input Log in this tool does **not** match the MOD's Input Log. Please treat it as a rough reference only.
- Only **XInput-compatible** gamepads are supported. For controllers such as the DualSense, please use software like DS4Windows.

---

## License / Copyright

Copyright (c) 2026 ThreeSparrows. **All Rights Reserved.**

The source code is published in this repository for transparency and review only. It is **not** released under an open-source license.

- Redistribution or reposting of this software (in whole or in part) without permission is **prohibited**.
- Distribution of modified versions of this software is **prohibited**.

If you would like to use this software or its code beyond viewing it here, please contact the author.

---

## Contact

- To report bugs, please use this repository's [Issues](../../issues) page, or contact via the distribution page.
- X (Twitter): [@suzume367](https://x.com/suzume367)
