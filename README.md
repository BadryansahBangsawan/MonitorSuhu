<div align="center">

# 🌡️ MonitorSuhu

**Lightweight hardware temperature overlay for macOS and Windows.**  
Always-on-top HUD with NVIDIA-style dark theme — drag to any corner, snap to edges, survives display changes.

<br/>

[![Latest Release](https://img.shields.io/github/v/release/BadryansahBangsawan/MonitorSuhu?style=flat-square&color=76B900&label=latest)](https://github.com/BadryansahBangsawan/MonitorSuhu/releases/latest)
[![macOS](https://img.shields.io/badge/macOS-14%2B-black?style=flat-square&logo=apple)](https://github.com/BadryansahBangsawan/MonitorSuhu/releases/latest)
[![Windows](https://img.shields.io/badge/Windows-10%2F11-0078D4?style=flat-square&logo=windows)](https://github.com/BadryansahBangsawan/MonitorSuhu/releases/latest)

<br/>

```
┃ CPU          58°C
┃ GPU          64°C
┃ SSD          42°C
┃ BOARD        36°C
```

</div>

---

## ⬇️ Download

| Platform | File | Requirements |
|---|---|---|
| **macOS** | `MonitorSuhu-*-macos.dmg` | macOS 14 Sonoma or later |
| **Windows** | `MonitorSuhu-*-windows-x64.exe` | Windows 10 / 11 (64-bit) |

👉 **[Go to Releases →](https://github.com/BadryansahBangsawan/MonitorSuhu/releases/latest)**

---

## 🍎 macOS — Installation

1. Download `MonitorSuhu-*-macos.dmg` from [Releases](https://github.com/BadryansahBangsawan/MonitorSuhu/releases/latest)
2. Open the DMG and drag **MonitorSuhu** into **Applications**
3. On first launch, macOS may block the app (unsigned build). To open it:

   ```bash
   xattr -cr /Applications/MonitorSuhu.app && open /Applications/MonitorSuhu.app
   ```

   Or: right-click the app → **Open** → **Open** again. Still blocked? **System Settings → Privacy & Security → Open Anyway**.

4. A **Suhu** icon appears in the menu bar. Click it to show/hide the overlay or open Settings.

> **Tip:** Click the Dock icon at any time to reopen the Settings window.

### Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| `⌃ ⇧ T` | Show / hide overlay |
| `⌃ ⇧ E` | Unlock overlay for dragging |

### Notes

- Sensors are read natively via Apple Silicon HID (`IOHIDEventSystemClient`) or SMC keys on Intel.
- RAM temperature only appears when a hardware sensor reports it.
- **Start with macOS** uses `SMAppService` — the first enable may prompt you in **System Settings → General → Login Items**.
- This is a desktop HUD, not a game capture overlay. Exclusive fullscreen games may cover it.

---

## 🪟 Windows — Installation

1. Download `MonitorSuhu-*-windows-x64.exe` from [Releases](https://github.com/BadryansahBangsawan/MonitorSuhu/releases/latest)
2. Run the installer — **UAC / Administrator** is required so hardware sensor drivers can open
3. The overlay appears in the top-right corner; a tray icon appears in the notification area

### Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl + Shift + T` | Show / hide overlay |
| `Ctrl + Shift + E` | Unlock overlay for dragging |

### Notes

- Sensors are read via [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor). Antivirus software may flag its kernel driver — allow it if the HUD shows no readings.
- A second launch exits immediately (only one overlay instance runs at a time).
- **Start with Windows** creates a Task Scheduler task at highest privileges so the overlay retains sensor access after reboot. The task is applied when you press **Save** in Settings.
- RAM temperature is rare on most consumer DIMMs.
- Use **borderless windowed** mode in games — exclusive fullscreen will cover the overlay.

---

## ⚙️ Settings

Both platforms share the same settings surface:

| Setting | Description |
|---|---|
| **Sensors** | Toggle CPU / GPU / SSD / Board / RAM / Fan rows |
| **Assignments** | Pin a HUD row to a named sensor, or leave Auto |
| **Thresholds** | Warn (yellow) and critical (red) per sensor. Fan uses RPM |
| **Appearance** | Compact one-line HUD, 30-sample sparkline, opacity, font, accent, °C / °F |
| **Position** | Corner presets or drag-to-place with edge snap |
| **Poll interval** | How often sensors are read (400 ms – 3 s, default 1 s) |
| **Start with OS** | Launch automatically on login |

Overlay position is stored as **relative corner edges**, so resizing the font or adding a sensor row keeps a corner HUD anchored to that corner. On Windows, position is DPI-aware — 125% / 150% scaling does not park the HUD off-screen.

**Settings file locations:**

- macOS: `~/Library/Application Support/MonitorSuhu/settings.json`  
- Windows: `%AppData%\MonitorSuhu\settings.json`

---

## 🔄 Updates

MonitorSuhu checks [GitHub Releases](https://github.com/BadryansahBangsawan/MonitorSuhu/releases) automatically on launch. When a new version is available, a banner appears in Settings and a tray notification pops up on Windows.

**Install from the app** on macOS and Windows: Settings → **Install …** downloads the matching DMG or setup exe and replaces the current install. Settings in `Application Support` / `%AppData%` stay put. Linux still opens the GitHub page until a packaged tarball ships with the release.

---

<div align="center">

Made with ♥ for people who want to know how hot their machine is.

</div>
