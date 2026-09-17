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

        settings.setThresholds(for: .cpu, warn: 75, critical: 90)
        settings.setThresholds(for: .fan, warn: 4000, critical: 5500)
        let rows = [
            SensorReading(id: "cpu", kind: .cpu, label: "CPU", value: 90),
            SensorReading(id: "fan", kind: .fan, label: "FAN", value: 2000)
        ]
        check("compact worst is cpu critical", ReadingLevel.worst(rows, settings: settings) == .critical)
        check("fan 2000 rpm is ok", ReadingLevel.of(rows[1], settings: settings) == .ok)

        check("isNewer true", Versioning.isNewer("1.0.4", than: "1.0.3"))
        check("isNewer equal", !Versioning.isNewer("1.0.3", than: "1.0.3"))
        check("isNewer false", !Versioning.isNewer("1.0.2", than: "1.0.3"))

        let dmg = ReleaseAsset(
            name: "MonitorSuhu-1.0.4-macos.dmg",
            url: URL(string: "https://github.com/BadryansahBangsawan/MonitorSuhu/releases/download/v1.0.4/MonitorSuhu-1.0.4-macos.dmg")!,
            size: 2_000_000
        )
        let exe = ReleaseAsset(
            name: "MonitorSuhu-1.0.4-windows-x64.exe",
            url: URL(string: "https://github.com/BadryansahBangsawan/MonitorSuhu/releases/download/v1.0.4/MonitorSuhu-1.0.4-windows-x64.exe")!,
            size: 50_000_000
        )
        check("pick macos dmg", ReleaseAssets.pick([exe, dmg], platform: "macos") == dmg)
        check("reject http dmg", !ReleaseAssets.isTrusted(
            URL(string: "http://github.com/a/b/MonitorSuhu-1.0.4-macos.dmg")!,
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
