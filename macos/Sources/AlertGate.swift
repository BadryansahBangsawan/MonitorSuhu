import Foundation

/// Fires when a visible reading is at or above critical. Cooldown is 60 s per kind
/// even if the value stays critical. Mute skips sound/notification; HUD color is unchanged.
final class AlertGate {
    static let cooldownMs: Double = 60_000
    static let muteMs: Double = 15 * 60_000

    private var lastFired: [SensorKind: Double] = [:]
    private let nowMs: () -> Double

    init(nowMs: @escaping () -> Double = { Date().timeIntervalSince1970 * 1000 }) {
        self.nowMs = nowMs
    }

    func evaluate(readings: [SensorReading], settings: AppSettings) -> [String] {
        guard settings.alertsEnabled else { return [] }
        let now = nowMs()
        if let until = settings.alertMuteUntil, until > now { return [] }

        var messages: [String] = []
        for reading in readings where settings.isKindVisible(reading.kind) {
            let t = settings.thresholds(for: reading.kind)
            guard reading.value >= t.critical else { continue }
            if let last = lastFired[reading.kind], now - last < Self.cooldownMs { continue }
            lastFired[reading.kind] = now
            messages.append("\(reading.label) \(settings.displayValue(reading))")
        }
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
