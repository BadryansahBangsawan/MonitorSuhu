import Foundation
import SwiftUI

enum SensorKind: String, Codable, CaseIterable, Identifiable {
    case cpu, gpu, ssd, board, ram

    var id: String { rawValue }

    var hudLabel: String {
        switch self {
        case .cpu: return "CPU"
        case .gpu: return "GPU"
        case .ssd: return "SSD"
        case .board: return "BOARD"
        case .ram: return "RAM"
        }
    }
}

struct SensorReading: Identifiable, Hashable {
    let id: String
    let kind: SensorKind
    let label: String
    let celsius: Double
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
        switch code {
        case 0x11: return "T"
        case 0x0E: return "E"
        default: return "#\(code)"
        }
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
    var position: OverlayPosition?
    var thresholds: [String: Thresholds]
    var toggleHotkey: KeyChord
    var editHotkey: KeyChord

    static let nvidiaGreen = "76B900"

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
            position: nil,
            thresholds: [
                SensorKind.cpu.rawValue: Thresholds(warn: 75, critical: 90),
                SensorKind.gpu.rawValue: Thresholds(warn: 75, critical: 90),
                SensorKind.ssd.rawValue: Thresholds(warn: 60, critical: 70),
                SensorKind.board.rawValue: Thresholds(warn: 70, critical: 85),
                SensorKind.ram.rawValue: Thresholds(warn: 70, critical: 85)
            ],
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
        }
    }

    func thresholds(for kind: SensorKind) -> Thresholds {
        thresholds[kind.rawValue] ?? Thresholds(warn: 75, critical: 90)
    }

    func displayTemperature(_ celsius: Double) -> String {
        if useFahrenheit {
            return "\(Int((celsius * 9 / 5 + 32).rounded()))°F"
        }
        return "\(Int(celsius.rounded()))°C"
    }

    var accentColor: Color {
        Color(hex: accentHex) ?? Color(red: 0.46, green: 0.73, blue: 0)
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
