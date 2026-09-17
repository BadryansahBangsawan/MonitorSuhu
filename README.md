<div align="center">

# 🌡️ MonitorSuhu

**Lightweight hardware temperature overlay for macOS, Windows, and Linux.**  
Always-on-top HUD with NVIDIA-style dark theme — drag to any corner, snap to edges, survives display changes.

<br/>

[![Latest Release](https://img.shields.io/github/v/release/BadryansahBangsawan/MonitorSuhu?style=flat-square&color=76B900&label=latest)](https://github.com/BadryansahBangsawan/MonitorSuhu/releases/latest)
[![macOS](https://img.shields.io/badge/macOS-14%2B-black?style=flat-square&logo=apple)](https://github.com/BadryansahBangsawan/MonitorSuhu/releases/latest)
[![Windows](https://img.shields.io/badge/Windows-10%2F11-0078D4?style=flat-square&logo=windows)](https://github.com/BadryansahBangsawan/MonitorSuhu/releases/latest)
[![Linux](https://img.shields.io/badge/Linux-x64-FCC624?style=flat-square&logo=linux&logoColor=black)](https://github.com/BadryansahBangsawan/MonitorSuhu/releases/latest)

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

Each OS has its own file on the same GitHub Release. The in-app updater only follows a tag that ships **this** platform’s file.

| Platform | File | Requirements |
|---|---|---|
| **macOS** | `MonitorSuhu-*-macos.dmg` | macOS 14 Sonoma or later (Apple Silicon + Intel) |
| **Windows** | `MonitorSuhu-*-windows-x64.exe` | Windows 10 / 11 (64-bit), Administrator |
| **Linux** | `MonitorSuhu-*-linux-x64.tar.gz` | x64, `/sys/class/hwmon` |

👉 **[Go to Releases →](https://github.com/BadryansahBangsawan/MonitorSuhu/releases/latest)**

---

## 🍎 macOS — Installation

1. Download `MonitorSuhu-*-macos.dmg` from [Releases](https://github.com/BadryansahBangsawan/MonitorSuhu/releases/latest)
2. Open the DMG. The volume **is** `MonitorSuhu.app` — drag that into **Applications**
3. On first launch, macOS may block the unsigned build. Run once:

   ```bash
   xattr -cr /Applications/MonitorSuhu.app && open /Applications/MonitorSuhu.app
   ```

   Or: right-click the app → **Open** → **Open** again. Still blocked? **System Settings → Privacy & Security → Open Anyway**.

4. A **Suhu** item appears in the menu bar. Click it to show/hide the overlay or open Settings.

> **Tip:** Click the Dock icon to reopen Settings. The HUD stays when Settings closes.

### Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| `⌃ ⇧ T` | Show / hide overlay (rebindable in Settings) |
| `⌃ ⇧ E` | Unlock overlay for dragging (rebindable in Settings) |

### Notes

- Temps are live HID on Apple Silicon (`IOHIDEventSystemClient`) or SMC on Intel. HUD labels are name-token picks (CPU prefers pACC / eACC / SoC over PMU tdie). BOARD is not the battery. Missing GPU usually means no HID name contains `gpu` / `agx`.
- RAM, CPU%, GPU%, and PWR stay off until you enable them **and** hardware reports them.
- **Start with macOS** uses `SMAppService` — the first enable may prompt **System Settings → General → Login Items**.
- Native fullscreen hides the HUD while **Hide in fullscreen** is on. Screen-share hide needs Screen Recording on macOS 14+; MonitorSuhu never prompts for it.

---

## 🪟 Windows — Installation

1. Download `MonitorSuhu-*-windows-x64.exe` from [Releases](https://github.com/BadryansahBangsawan/MonitorSuhu/releases/latest)
2. Run the installer — **UAC / Administrator** is required so hardware sensor drivers can open
3. The overlay appears in the top-right corner; a tray icon appears in the notification area

### Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl + Shift + T` | Show / hide overlay (rebindable in Settings) |
| `Ctrl + Shift + E` | Unlock overlay for dragging (rebindable in Settings) |

### Notes

- Sensors are read via [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor). Antivirus may flag its kernel driver — allow it if the HUD is empty.
- A second launch exits immediately (one overlay instance).
- **Start with Windows** creates a Task Scheduler task at highest privileges. Applied when you press **Save** in Settings.
- RAM temperature is rare on consumer DIMMs. CPU load, GPU load, and power stay off until you enable them.

---

## 🐧 Linux — Installation

1. Download `MonitorSuhu-*-linux-x64.tar.gz` from [Releases](https://github.com/BadryansahBangsawan/MonitorSuhu/releases/latest)
2. Extract and install:

   ```bash
   tar -xzf MonitorSuhu-*-linux-x64.tar.gz
   bash install.sh
   ```

   That copies the app to `~/.local/opt/MonitorSuhu` and adds a desktop entry. Or run `./MonitorSuhu` from the extracted folder.

### Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl + Shift + T` | Show / hide overlay (rebindable in Settings) |
| `Ctrl + Shift + E` | Unlock overlay for dragging (rebindable in Settings) |

### Notes

- Temps come from `/sys/class/hwmon` (and `/sys/class/thermal` if hwmon is empty). No dummy °C.
- A second launch exits immediately (`/tmp/id.monitorsuhu.lock`).
- **Start with Linux** uses the same JSON flag as Windows autostart.
- Settings live in `~/.config/MonitorSuhu/settings.json`.

---

## ⚙️ Settings

All three platforms share the same settings surface:

| Setting | Description |
|---|---|
| **Sensors** | Toggle CPU / GPU / SSD / Board / RAM / Fan, plus optional CPU%, GPU%, PWR |
| **Assignments** | Pin a HUD row to a named sensor, or leave Auto |
| **Thresholds** | Warn (yellow) and critical (red) per sensor. Fan uses RPM; load uses %; power uses W |
| **Alerts** | Notify when a visible reading crosses critical; mute 15 minutes from tray or Settings |
| **Shortcuts** | Click a chord pill and press a modifier plus a letter or number. Esc cancels |
| **Profiles** | Desktop / Game / Silent looks. Editing sensors or appearance marks Custom |
| **Appearance** | Compact one-line HUD, 30-sample sparkline, opacity, font, accent, °C / °F |
| **Position** | Corner presets or drag-to-place with edge snap |
| **Auto-hide** | Hide during exclusive fullscreen (and capture where the OS can tell). Show overlay stays on |
| **Poll interval** | How often sensors are read (400 ms – 3 s, default 1 s) |
| **Start with OS** | Launch automatically on login |

Overlay position is stored as **relative corner edges**, so resizing the font or adding a sensor row keeps a corner HUD anchored. On Windows, position is DPI-aware — 125% / 150% scaling does not park the HUD off-screen.

**Settings file locations:**

- macOS: `~/Library/Application Support/MonitorSuhu/settings.json`
- Windows: `%AppData%\MonitorSuhu\settings.json`
- Linux: `~/.config/MonitorSuhu/settings.json`

---

## 🔄 Updates

On launch, MonitorSuhu lists GitHub Releases and picks the newest tag that includes **this OS** file (macOS `.dmg`, Windows `.exe`, Linux `.tar.gz`). A Windows-only tag is not an update on Mac.

When a newer build exists, Settings shows a banner (and a tray balloon on Windows). **Install …** downloads that file with a progress bar, then the app closes to finish. Open it again from Applications / Start menu / the desktop entry if it does not restart.

- **macOS:** replaces `/Applications/MonitorSuhu.app`; settings stay in Application Support
- **Windows:** runs the setup silently and relaunches; settings stay in `%AppData%`
- **Linux:** extracts the tarball over the running folder; settings stay in `~/.config/MonitorSuhu`

---

<div align="center">

Made with ♥ for people who want to know how hot their machine is.

</div>
