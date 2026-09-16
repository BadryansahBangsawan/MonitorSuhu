import AppKit

final class AlertGate {
    private var inCritical: Set<SensorKind> = []
    func evaluate(readings: [SensorReading], settings: AppSettings) {
        var now: Set<SensorKind> = []
        for r in readings where settings.isKindVisible(r.kind) {
            let t = settings.thresholds(for: r.kind)
            if r.value >= t.critical { now.insert(r.kind) }
        }
        let entered = now.subtracting(inCritical)
        inCritical = now
        if !entered.isEmpty { NSSound.beep() }
    }
}
