import Foundation
import Darwin
import IOKit

/// Reads Apple Silicon HID temperature sensors via private IOKit HID event APIs.
/// Symbols are resolved with dlsym so the app still launches if they disappear.
final class HIDTemperatureReader {
    private typealias EventSystemClient = UnsafeMutableRawPointer
    private typealias ServiceClient = UnsafeMutableRawPointer
    private typealias HIDEvent = UnsafeMutableRawPointer

    private typealias CreateFn = @convention(c) (CFAllocator?) -> EventSystemClient?
    private typealias SetMatchingFn = @convention(c) (EventSystemClient, CFDictionary) -> Void
    private typealias CopyServicesFn = @convention(c) (EventSystemClient) -> Unmanaged<CFArray>?
    private typealias CopyEventFn = @convention(c) (ServiceClient, UInt32, UInt32, UInt64) -> HIDEvent?
    private typealias GetFloatFn = @convention(c) (HIDEvent, UInt32) -> Double
    private typealias CopyPropertyFn = @convention(c) (ServiceClient, CFString) -> Unmanaged<CFTypeRef>?
    private typealias ReleaseFn = @convention(c) (UnsafeRawPointer) -> Void

    private static let temperatureEventType: UInt32 = 15
    private static let temperatureField: UInt32 = 15 << 16

    private var handle: UnsafeMutableRawPointer?
    private var client: EventSystemClient?
    private var create: CreateFn?
    private var setMatching: SetMatchingFn?
    private var copyServices: CopyServicesFn?
    private var copyEvent: CopyEventFn?
    private var getFloat: GetFloatFn?
    private var copyProperty: CopyPropertyFn?
    private var release: ReleaseFn?

    func open() {
        if client != nil { return }
        handle = dlopen("/System/Library/Frameworks/IOKit.framework/IOKit", RTLD_LAZY)
        let cf = dlopen("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation", RTLD_LAZY)
        guard let handle else { return }

        create = unsafeBitCast(dlsym(handle, "IOHIDEventSystemClientCreate"), to: CreateFn?.self)
        setMatching = unsafeBitCast(dlsym(handle, "IOHIDEventSystemClientSetMatching"), to: SetMatchingFn?.self)
        copyServices = unsafeBitCast(dlsym(handle, "IOHIDEventSystemClientCopyServices"), to: CopyServicesFn?.self)
        copyEvent = unsafeBitCast(dlsym(handle, "IOHIDServiceClientCopyEvent"), to: CopyEventFn?.self)
        getFloat = unsafeBitCast(dlsym(handle, "IOHIDEventGetFloatValue"), to: GetFloatFn?.self)
        copyProperty = unsafeBitCast(dlsym(handle, "IOHIDServiceClientCopyProperty"), to: CopyPropertyFn?.self)
        if let cf {
            release = unsafeBitCast(dlsym(cf, "CFRelease"), to: ReleaseFn?.self)
        }

        guard let create, let setMatching else { return }
        guard let client = create(kCFAllocatorDefault) else { return }
        self.client = client

        let matching: [String: Any] = [
            "PrimaryUsagePage": 0xff00,
            "PrimaryUsage": 0x0005
        ]
        setMatching(client, matching as CFDictionary)
    }

    func close() {
        if let client {
            release?(client)
            self.client = nil
        }
    }

    deinit { close() }

    func poll() -> [(name: String, celsius: Double)] {
        open()
        guard let client, let copyServices, let copyEvent, let getFloat else { return [] }
        guard let unmanaged = copyServices(client) else { return [] }
        let services = unmanaged.takeRetainedValue() as NSArray

        var rows: [(String, Double)] = []
        rows.reserveCapacity(services.count)

        for (index, object) in services.enumerated() {
            let service = Unmanaged.passUnretained(object as AnyObject).toOpaque()
            var name = "Sensor \(index + 1)"
            if let copyProperty {
                for key in ["Product", "ProductName"] as [CFString] {
                    if let prop = copyProperty(service, key)?.takeRetainedValue() as? String, !prop.isEmpty {
                        name = prop
                        break
                    }
                }
            }

            guard let event = copyEvent(service, Self.temperatureEventType, 0, 0) else { continue }
            let temp = getFloat(event, Self.temperatureField)
            release?(event)
            guard temp.isFinite, temp > 0, temp < 120 else { continue }
            rows.append((name, temp))
        }
        return rows
    }
}
