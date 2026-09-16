# MonitorSuhu

NVIDIA-style temperature HUD for **macOS** and **Windows**. Small dark overlay, green accent bar, always on top, drag to any corner.

## Install

### macOS

1. Download `MonitorSuhu-*-macos.dmg` from [Releases](https://github.com/BadryansahBangsawan/MonitorSuhu/releases).
2. Open the DMG and drag **MonitorSuhu** onto **Applications**.
3. First launch (unsigned GitHub build):

```bash
xattr -cr /Applications/MonitorSuhu.app
open /Applications/MonitorSuhu.app
```

Or right-click the app → **Open**. If macOS still blocks it: **System Settings → Privacy & Security → Open Anyway**.

4. The thermometer icon appears in the Dock and Launchpad. Menu bar extra is **Suhu**. Click the Dock icon again to open Settings.

Build a DMG yourself:

```bash
cd macos
make dmg
# → macos/dist/MonitorSuhu-1.0.1-macos.dmg
```

### Windows

1. Download `MonitorSuhu-*-windows-x64.exe` from [Releases](https://github.com/BadryansahBangsawan/MonitorSuhu/releases).
2. Run the installer (UAC / Administrator is required so hardware sensors can open).
3. Overlay appears top-right; tray icon in the notification area.

Build the installer yourself (needs [.NET 8 SDK](https://dotnet.microsoft.com/download) + [Inno Setup 6](https://jrsoftware.org/isinfo.php)):

```powershell
cd windows
.\installer\build.ps1
# → windows\dist\MonitorSuhu-1.0.1-windows-x64.exe
```

Portable exe without installer: `.\publish.ps1` → `windows\dist\win-x64\MonitorSuhu.exe`.

Sensor rows that the machine does not actually expose (RAM on most PCs, motherboard SuperIO on many laptops) are hidden, not shown as `N/A`.

```
│ CPU          58°C
│ GPU          64°C
│ SSD          42°C
│ BOARD        36°C
```

## macOS

Native Swift / AppKit menu-bar app. Reads Apple Silicon HID temperature sensors (`IOHIDEventSystemClient`, usage page `0xff00` / usage `0x0005`). Intel Macs fall back to SMC keys.

```bash
cd macos
make run
```

Build only: `make app` → `macos/build/MonitorSuhu.app`

Menu bar extra `Suhu` → Show/Hide, Edit Layout, Settings, Quit.

| Shortcut | Action |
|---|---|
| ⌃⇧T | Show / hide overlay |
| ⌃⇧E | Unlock overlay so you can drag it |

Unlock, drag, it snaps to corners. Position is saved per display.

**Notes**

- This is a desktop HUD, not a game-capture overlay. Exclusive fullscreen games can cover it.
- RAM temperature only appears if a HID/SMC sensor exists.
- Start with macOS uses `SMAppService` (macOS 13+). The first enable may ask you to allow a login item in System Settings.
- Unsigned local build: macOS may ask you to allow it under Privacy & Security.

## Windows

C# / WPF on .NET 8. Sensors via [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor). Needs **Administrator** so CPU/motherboard drivers can open.

On a Windows PC with the .NET 8 SDK:

```powershell
cd windows
dotnet restore
dotnet run --project src\MonitorSuhu.App
```

Single-file exe (no SDK on the target PC):

```powershell
cd windows
.\publish.ps1          # win-x64
.\publish.ps1 -Arch arm64
```

Output: `windows/dist/win-x64/MonitorSuhu.exe`

| Shortcut | Action |
|---|---|
| Ctrl+Shift+T | Show / hide overlay |
| Ctrl+Shift+E | Unlock overlay so you can drag it |

Tray icon → Show/Hide, Edit Layout, Settings, Exit.

A second launch exits immediately (one overlay only). Global hotkeys are registered on a message-only window so hiding the HUD does not drop Ctrl+Shift+T. If another app already owns those chords, a tray balloon says so.

Start with Windows registers a **Task Scheduler** task at highest privileges so the overlay still has admin after reboot (a normal Startup-folder shortcut would lose elevation). The task is created or removed when you press **Save**, not while dragging sliders.

**Notes**

- Exclusive fullscreen games hide this overlay. Use borderless windowed.
- RAM temperature is rare on consumer DIMMs.
- Antivirus may flag LibreHardwareMonitor’s kernel driver. Allow it if the HUD stays empty — Settings also shows the Open() error instead of a silent `NO SENSORS`.
- First launch triggers UAC.

## Settings (both)

- Which sensors to show
- Warn / critical colors (green → yellow → red) — per CPU / GPU / SSD / board
- Opacity, font size, accent color (NVIDIA green / cyan / white / orange), °C / °F
- Corner presets and drag-to-place
- Poll interval
- Start with OS

Overlay position is stored as relative edges (not only top-left), so growing the font or adding a sensor row keeps a corner HUD on that corner. Windows also converts the monitor work area through the current DPI so 125% / 150% scaling does not park the HUD off-screen.

Settings file:

- macOS: `~/Library/Application Support/MonitorSuhu/settings.json`
- Windows: `%AppData%\MonitorSuhu\settings.json`

## Layout

```
macos/          Swift overlay you can run on this Mac
windows/        WPF overlay, build on a Windows PC
```
