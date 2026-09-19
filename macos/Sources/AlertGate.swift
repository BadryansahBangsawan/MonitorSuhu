import Foundation

/// Fires on transition into critical per kind; stay-critical does not re-fire;
/// drop below critical then re-cross does fire. Mute/disable skip sound without
/// clearing `active`. HUD color is unchanged.
final class AlertGate {
    static let muteMs: Double = 15 * 60_000

    private var active: Set<SensorKind> = []
    private let nowMs: () -> Double

    init(nowMs: @escaping () -> Double = { Date().timeIntervalSince1970 * 1000 }) {
        self.nowMs = nowMs
    }

    func evaluate(readings: [SensorReading], settings: AppSettings) -> [String] {
        guard settings.alertsEnabled else { return [] }
        let now = nowMs()
        if let until = settings.alertMuteUntil, until > now { return [] }

        var currently = Set<SensorKind>()
        var messages: [String] = []
        for reading in readings where settings.isKindVisible(reading.kind) {
            let t = settings.thresholds(for: reading.kind)
            guard reading.value >= t.critical else { continue }
            currently.insert(reading.kind)
            if active.contains(reading.kind) { continue }
            active.insert(reading.kind)
            messages.append("\(reading.label) \(settings.displayValue(reading))")
        }
        active.formIntersection(currently)
        return messages
    }
}

extension AppSettings {
    mutating func muteAlerts(forMinutes minutes: Double = 15, nowMs: Double = Date().timeIntervalSince1970 * 1000) {
        alertMuteUntil = nowMs + minutes * 60_000
    }

    func muteCaption(nowMs: Double = Date().timeIntervalSince1970 * 1000) -> String? {
        guard let until = alertMuteUntil, until > nowMs else { return nil }
        let minutes = max(1, Int(ceil((until - nowMs) / 60_000)))
        return "Muted for \(minutes) min. HUD colors still change."
    }
}
