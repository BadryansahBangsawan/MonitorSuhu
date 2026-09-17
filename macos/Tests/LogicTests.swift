import Foundation

@main
enum LogicTests {
    static func main() {
        var failures = 0
        func check(_ name: String, _ ok: Bool) {
            if ok {
                print("ok  \(name)")
            } else {
                print("FAIL \(name)")
                failures += 1
            }
        }

        var settings = AppSettings.default
        settings.overlayOpacity = 2
        settings.fontSize = 3
        settings.pollIntervalMs = 50
        settings.accentHex = "nope"
        settings.sanitize()
        check("opacity clamped", settings.overlayOpacity >= 0.4 && settings.overlayOpacity <= 0.95)
        check("font clamped", settings.fontSize >= 11 && settings.fontSize <= 18)
        check("poll clamped", settings.pollIntervalMs >= 400 && settings.pollIntervalMs <= 3000)
        check("hex fallback", settings.accentHex == AppSettings.nvidiaGreen)

        settings.setThresholds(for: .cpu, warn: 100, critical: 90)
        let cpuT = settings.thresholds(for: .cpu)
        check("cpu warn < crit", cpuT.warn < cpuT.critical)

        settings.setThresholds(for: .fan, warn: 10, critical: 20)
        let fanT = settings.thresholds(for: .fan)
        check("fan rpm range", fanT.warn >= 500 && fanT.critical >= 600 && fanT.warn < fanT.critical)

        settings.setThresholds(for: .cpuLoad, warn: 0, critical: 200)
        let loadT = settings.thresholds(for: .cpuLoad)
        check("load percent range", loadT.warn >= 1 && loadT.critical <= 100 && loadT.warn < loadT.critical)

        settings.setThresholds(for: .power, warn: 1, critical: 2)
        let powerT = settings.thresholds(for: .power)
        check("power watt range", powerT.warn >= 5 && powerT.critical >= 10 && powerT.warn < powerT.critical)

        check("display temp", settings.displayValue(SensorReading(id: "cpu", kind: .cpu, label: "CPU", value: 72)) == "72°C")
        check("display fan", settings.displayValue(SensorReading(id: "fan", kind: .fan, label: "FAN", value: 2100)) == "2100 RPM")
        check("display load", settings.displayValue(SensorReading(id: "cpu-load", kind: .cpuLoad, label: "CPU%", value: 42)) == "42%")
        check("display power", settings.displayValue(SensorReading(id: "power", kind: .power, label: "PWR", value: 125)) == "125 W")
        check("extra rows default off", !settings.showCpuLoad && !settings.showGpuLoad && !settings.showPower)
        check("cpu still visible", settings.isKindVisible(.cpu))
        check("cpu load hidden", !settings.isKindVisible(.cpuLoad))

        check("chord needs modifier", KeyChord.from(keyCode: 0x11, control: false, option: false, shift: false, command: false) == nil)
        check("chord rejects space", KeyChord.from(keyCode: 0x31, control: true, option: false, shift: false, command: false) == nil)
        check("chord digit", KeyChord.from(keyCode: 0x12, control: true, option: false, shift: false, command: false)?.display == "⌃1")
        let rebound = KeyChord.from(keyCode: 0x20, control: true, option: false, shift: true, command: false)
        check("chord display", rebound?.display == "⌃⇧U")
        check("chord conflict", rebound != settings.toggleHotkey)

        let locked = settings.locked
        let poll = settings.pollIntervalMs
        let toggle = settings.toggleHotkey
        settings.applyProfile(HudProfiles.game)
        check("game compact", settings.compactHud)
        check("game extras", settings.showCpuLoad && settings.showGpuLoad && settings.showPower)
        check("game hide ssd", !settings.showSsd && !settings.showBoard && settings.showFan)
        check("game opacity", abs(settings.overlayOpacity - 0.70) < 0.001)
        check("game font", settings.fontSize == 12)
        check("game cpu thresh", settings.thresholds(for: .cpu).warn == 80 && settings.thresholds(for: .cpu).critical == 95)
        check("game keeps lock", settings.locked == locked)
        check("game keeps poll", settings.pollIntervalMs == poll)
        check("game keeps hotkey", settings.toggleHotkey == toggle)
        settings.overlayOpacity = 0.90
        settings.markCustom()
        check("mutate marks custom", settings.activeProfile == HudProfiles.custom)

        var clock: Double = 1_000_000
        let gate = AlertGate(nowMs: { clock })
        settings.setThresholds(for: .cpu, warn: 75, critical: 90)
        let hot = SensorReading(id: "cpu", kind: .cpu, label: "CPU", value: 90)
        check("alert first fire", gate.evaluate(readings: [hot], settings: settings) == ["CPU 90°C"])
        check("alert cooldown", gate.evaluate(readings: [hot], settings: settings).isEmpty)
        clock += AlertGate.cooldownMs
        check("alert after cooldown", gate.evaluate(readings: [hot], settings: settings) == ["CPU 90°C"])
        settings.muteAlerts(nowMs: clock)
        check("alert muted", gate.evaluate(readings: [hot], settings: settings).isEmpty)

        var hideSettings = AppSettings.default
        check("hide fullscreen", FullscreenPolicy.shouldHide(settings: hideSettings, fullscreen: true, capturing: false))
        hideSettings.hideInFullscreen = false
        hideSettings.hideDuringCapture = true
        check("hide capture", FullscreenPolicy.shouldHide(settings: hideSettings, fullscreen: false, capturing: true))
        hideSettings.hideDuringCapture = false
        check("hide disabled", !FullscreenPolicy.shouldHide(settings: hideSettings, fullscreen: true, capturing: true))
        check("covers screen points", FullscreenPolicy.coversScreen(width: 1440, height: 900, screenWidth: 1440, screenHeight: 900, scale: 2))
        check("covers screen pixels", FullscreenPolicy.coversScreen(width: 2880, height: 1800, screenWidth: 1440, screenHeight: 900, scale: 2))
        check("covers screen no", !FullscreenPolicy.coversScreen(width: 400, height: 200, screenWidth: 1440, screenHeight: 900, scale: 2))
        check("capture owner", FullscreenPolicy.isCaptureOwner("screencaptureui"))
        check("capture owner no", !FullscreenPolicy.isCaptureOwner("Safari"))

        settings.setThresholds(for: .cpu, warn: 75, critical: 90)
        settings.setThresholds(for: .fan, warn: 4000, critical: 5500)
        let rows = [
            SensorReading(id: "cpu", kind: .cpu, label: "CPU", value: 90),
            SensorReading(id: "fan", kind: .fan, label: "FAN", value: 2000)
        ]
        check("compact worst is cpu critical", ReadingLevel.worst(rows, settings: settings) == .critical)
        check("fan 2000 rpm is ok", ReadingLevel.of(rows[1], settings: settings) == .ok)

        check("isNewer true", Versioning.isNewer("1.0.5", than: "1.0.4"))
        check("isNewer equal", !Versioning.isNewer("1.0.4", than: "1.0.4"))
        check("isNewer false", !Versioning.isNewer("1.0.3", than: "1.0.4"))

        let dmg = ReleaseAsset(
            name: "MonitorSuhu-1.0.5-macos.dmg",
            url: URL(string: "https://github.com/BadryansahBangsawan/MonitorSuhu/releases/download/v1.0.5/MonitorSuhu-1.0.5-macos.dmg")!,
            size: 2_000_000
        )
        let exe = ReleaseAsset(
            name: "MonitorSuhu-1.0.5-windows-x64.exe",
            url: URL(string: "https://github.com/BadryansahBangsawan/MonitorSuhu/releases/download/v1.0.5/MonitorSuhu-1.0.5-windows-x64.exe")!,
            size: 50_000_000
        )
        check("pick macos dmg", ReleaseAssets.pick([exe, dmg], platform: "macos") == dmg)
        check("reject http dmg", !ReleaseAssets.isTrusted(
            URL(string: "http://github.com/a/b/MonitorSuhu-1.0.5-macos.dmg")!,
            name: dmg.name,
            platform: "macos"
        ))

        if failures > 0 {
            fputs("\(failures) failed\n", stderr)
            exit(1)
        }
        print("all tests passed")
    }
}
