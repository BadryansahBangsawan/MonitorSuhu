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

        settings.setThresholds(for: .cpu, warn: 75, critical: 90)
        settings.setThresholds(for: .fan, warn: 4000, critical: 5500)
        let rows = [
            SensorReading(id: "cpu", kind: .cpu, label: "CPU", value: 90),
            SensorReading(id: "fan", kind: .fan, label: "FAN", value: 2000)
        ]
        check("compact worst is cpu critical", ReadingLevel.worst(rows, settings: settings) == .critical)
        check("fan 2000 rpm is ok", ReadingLevel.of(rows[1], settings: settings) == .ok)

        check("isNewer true", Versioning.isNewer("1.0.3", than: "1.0.2"))
        check("isNewer equal", !Versioning.isNewer("1.0.2", than: "1.0.2"))
        check("isNewer false", !Versioning.isNewer("1.0.1", than: "1.0.2"))

        if failures > 0 {
            fputs("\(failures) failed\n", stderr)
            exit(1)
        }
        print("all tests passed")
    }
}
