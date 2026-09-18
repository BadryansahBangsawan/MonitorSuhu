<div align="center">

# 🌡️ MonitorSuhu

**Always-on-top hardware temperature HUD for macOS, Windows, and Linux.**  
NVIDIA-style dark overlay — drag to a corner, snap to edges, survives display changes.

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

The `58°C` block is example layout only. Runtime numbers come from the machine.

</div>

---

## What you get

| Piece | Behavior |
|---|---|
| **HUD** | Always-on-top overlay. CPU / GPU / SSD / BOARD / RAM / FAN. Optional CPU%, GPU%, PWR. |
| **Extra / tray** | CPU only: `Suhu 46°` (no `C`/`F` suffix). Missing CPU → `Suhu`. |
| **Lock** | Clicks pass through the HUD. Unlock to drag; it snaps to edges. |
| **Compact** | One line, worst-threshold color (RPM is never compared to °C). Default off. The color follows the hottest enabled °C row among CPU/GPU/SSD/BOARD — fan RPM is excluded from that pick. |
| **Sparkline** | Last 30 polls on stacked rows. Default off. |
| **Alerts** | One system sound on rising-edge critical. Mute 15 minutes from the extra/tray or Settings. |

No dummy temperatures. Empty HUD is `NO SENSORS`.

---

## ⬇️ Download

Same GitHub Release, three files. The in-app updater only treats a tag as “latest” if it ships **this OS** file (a Windows-only tag is not a Mac update).

| Platform | File | Requirements |
|---|---|---|
| **macOS** | `MonitorSuhu-*-macos.dmg` | macOS 14+ (universal: Apple Silicon + Intel) |
| **Windows** | `MonitorSuhu-*-windows-x64.exe` | Windows 10 / 11 x64, run as Administrator |
| **Linux** | `MonitorSuhu-*-linux-x64.tar.gz` | x64, `/sys/class/hwmon` (or `/sys/class/thermal`) |

👉 **[Releases](https://github.com/BadryansahBangsawan/MonitorSuhu/releases/latest)**

Default shortcuts on every OS: **Ctrl+Shift+T** (toggle overlay), **Ctrl+Shift+E** (unlock to drag). Rebind in Settings.

---

## 🍎 macOS

1. Download `MonitorSuhu-*-macos.dmg`.
2. Open it. The disk volume **is** `MonitorSuhu.app` (bundle at the volume root). Drag that into **Applications**.
3. Unsigned build — first open:

   ```bash
   xattr -cr /Applications/MonitorSuhu.app && open /Applications/MonitorSuhu.app
   ```

   Or right-click → **Open** → **Open**. Still blocked: **System Settings → Privacy & Security → Open Anyway**.

4. Menu extra title is **Suhu** (right side of the menu bar). Dock icon reopens Settings; closing Settings does not quit the HUD.

**Sensors:** Apple Silicon HID (`IOHIDEventSystemClient`, usage page `0xff00` / usage `0x0005`). Intel fallback: SMC. CPU pick ranks pACC / eACC / SoC over PMU `tdie`. BOARD is wifi / skin / ambient — not the battery (`gas gauge`). No GPU row unless a HID product name contains `gpu` / `agx` / `dgpu` / `gfx`. FAN from SMC `F0Ac` / `F1Ac` (200–15000 RPM).

**Start with macOS:** `SMAppService`. First enable may prompt **System Settings → General → Login Items**.

**Auto-hide:** exclusive fullscreen. Screen-share hide needs Screen Recording on macOS 14+; the app never prompts for it.

Settings: `~/Library/Application Support/MonitorSuhu/settings.json`

---

## 🪟 Windows

1. Download `MonitorSuhu-*-windows-x64.exe`.
2. Run it with **UAC / Administrator** so LibreHardwareMonitor can load its driver.
3. HUD top-right; tray icon in the notification area.

**Sensors:** [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor). Empty HUD is usually driver/UAC, not fake zeros. Antivirus may flag the kernel driver — allow it. BOARD skips Super-I/O sensors named like CPU. FAN is the highest real RPM.

**Start with Windows:** Task Scheduler logon task at highest privileges, applied when you press **Save**.

**Auto-hide:** exclusive fullscreen. Screensaver counts as capture.

A second launch exits (single instance).

Settings: `%AppData%\MonitorSuhu\settings.json`

---

## 🐧 Linux

1. Download `MonitorSuhu-*-linux-x64.tar.gz`.
2. Extract and install:

   ```bash
   tar -xzf MonitorSuhu-*-linux-x64.tar.gz
   bash install.sh
   ```

   Copies to `~/.local/opt/MonitorSuhu`, symlinks `~/.local/bin/MonitorSuhu`, writes `~/.local/share/applications/monitorsuhu.desktop`. Override prefix with `MONITORSUHU_HOME`. Or run `./MonitorSuhu` from the extract folder.

**Sensors:** `/sys/class/hwmon` millidegree temps and fan inputs. If that tree is empty, `/sys/class/thermal`. CPU% from `/proc/stat`. No dummy °C. To list available sensor names before setting Assignments, run `sensors` (from the `lm-sensors` package).

> **Tip — empty HUD on Linux:** if `/sys/class/hwmon` exists but the HUD shows `NO SENSORS`, the hwmon entries may not be world-readable. Run `sudo sensors-detect --auto` once to load the correct kernel modules, then re-launch. On some distros (Arch, NixOS) you may also need to add your user to the `video` group: `sudo usermod -aG video $USER` (log out and back in).

**Start with Linux:** same JSON flag as Windows autostart (`StartWithWindows` in the file).

**Hotkeys:** skipped on Wayland (`WAYLAND_DISPLAY`). X11 grabs on the overlay; a failed grab still leaves the HUD running.

Second launch exits (`/tmp/id.monitorsuhu.lock`).

Settings: `~/.config/MonitorSuhu/settings.json`

---

## ⚙️ Settings

> **Tip — Assignments:** if Auto picks the wrong sensor (e.g. an ambient probe instead of the die temperature), open **Settings → Assignments**, expand the row, and pick the named sensor from the dropdown. The choice persists across restarts and updates.

| Setting | Description |
|---|---|
| **Overlay** | Show, lock (click-through), start with OS, hide in fullscreen / capture |
| **Sensors** | CPU / GPU / SSD / Board / RAM / Fan, plus optional CPU%, GPU%, PWR |
| **Assignments** | Pin a HUD row to a named sensor, or Auto |
| **Thresholds** | Warn (yellow) and critical (red). Fan is RPM; load is %; power is W |
| **Alerts** | Rising-edge critical beep; mute 15 minutes |
| **Shortcuts** | Click a chord, press modifier + letter/number. Esc cancels |
| **Profiles** | Desktop / Game / Silent. Editing sensors or appearance marks Custom |
| **Appearance** | Compact, sparkline (30 samples), opacity, font, accent, °C / °F |
| **Position** | Corner presets or drag. Stored as relative corner edges (Windows is DPI-aware) |
| **Poll** | 400 ms – 3 s (default 1 s) |
| **About** | Version and **Install …** when a newer build for this OS exists |

---

## 🔄 Updates

On launch the app lists GitHub Releases and selects the newest non-draft tag that contains this platform’s asset.

Settings banner (Windows also balloons the tray). **Install …** downloads with a progress bar, then the process exits to finish. Reopen from Applications / Start / the desktop entry if it does not come back.

| OS | What Install does |
|---|---|
| macOS | Replaces `/Applications/MonitorSuhu.app` |
| Windows | Silent setup exe, then relaunch |
| Linux | Extracts the tarball over the running folder |

---

## Build from source

Repo: [BadryansahBangsawan/MonitorSuhu](https://github.com/BadryansahBangsawan/MonitorSuhu). Tag `v*` runs CI (DMG + Inno exe + Linux tarball).

**macOS 14+**

```bash
cd macos
make app          # universal arm64 + x86_64, ad-hoc signed
make test
open build/MonitorSuhu.app
```

**Windows** (x64, .NET 8)

```powershell
dotnet test windows/src/MonitorSuhu.Core.Tests/MonitorSuhu.Core.Tests.csproj
./windows/publish.ps1
# optional: Inno Setup 6 → windows/installer/MonitorSuhu.iss
```

**Linux** (.NET 8)

```bash
dotnet test linux/MonitorSuhu.Linux.Tests/MonitorSuhu.Linux.Tests.csproj
dotnet publish linux/MonitorSuhu.Linux.csproj -c Release -r linux-x64 --self-contained true -o linux/dist/linux-x64
bash linux/scripts/make-tarball.sh
```

Do not run the Linux ELF on macOS.

---

## ❓ FAQ

**Why does the HUD show `NO SENSORS`?**  
On Linux, `/sys/class/hwmon` may not be readable by your user. Run `sudo sensors-detect --auto` once to load kernel modules, or add your user to the `video` group. On Windows, ensure you launched with Administrator / UAC so the LibreHardwareMonitor driver can load.

**Can I display temperatures in Fahrenheit?**  
Yes — open **Settings → Appearance** and switch the °C / °F toggle. The change applies immediately without a restart.

**The HUD disappears in fullscreen games. Is that intentional?**  
Yes. Auto-hide in exclusive fullscreen is enabled by default to avoid interfering with captures and screen recordings. Disable it in **Settings → Overlay → Hide in fullscreen / capture**.

**My GPU sensor is missing on Apple Silicon.**  
MonitorSuhu only shows a GPU row when a HID product name contains `gpu`, `agx`, `dgpu`, or `gfx`. On Apple Silicon this matches the integrated GPU reported by `IOHIDEventSystemClient`. If the row is absent, the sensor name on your chip may differ — check the **Assignments** panel for available names.

**How do I reset settings to defaults?**  
Delete (or rename) the settings file and relaunch: `~/Library/Application Support/MonitorSuhu/settings.json` (macOS), `%AppData%\MonitorSuhu\settings.json` (Windows), or `~/.config/MonitorSuhu/settings.json` (Linux).

**How do I mute the critical alert sound temporarily?**  
From the menu extra / tray or **Settings → Alerts**, choose mute for 15 minutes. Rising-edge critical beeps stay silenced until that window ends or you unmute.

**How do I fully quit the HUD (not just close Settings)?**  
Closing the Settings window leaves the overlay and menu extra running. Quit from the menu extra / tray (**Quit**), or use the configured overlay toggle shortcut then quit from there — the Dock icon on macOS only reopens Settings.

---

<div align="center">

Made with ♥ for people who want to know how hot their machine is.

</div>
