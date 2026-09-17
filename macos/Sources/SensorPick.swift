import Foundation

/// Token ranks for Apple Silicon HID product names.
/// CPU must not be "max of every PMU tdie"; BOARD must not be the battery.
enum SensorPick {
    static let gpuTokens = ["gpu", "agx", "dgpu", "gfx"]
    static let ssdTokens = ["nand", "ssd", "storage"]
    static let ramTokens = ["dram", "memory"]
    static let devTokens = ["tdev"]

    static func cpu(_ rows: [(name: String, celsius: Double)]) -> Double? {
        ranked(rows, score: cpuScore)
    }

    static func board(_ rows: [(name: String, celsius: Double)]) -> Double? {
        token(
            rows,
            matching: ["wifi", "airport", "skin", "ambient"],
            excluding: ["tdie", "soc", "cpu", "pacc", "eacc", "nand", "ssd", "storage", "tdev", "tcal", "pmu", "gas gauge", "battery"]
        )
    }

    static func token(
        _ rows: [(name: String, celsius: Double)],
        matching tokens: [String],
        excluding: [String]
    ) -> Double? {
        let hits = valid(rows).filter { row in
            let n = row.name.lowercased()
            if excluding.contains(where: { n.contains($0) }) { return false }
            return tokens.contains { n.contains($0) }
        }
        return hits.map(\.celsius).max()
    }

    /// Hottest remaining HID row, skipping calibration / NAND / battery.
    static func lastResortCpu(_ rows: [(name: String, celsius: Double)]) -> Double? {
        valid(rows)
            .filter { row in
                let n = row.name.lowercased()
                return !n.contains("tdev")
                    && !n.contains("tcal")
                    && !n.contains("gas gauge")
                    && !n.contains("battery")
                    && !n.contains("nand")
                    && !n.contains("ssd")
            }
            .map(\.celsius)
            .max()
    }

    static func cpuScore(_ name: String) -> Int {
        let n = name.lowercased()
        if n.contains("tdev") || n.contains("tcal") { return 0 }
        if n.contains("pacc") || n.contains("eacc") { return 5 }
        if n.contains("soc") { return 4 }
        if n.contains("cpu") && !n.contains("pmu") { return 4 }
        if n.contains("tdie") && !n.contains("pmu") { return 3 }
        if n.contains("tdie") { return 1 }
        return 0
    }

    private static func ranked(
        _ rows: [(name: String, celsius: Double)],
        score: (String) -> Int
    ) -> Double? {
        let scored = valid(rows).compactMap { row -> (Int, Double)? in
            let s = score(row.name)
            return s > 0 ? (s, row.celsius) : nil
        }
        guard let best = scored.map(\.0).max() else { return nil }
        return scored.filter { $0.0 == best }.map(\.1).max()
    }

    private static func valid(_ rows: [(name: String, celsius: Double)]) -> [(name: String, celsius: Double)] {
        rows.filter { $0.celsius > 1 && $0.celsius < 110 }
    }
}
