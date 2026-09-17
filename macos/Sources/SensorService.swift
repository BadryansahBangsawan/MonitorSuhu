import Foundation
import Combine
import Darwin
import IOKit

final class SensorService: ObservableObject {
    @Published private(set) var snapshot = HardwareSnapshot(readings: [], timestamp: Date())
    @Published private(set) var catalog: [(id: String, name: String, value: Double, hint: CatalogHint)] = []

    private let hid = HIDTemperatureReader()
    private var timer: DispatchSourceTimer?
    private let queue = DispatchQueue(label: "id.monitorsuhu.sensors", qos: .utility)
    private var bindings: () -> [String: String] = { [:] }
    private var rings: [SensorKind: [Double]] = [:]
    private var cpuLoad = HostCpuLoad()

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
        queue.sync { hid.close() }
    }

    func history(for kind: SensorKind) -> [Double] {
        rings[kind] ?? []
    }

    private func tick() {
        let hidRows = hid.poll()
        let smcKeys = SMCTemperatureReader.catalogKeys()
        let fanKeys = SMCFanReader.catalogKeys()
        let bound = bindings()

        var nextCatalog: [(id: String, name: String, value: Double, hint: CatalogHint)] = []
        var seen = Set<String>()
        for row in hidRows {
            if seen.insert(row.name).inserted {
                nextCatalog.append((id: row.name, name: row.name, value: row.celsius, hint: .temp))
            }
        }
        for row in smcKeys {
            if seen.insert(row.id).inserted {
                nextCatalog.append((id: row.id, name: row.name, value: row.value, hint: .temp))
            }
        }
        for row in fanKeys {
            if seen.insert(row.id).inserted {
                nextCatalog.append((id: row.id, name: row.name, value: row.value, hint: .fan))
            }
        }
        for row in SMCPowerReader.catalogKeys() {
            if seen.insert(row.id).inserted {
                nextCatalog.append((id: row.id, name: row.name, value: row.value, hint: .power))
            }
        }

        let valid = hidRows.filter { $0.celsius > 1 && $0.celsius < 110 }
        var readings: [SensorReading] = []

        if let cpu = Self.boundTemp(
            kind: .cpu,
            bindings: bound,
            valid: valid,
            catalog: nextCatalog
        ) ?? SensorPick.cpu(valid) {
            readings.append(SensorReading(id: "cpu", kind: .cpu, label: "CPU", value: cpu))
        }
        if let gpu = Self.boundTemp(
            kind: .gpu,
            bindings: bound,
            valid: valid,
            catalog: nextCatalog
        ) ?? SensorPick.token(valid, matching: SensorPick.gpuTokens, excluding: SensorPick.devTokens) {
            readings.append(SensorReading(id: "gpu", kind: .gpu, label: "GPU", value: gpu))
        }
        if let ssd = Self.boundTemp(
            kind: .ssd,
            bindings: bound,
            valid: valid,
            catalog: nextCatalog
        ) ?? SensorPick.token(valid, matching: SensorPick.ssdTokens, excluding: []) {
            readings.append(SensorReading(id: "ssd", kind: .ssd, label: "SSD", value: ssd))
        }
        if let board = Self.boundTemp(
            kind: .board,
            bindings: bound,
            valid: valid,
            catalog: nextCatalog
        ) ?? SensorPick.board(valid) {
            readings.append(SensorReading(id: "board", kind: .board, label: "BOARD", value: board))
        }
        if let ram = Self.boundTemp(
            kind: .ram,
            bindings: bound,
            valid: valid,
            catalog: nextCatalog
        ) ?? SensorPick.token(valid, matching: SensorPick.ramTokens, excluding: []) {
            readings.append(SensorReading(id: "ram", kind: .ram, label: "RAM", value: ram))
        }

        // Intel Mac fallback: SMC keys when HID is empty.
        if readings.isEmpty, let smc = SMCTemperatureReader.poll() {
            readings.append(contentsOf: smc)
        }

        if readings.isEmpty, let hottest = SensorPick.lastResortCpu(valid) {
            readings.append(SensorReading(id: "cpu", kind: .cpu, label: "CPU", value: hottest))
        }

        if let rpm = Self.fanValue(bindings: bound, catalog: nextCatalog), rpm > 0 {
            readings.append(SensorReading(id: "fan", kind: .fan, label: "FAN", value: rpm))
        }

        if let load = Self.boundExtra(kind: .cpuLoad, bindings: bound, catalog: nextCatalog, hint: .load)
            ?? cpuLoad.poll()
        {
            readings.append(SensorReading(id: "cpu-load", kind: .cpuLoad, label: "CPU%", value: load))
        }
        if let gpuLoad = Self.boundExtra(kind: .gpuLoad, bindings: bound, catalog: nextCatalog, hint: .load)
            ?? GpuLoadReader.poll()
        {
            readings.append(SensorReading(id: "gpu-load", kind: .gpuLoad, label: "GPU%", value: gpuLoad))
        }
        if let watts = Self.boundExtra(kind: .power, bindings: bound, catalog: nextCatalog, hint: .power)
            ?? nextCatalog.first(where: { $0.hint == .power })?.value
        {
            readings.append(SensorReading(id: "power", kind: .power, label: "PWR", value: watts))
        }

        DispatchQueue.main.async { [weak self] in
            guard let self else { return }
            for reading in readings {
                var ring = self.rings[reading.kind, default: []]
                ring.append(reading.value)
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

    private static func boundTemp(
        kind: SensorKind,
        bindings: [String: String],
        valid: [(name: String, celsius: Double)],
        catalog: [(id: String, name: String, value: Double, hint: CatalogHint)]
    ) -> Double? {
        let name = bindings[kind.rawValue] ?? ""
        guard !name.isEmpty else { return nil }
        if let row = valid.first(where: { $0.name == name }) {
            return row.celsius
        }
        if let row = catalog.first(where: { $0.hint == .temp && ($0.id == name || $0.name == name) }) {
            return row.value
        }
        return nil
    }

    private static func sameDisplay(_ a: [SensorReading], _ b: [SensorReading]) -> Bool {
        a.count == b.count && zip(a, b).allSatisfy {
            $0.id == $1.id && $0.kind == $1.kind && $0.value.rounded() == $1.value.rounded()
        }
    }

    private static func fanValue(
        bindings: [String: String],
        catalog: [(id: String, name: String, value: Double, hint: CatalogHint)]
    ) -> Double? {
        let name = bindings[SensorKind.fan.rawValue] ?? ""
        if !name.isEmpty {
            if let row = catalog.first(where: { $0.hint == .fan && ($0.id == name || $0.name == name) }) {
                return row.value
            }
        }
        return catalog.first(where: { $0.hint == .fan })?.value
    }

    private static func boundExtra(
        kind: SensorKind,
        bindings: [String: String],
        catalog: [(id: String, name: String, value: Double, hint: CatalogHint)],
        hint: CatalogHint
    ) -> Double? {
        let name = bindings[kind.rawValue] ?? ""
        guard !name.isEmpty else { return nil }
        return catalog.first(where: { $0.hint == hint && ($0.id == name || $0.name == name) })?.value
    }
}

final class HostCpuLoad {
    private var previous: host_cpu_load_info?

    func poll() -> Double? {
        var info = host_cpu_load_info()
        var count = mach_msg_type_number_t(MemoryLayout<host_cpu_load_info>.stride / MemoryLayout<integer_t>.stride)
        let kr = withUnsafeMutablePointer(to: &info) {
            $0.withMemoryRebound(to: integer_t.self, capacity: Int(count)) {
                host_statistics(mach_host_self(), HOST_CPU_LOAD_INFO, $0, &count)
            }
        }
        guard kr == KERN_SUCCESS else { return nil }
        defer { previous = info }
        guard let prev = previous else { return nil }
        let user = Double(info.cpu_ticks.0) - Double(prev.cpu_ticks.0)
        let system = Double(info.cpu_ticks.1) - Double(prev.cpu_ticks.1)
        let idle = Double(info.cpu_ticks.2) - Double(prev.cpu_ticks.2)
        let nice = Double(info.cpu_ticks.3) - Double(prev.cpu_ticks.3)
        let total = user + system + idle + nice
        guard total > 0 else { return nil }
        return min(100, max(0, (user + system + nice) / total * 100))
    }
}

enum GpuLoadReader {
    static func poll() -> Double? {
        let matching = IOServiceMatching("IOAccelerator")
        var iterator: io_iterator_t = 0
        guard IOServiceGetMatchingServices(kIOMainPortDefault, matching, &iterator) == KERN_SUCCESS else {
            return nil
        }
        defer { IOObjectRelease(iterator) }

        var best: Double?
        var service = IOIteratorNext(iterator)
        while service != 0 {
            defer {
                IOObjectRelease(service)
                service = IOIteratorNext(iterator)
            }
            var props: Unmanaged<CFMutableDictionary>?
            guard IORegistryEntryCreateCFProperties(service, &props, kCFAllocatorDefault, 0) == KERN_SUCCESS,
                  let dict = props?.takeRetainedValue() as? [String: Any]
            else { continue }
            if let stats = dict["PerformanceStatistics"] as? [String: Any] {
                for key in ["Device Utilization %", "GPU Activity(%)", "Renderer Utilization %"] {
                    if let n = number(stats[key]), n > 0, n <= 100 {
                        best = max(best ?? 0, n)
                    }
                }
            }
        }
        return best
    }

    private static func number(_ value: Any?) -> Double? {
        if let n = value as? NSNumber { return n.doubleValue }
        if let n = value as? Double { return n }
        if let n = value as? Int { return Double(n) }
        return nil
    }
}

enum SMCPowerReader {
    static func poll() -> Double? {
        catalogKeys().first?.value
    }

    static func catalogKeys() -> [(id: String, name: String, value: Double)] {
        var conn: io_connect_t = 0
        guard SMCTemperatureReader.openSMC(&conn) else { return [] }
        defer { IOServiceClose(conn) }
        var rows: [(id: String, name: String, value: Double)] = []
        for key in ["PSTR", "PCPT", "PCPC"] {
            if let watts = readWatts(conn, key) {
                rows.append((id: key, name: key, value: watts))
            }
        }
        return rows
    }

    private static func readWatts(_ conn: io_connect_t, _ key: String) -> Double? {
        var input = SMCParamStruct()
        var output = SMCParamStruct()
        input.key = SMCTemperatureReader.fourChar(key)
        input.data8 = 9
        guard SMCTemperatureReader.smcCall(conn, 2, &input, &output) else { return nil }

        var read = SMCParamStruct()
        var result = SMCParamStruct()
        read.key = SMCTemperatureReader.fourChar(key)
        read.data8 = 5
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
        } else if size >= 2 {
            let raw = (Int(bytes.0) << 8) | Int(bytes.1)
            value = Double(raw) / 256.0
        } else {
            return nil
        }
        guard value > 0, value < 1000 else { return nil }
        return value
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
            readings.append(SensorReading(id: "cpu", kind: .cpu, label: "CPU", value: cpu))
        }
        if let gpu = readKey(conn, "TG0P") ?? readKey(conn, "TG0D") {
            readings.append(SensorReading(id: "gpu", kind: .gpu, label: "GPU", value: gpu))
        }
        if let ssd = readKey(conn, "TH0P") ?? readKey(conn, "TH0A") {
            readings.append(SensorReading(id: "ssd", kind: .ssd, label: "SSD", value: ssd))
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
