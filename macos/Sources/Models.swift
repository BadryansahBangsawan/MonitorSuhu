import Foundation
import SwiftUI

enum SensorKind: String, Codable, CaseIterable, Identifiable {
    case cpu, gpu, ssd, board, ram, fan

    var id: String { rawValue }

    var hudLabel: String {
        switch self {
        case .cpu: return "CPU"
        case .gpu: return "GPU"
        case .ssd: return "SSD"
        case .board: return "BOARD"
        case .ram: return "RAM"
        case .fan: return "FAN"
        }
    }
}

struct SensorReading: Identifiable, Hashable {
    let id: String
    let kind: SensorKind
    let label: String
    let value: Double
}

enum ReadingLevel: Int, Comparable {
    case ok = 0
    case warn = 1
    case critical = 2

    static func < (lhs: ReadingLevel, rhs: ReadingLevel) -> Bool {
        lhs.rawValue < rhs.rawValue
    }

    static func of(value: Double, thresholds: Thresholds) -> ReadingLevel {
        if value >= thresholds.critical { return .critical }
        if value >= thresholds.warn { return .warn }
        return .ok
    }

    static func of(_ reading: SensorReading, settings: AppSettings) -> ReadingLevel {
        of(value: reading.value, thresholds: settings.thresholds(for: reading.kind))
    }

    static func worst(_ levels: [ReadingLevel]) -> ReadingLevel {
        levels.max() ?? .ok
    }

    static func worst(_ readings: [SensorReading], settings: AppSettings) -> ReadingLevel {
        worst(readings.map { of($0, settings: settings) })
    }
}

struct HardwareSnapshot {
    var readings: [SensorReading]
    var timestamp: Date
}

struct Thresholds: Codable, Equatable {
    var warn: Double
    var critical: Double
}

struct OverlayPosition: Codable, Equatable {
    var screenName: String
    var relativeX: Double
    var relativeY: Double
    /// Right / top edges, so a size change can pin the same corner.
    var relativeMaxX: Double?
    var relativeMaxY: Double?
}

struct KeyChord: Codable, Equatable {
    var keyCode: UInt32
    var control: Bool
    var option: Bool
    var shift: Bool
    var command: Bool

    var carbonModifiers: UInt32 {
        var m: UInt32 = 0
        if control { m |= 0x1000 } // controlKey
        if option { m |= 0x0800 }  // optionKey
        if shift { m |= 0x0200 }   // shiftKey
        if command { m |= 0x0100 } // cmdKey
        return m
    }

    var display: String {
        var parts: [String] = []
        if control { parts.append("⌃") }
        if option { parts.append("⌥") }
        if shift { parts.append("⇧") }
        if command { parts.append("⌘") }
        parts.append(Self.keyName(keyCode))
        return parts.joined()
    }

    private static func keyName(_ code: UInt32) -> String {
        // Carbon virtual key codes (kVK_ANSI_*). Look up by the integer value
        // so JSON-decoded 17 matches T (0x11) rather than rendering as "#17".
        let names: [UInt32: String] = [
            0x00: "A", 0x01: "S", 0x02: "D", 0x03: "F", 0x04: "H",
            0x05: "G", 0x06: "Z", 0x07: "X", 0x08: "C", 0x09: "V",
            0x0B: "B", 0x0C: "Q", 0x0D: "W", 0x0E: "E", 0x0F: "R",
            0x10: "Y", 0x11: "T", 0x12: "1", 0x13: "2", 0x14: "3",
            0x15: "4", 0x16: "6", 0x17: "5", 0x18: "=", 0x19: "9",
            0x1A: "7", 0x1B: "-", 0x1C: "8", 0x1D: "0", 0x1E: "]",
            0x1F: "O", 0x20: "U", 0x21: "[", 0x22: "I", 0x23: "P",
            0x25: "L", 0x26: "J", 0x27: "'", 0x28: "K", 0x29: ";",
            0x2D: "N", 0x2E: "M", 0x2F: ".", 0x31: " "
        ]
        if let name = names[code] { return name }
        if let name = names[UInt32(Int(code))] { return name }
        return "#\(Int(code))"
    }
}

struct AppSettings: Codable, Equatable {
    var overlayOpacity: Double
    var fontSize: Double
    var accentHex: String
    var useFahrenheit: Bool
    var pollIntervalMs: Double
    var locked: Bool
    var overlayVisible: Bool
    var startWithOs: Bool
    var showCpu: Bool
    var showGpu: Bool
    var showSsd: Bool
    var showBoard: Bool
    var showRam: Bool
    var compactHud: Bool
    var showSparkline: Bool
    var showFan: Bool
    var position: OverlayPosition?
    var thresholds: [String: Thresholds]
    var sensorBindings: [String: String]
    var toggleHotkey: KeyChord
    var editHotkey: KeyChord

    static let nvidiaGreen = "76B900"

    static func normalizeHex(_ hex: String) -> String {
        var s = hex.trimmingCharacters(in: .whitespacesAndNewlines).uppercased()
        if s.hasPrefix("#") { s.removeFirst() }
        return s
    }

    static var `default`: AppSettings {
        AppSettings(
            overlayOpacity: 0.80,
            fontSize: 13,
            accentHex: nvidiaGreen,
            useFahrenheit: false,
            pollIntervalMs: 1000,
            locked: true,
            overlayVisible: true,
            startWithOs: false,
            showCpu: true,
            showGpu: true,
            showSsd: true,
            showBoard: true,
            showRam: true,
            compactHud: false,
            showSparkline: false,
            showFan: true,
            position: nil,
            thresholds: [
                SensorKind.cpu.rawValue: Thresholds(warn: 75, critical: 90),
                SensorKind.gpu.rawValue: Thresholds(warn: 75, critical: 90),
                SensorKind.ssd.rawValue: Thresholds(warn: 60, critical: 70),
                SensorKind.board.rawValue: Thresholds(warn: 70, critical: 85),
                SensorKind.ram.rawValue: Thresholds(warn: 70, critical: 85),
                SensorKind.fan.rawValue: Thresholds(warn: 4000, critical: 5500)
            ],
            sensorBindings: [:],
            toggleHotkey: KeyChord(keyCode: 0x11, control: true, option: false, shift: true, command: false),
            editHotkey: KeyChord(keyCode: 0x0E, control: true, option: false, shift: true, command: false)
        )
    }

    func isKindVisible(_ kind: SensorKind) -> Bool {
        switch kind {
        case .cpu: return showCpu
        case .gpu: return showGpu
        case .ssd: return showSsd
        case .board: return showBoard
        case .ram: return showRam
        case .fan: return showFan
        }
    }

    func thresholds(for kind: SensorKind) -> Thresholds {
        thresholds[kind.rawValue] ?? Thresholds(warn: 75, critical: 90)
    }

    mutating func setThresholds(for kind: SensorKind, warn: Double? = nil, critical: Double? = nil) {
        var t = thresholds(for: kind)
        if kind == .fan {
            if let warn { t.warn = Self.clamp(warn, 500, 8000) }
            if let critical { t.critical = Self.clamp(critical, 600, 10000) }
            if t.warn >= t.critical {
                t.critical = min(10000, t.warn + 5)
            }
        } else {
            if let warn { t.warn = Self.clamp(warn, 1, 120) }
            if let critical { t.critical = Self.clamp(critical, 2, 130) }
            if t.warn >= t.critical {
                t.critical = min(130, t.warn + 5)
            }
        }
        thresholds[kind.rawValue] = t
    }

    mutating func sanitize() {
        overlayOpacity = Self.clamp(overlayOpacity, 0.4, 0.95)
        fontSize = Self.clamp(fontSize, 11, 18)
        pollIntervalMs = Self.clamp(pollIntervalMs, 400, 3000)
        let hex = Self.normalizeHex(accentHex)
        accentHex = hex.count == 6 ? hex : Self.nvidiaGreen
        var next = Self.default.thresholds
        for (key, value) in thresholds {
            var t = value
            if key == SensorKind.fan.rawValue {
                t.warn = Self.clamp(t.warn, 500, 8000)
                t.critical = Self.clamp(t.critical, 600, 10000)
                if t.warn >= t.critical { t.critical = min(10000, t.warn + 5) }
            } else {
                t.warn = Self.clamp(t.warn, 1, 120)
                t.critical = Self.clamp(t.critical, 2, 130)
                if t.warn >= t.critical { t.critical = min(130, t.warn + 5) }
            }
            next[key] = t
        }
        thresholds = next
    }


    private static func clamp(_ value: Double, _ lo: Double, _ hi: Double) -> Double {
        min(hi, max(lo, value))
    }

    func displayTemperature(_ celsius: Double) -> String {
        if useFahrenheit {
            return "\(Int((celsius * 9 / 5 + 32).rounded()))°F"
        }
        return "\(Int(celsius.rounded()))°C"
    }

    func displayValue(_ reading: SensorReading) -> String {
        if reading.kind == .fan {
            return "\(Int(reading.value.rounded())) RPM"
        }
        return displayTemperature(reading.value)
    }

    func menuBarTitle(cpuCelsius: Double?) -> String {
        guard let cpuCelsius else { return "Suhu" }
        let converted = useFahrenheit ? cpuCelsius * 9 / 5 + 32 : cpuCelsius
        return "Suhu \(Int(converted.rounded()))°"
    }

    var accentColor: Color {
        Color(hex: accentHex) ?? Color(red: 0.46, green: 0.73, blue: 0)
    }
}

extension AppSettings {
    init(from decoder: Decoder) throws {
        let d = AppSettings.default
        let c = try decoder.container(keyedBy: CodingKeys.self)
        overlayOpacity = try c.decodeIfPresent(Double.self, forKey: .overlayOpacity) ?? d.overlayOpacity
        fontSize = try c.decodeIfPresent(Double.self, forKey: .fontSize) ?? d.fontSize
        accentHex = try c.decodeIfPresent(String.self, forKey: .accentHex) ?? d.accentHex
        useFahrenheit = try c.decodeIfPresent(Bool.self, forKey: .useFahrenheit) ?? d.useFahrenheit
        pollIntervalMs = try c.decodeIfPresent(Double.self, forKey: .pollIntervalMs) ?? d.pollIntervalMs
        locked = try c.decodeIfPresent(Bool.self, forKey: .locked) ?? d.locked
        overlayVisible = try c.decodeIfPresent(Bool.self, forKey: .overlayVisible) ?? d.overlayVisible
        startWithOs = try c.decodeIfPresent(Bool.self, forKey: .startWithOs) ?? d.startWithOs
        showCpu = try c.decodeIfPresent(Bool.self, forKey: .showCpu) ?? d.showCpu
        showGpu = try c.decodeIfPresent(Bool.self, forKey: .showGpu) ?? d.showGpu
        showSsd = try c.decodeIfPresent(Bool.self, forKey: .showSsd) ?? d.showSsd
        showBoard = try c.decodeIfPresent(Bool.self, forKey: .showBoard) ?? d.showBoard
        showRam = try c.decodeIfPresent(Bool.self, forKey: .showRam) ?? d.showRam
        compactHud = try c.decodeIfPresent(Bool.self, forKey: .compactHud) ?? d.compactHud
        showSparkline = try c.decodeIfPresent(Bool.self, forKey: .showSparkline) ?? d.showSparkline
        showFan = try c.decodeIfPresent(Bool.self, forKey: .showFan) ?? d.showFan
        position = try c.decodeIfPresent(OverlayPosition.self, forKey: .position) ?? d.position
        thresholds = try c.decodeIfPresent([String: Thresholds].self, forKey: .thresholds) ?? d.thresholds
        sensorBindings = try c.decodeIfPresent([String: String].self, forKey: .sensorBindings) ?? d.sensorBindings
        toggleHotkey = try c.decodeIfPresent(KeyChord.self, forKey: .toggleHotkey) ?? d.toggleHotkey
        editHotkey = try c.decodeIfPresent(KeyChord.self, forKey: .editHotkey) ?? d.editHotkey
        sanitize()
    }
}

extension Color {
    init?(hex: String) {
        var s = hex.trimmingCharacters(in: .whitespacesAndNewlines)
        if s.hasPrefix("#") { s.removeFirst() }
        guard s.count == 6, let value = UInt32(s, radix: 16) else { return nil }
        let r = Double((value >> 16) & 0xFF) / 255
        let g = Double((value >> 8) & 0xFF) / 255
        let b = Double(value & 0xFF) / 255
        self.init(red: r, green: g, blue: b)
    }
}
