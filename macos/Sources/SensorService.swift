import Foundation
import Combine
import IOKit

final class SensorService: ObservableObject {
    @Published private(set) var snapshot = HardwareSnapshot(readings: [], timestamp: Date())
    @Published private(set) var catalog: [(id: String, name: String, value: Double, isFan: Bool)] = []

    private let hid = HIDTemperatureReader()
    private var timer: DispatchSourceTimer?
    private let queue = DispatchQueue(label: "id.monitorsuhu.sensors", qos: .utility)
    private var bindings: () -> [String: String] = { [:] }
    private var rings: [SensorKind: [Double]] = [:]

    func start(intervalMs: Double, bindings: @escaping () -> [String: String] = { [:] }) {
        stop()
        self.bindings = bindings
        hid.open()
        let timer = DispatchSource.makeTimerSource(queue: queue)
        timer.schedule(deadline: .now(), repeating: max(intervalMs, 400) / 1000)
        timer.setEventHandler { [weak self] in
            self?.tick()
        }
        timer.resume()
        self.timer = timer
    }

    func stop() {
        timer?.cancel()
        timer = nil
    }

    func history(for kind: SensorKind) -> [Double] {
        rings[kind] ?? []
    }

    private func tick() {
        let hidRows = hid.poll()
        let smcKeys = SMCTemperatureReader.catalogKeys()
        let fanKeys = SMCFanReader.catalogKeys()
        let bound = bindings()

        var nextCatalog: [(id: String, name: String, value: Double, isFan: Bool)] = []
        var seen = Set<String>()
        for row in hidRows {
            if seen.insert(row.name).inserted {
                nextCatalog.append((id: row.name, name: row.name, value: row.celsius, isFan: false))
            }
        }
        for row in smcKeys {
            if seen.insert(row.id).inserted {
                nextCatalog.append((id: row.id, name: row.name, value: row.value, isFan: false))
            }
        }
        for row in fanKeys {
            if seen.insert(row.id).inserted {
                nextCatalog.append((id: row.id, name: row.name, value: row.value, isFan: true))
            }
        }

        let valid = hidRows.filter { $0.celsius > 1 && $0.celsius < 110 }
        var readings: [SensorReading] = []

        if let cpu = Self.boundOrPick(
            kind: .cpu,
            bindings: bound,
            valid: valid,
            catalog: nextCatalog,
            tokens: Self.cpuTokens,
            excluding: Self.devTokens
        ) {
            readings.append(SensorReading(id: "cpu", kind: .cpu, label: "CPU", celsius: cpu))
        }
        if let gpu = Self.boundOrPick(
            kind: .gpu,
            bindings: bound,
            valid: valid,
            catalog: nextCatalog,
            tokens: Self.gpuTokens,
            excluding: Self.devTokens
        ) {
            readings.append(SensorReading(id: "gpu", kind: .gpu, label: "GPU", celsius: gpu))
        }
        if let ssd = Self.boundOrPick(
            kind: .ssd,
            bindings: bound,
            valid: valid,
            catalog: nextCatalog,
            tokens: Self.ssdTokens,
            excluding: []
        ) {
            readings.append(SensorReading(id: "ssd", kind: .ssd, label: "SSD", celsius: ssd))
        }
        if let board = Self.boundOrPick(
            kind: .board,
            bindings: bound,
            valid: valid,
            catalog: nextCatalog,
            tokens: Self.boardTokens,
            excluding: Self.cpuTokens + Self.ssdTokens + Self.devTokens
        ) {
            readings.append(SensorReading(id: "board", kind: .board, label: "BOARD", celsius: board))
        }
        if let ram = Self.boundOrPick(
            kind: .ram,
            bindings: bound,
            valid: valid,
            catalog: nextCatalog,
            tokens: Self.ramTokens,
            excluding: []
        ) {
            readings.append(SensorReading(id: "ram", kind: .ram, label: "RAM", celsius: ram))
        }

        // Intel Mac fallback: SMC keys when HID is empty.
        if readings.isEmpty, let smc = SMCTemperatureReader.poll() {
            readings.append(contentsOf: smc)
        }

        // Last resort: show the hottest HID sensor as CPU so the HUD is never blank.
        if readings.isEmpty, let hottest = valid.max(by: { $0.celsius < $1.celsius }) {
            readings.append(SensorReading(id: "cpu", kind: .cpu, label: "CPU", celsius: hottest.celsius))
        }

        if let rpm = Self.fanValue(bindings: bound, catalog: nextCatalog), rpm > 0 {
            readings.append(SensorReading(id: "fan", kind: .fan, label: "FAN", celsius: rpm))
        }

        DispatchQueue.main.async { [weak self] in
            guard let self else { return }
            for reading in readings {
                var ring = self.rings[reading.kind, default: []]
                ring.append(reading.celsius)
                if ring.count > 30 {
                    ring.removeFirst()
                }
                self.rings[reading.kind] = ring
            }
            self.catalog = nextCatalog
            if Self.sameDisplay(self.snapshot.readings, readings) { return }
            self.snapshot = HardwareSnapshot(readings: readings, timestamp: Date())
        }
    }

    private static let cpuTokens = ["tdie", "soc", "cpu", "pacc", "eacc"]
    private static let gpuTokens = ["gpu", "agx", "dgpu", "gfx"]
    private static let ssdTokens = ["nand", "ssd", "storage"]
    private static let boardTokens = ["wifi", "airport", "skin", "ambient", "gas gauge"]
    private static let ramTokens = ["dram", "memory"]
    private static let devTokens = ["tdev"]

    private static func sameDisplay(_ a: [SensorReading], _ b: [SensorReading]) -> Bool {
        a.count == b.count && zip(a, b).allSatisfy {
            $0.id == $1.id && $0.kind == $1.kind && $0.celsius.rounded() == $1.celsius.rounded()
        }
    }

    private static func pick(
        _ rows: [(name: String, celsius: Double)],
        matching tokens: [String],
        excluding: [String]
    ) -> Double? {
        let hits = rows.filter { row in
            let n = row.name.lowercased()
            if excluding.contains(where: { n.contains($0) }) { return false }
            return tokens.contains { n.contains($0) }
        }
        return hits.map(\.celsius).max()
    }

    private static func boundOrPick(
        kind: SensorKind,
        bindings: [String: String],
        valid: [(name: String, celsius: Double)],
        catalog: [(id: String, name: String, value: Double, isFan: Bool)],
        tokens: [String],
        excluding: [String]
    ) -> Double? {
        let name = bindings[kind.rawValue] ?? ""
        if !name.isEmpty {
            if let row = valid.first(where: { $0.name == name }) {
                return row.celsius
            }
            if let row = catalog.first(where: { !$0.isFan && ($0.id == name || $0.name == name) }) {
                return row.value
            }
        }
        return pick(valid, matching: tokens, excluding: excluding)
    }

    private static func fanValue(
        bindings: [String: String],
        catalog: [(id: String, name: String, value: Double, isFan: Bool)]
    ) -> Double? {
        let name = bindings[SensorKind.fan.rawValue] ?? ""
        if !name.isEmpty {
            if let row = catalog.first(where: { $0.isFan && ($0.id == name || $0.name == name) }) {
                return row.value
            }
        }
        return catalog.first(where: \.isFan)?.value
    }
}

/// Intel Mac SMC reader. Apple Silicon machines typically have no useful SMC temp keys.
enum SMCTemperatureReader {
    static func poll() -> [SensorReading]? {
        var conn: io_connect_t = 0
        guard openSMC(&conn) else { return nil }
        defer { IOServiceClose(conn) }

        var readings: [SensorReading] = []
        if let cpu = readKey(conn, "TC0P") ?? readKey(conn, "TC0D") ?? readKey(conn, "TC0E") {
            readings.append(SensorReading(id: "cpu", kind: .cpu, label: "CPU", celsius: cpu))
        }
        if let gpu = readKey(conn, "TG0P") ?? readKey(conn, "TG0D") {
            readings.append(SensorReading(id: "gpu", kind: .gpu, label: "GPU", celsius: gpu))
        }
        if let ssd = readKey(conn, "TH0P") ?? readKey(conn, "TH0A") {
            readings.append(SensorReading(id: "ssd", kind: .ssd, label: "SSD", celsius: ssd))
        }
        return readings.isEmpty ? nil : readings
    }

    static func catalogKeys() -> [(id: String, name: String, value: Double)] {
        var conn: io_connect_t = 0
        guard openSMC(&conn) else { return [] }
        defer { IOServiceClose(conn) }
        let keys = ["TC0P", "TC0D", "TC0E", "TG0P", "TG0D", "TH0P", "TH0A"]
        var rows: [(id: String, name: String, value: Double)] = []
        for key in keys {
            if let value = readKey(conn, key) {
                rows.append((id: key, name: key, value: value))
            }
        }
        return rows
    }

    fileprivate static func openSMC(_ conn: inout io_connect_t) -> Bool {
        let matching = IOServiceMatching("AppleSMC")
        var iterator: io_iterator_t = 0
        guard IOServiceGetMatchingServices(kIOMainPortDefault, matching, &iterator) == KERN_SUCCESS else {
            return false
        }
        defer { IOObjectRelease(iterator) }
        let service = IOIteratorNext(iterator)
        guard service != 0 else { return false }
        defer { IOObjectRelease(service) }
        return IOServiceOpen(service, mach_task_self_, 0, &conn) == KERN_SUCCESS
    }

    private static func readKey(_ conn: io_connect_t, _ key: String) -> Double? {
        var input = SMCParamStruct()
        var output = SMCParamStruct()
        input.key = fourChar(key)
        input.data8 = 9 // kSMCGetKeyInfo
        guard smcCall(conn, 2, &input, &output) else { return nil }

        var read = SMCParamStruct()
        var result = SMCParamStruct()
        read.key = fourChar(key)
        read.data8 = 5 // kSMCReadKey
        read.keyInfo.dataSize = output.keyInfo.dataSize
        guard smcCall(conn, 2, &read, &result) else { return nil }
        let bytes = result.bytes
        let size = Int(result.keyInfo.dataSize)
        guard size >= 2 else { return nil }
        let raw = (Int(bytes.0) << 8) | Int(bytes.1)
        let value = Double(raw) / 256.0
        guard value > 0, value < 120 else { return nil }
        return value
    }

    fileprivate static func smcCall(_ conn: io_connect_t, _ index: UInt32, _ input: inout SMCParamStruct, _ output: inout SMCParamStruct) -> Bool {
        let inSize = MemoryLayout<SMCParamStruct>.stride
        var outSize = MemoryLayout<SMCParamStruct>.stride
        let kr = IOConnectCallStructMethod(conn, index, &input, inSize, &output, &outSize)
        return kr == KERN_SUCCESS
    }

    fileprivate static func fourChar(_ s: String) -> UInt32 {
        var result: UInt32 = 0
        for byte in s.utf8.prefix(4) {
            result = (result << 8) | UInt32(byte)
        }
        return result
    }
}

enum SMCFanReader {
    static func poll() -> Double? {
        catalogKeys().first?.value
    }

    static func catalogKeys() -> [(id: String, name: String, value: Double)] {
        var conn: io_connect_t = 0
        guard SMCTemperatureReader.openSMC(&conn) else { return [] }
        defer { IOServiceClose(conn) }
        var rows: [(id: String, name: String, value: Double)] = []
        for key in ["F0Ac", "F1Ac"] {
            if let rpm = readKey(conn, key) {
                rows.append((id: key, name: key, value: rpm))
            }
        }
        return rows
    }

    private static func readKey(_ conn: io_connect_t, _ key: String) -> Double? {
        var input = SMCParamStruct()
        var output = SMCParamStruct()
        input.key = SMCTemperatureReader.fourChar(key)
        input.data8 = 9 // kSMCGetKeyInfo
        guard SMCTemperatureReader.smcCall(conn, 2, &input, &output) else { return nil }

        var read = SMCParamStruct()
        var result = SMCParamStruct()
        read.key = SMCTemperatureReader.fourChar(key)
        read.data8 = 5 // kSMCReadKey
        read.keyInfo.dataSize = output.keyInfo.dataSize
        guard SMCTemperatureReader.smcCall(conn, 2, &read, &result) else { return nil }

        let size = Int(output.keyInfo.dataSize)
        let bytes = result.bytes
        let value: Double
        if size >= 4 {
            let bits = UInt32(bytes.0) << 24
                | UInt32(bytes.1) << 16
                | UInt32(bytes.2) << 8
                | UInt32(bytes.3)
            let parsed = Float(bitPattern: bits)
            guard parsed.isFinite else { return nil }
            value = Double(parsed)
        } else if size == 2 {
            let raw = (Int(bytes.0) << 8) | Int(bytes.1)
            value = Double(raw) / 256.0
        } else {
            return nil
        }
        guard value >= 200, value <= 15000 else { return nil }
        return value
    }
}

private struct SMCParamStruct {
    var key: UInt32 = 0
    var vers = SMCVersion()
    var pLimitData = SMCPLimitData()
    var keyInfo = SMCKeyInfoData()
    var padding: UInt16 = 0
    var result: UInt8 = 0
    var status: UInt8 = 0
    var data8: UInt8 = 0
    var data32: UInt32 = 0
    var bytes: (UInt8, UInt8, UInt8, UInt8, UInt8, UInt8, UInt8, UInt8,
                UInt8, UInt8, UInt8, UInt8, UInt8, UInt8, UInt8, UInt8,
                UInt8, UInt8, UInt8, UInt8, UInt8, UInt8, UInt8, UInt8,
                UInt8, UInt8, UInt8, UInt8, UInt8, UInt8, UInt8, UInt8) =
        (0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)
}

private struct SMCVersion {
    var major: UInt8 = 0
    var minor: UInt8 = 0
    var build: UInt8 = 0
    var reserved: UInt8 = 0
    var release: UInt16 = 0
}

private struct SMCPLimitData {
    var version: UInt16 = 0
    var length: UInt16 = 0
    var cpuPLimit: UInt32 = 0
    var gpuPLimit: UInt32 = 0
    var memPLimit: UInt32 = 0
}

private struct SMCKeyInfoData {
    var dataSize: UInt32 = 0
    var dataType: UInt32 = 0
    var dataAttributes: UInt8 = 0
}
