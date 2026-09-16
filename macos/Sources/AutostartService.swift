import Foundation
import ServiceManagement

enum AutostartService {
    static func apply(_ enabled: Bool) -> String? {
        guard #available(macOS 13.0, *) else {
            return "Start at login requires macOS 13 or later."
        }
        do {
            let service = SMAppService.mainApp
            if enabled {
                switch service.status {
                case .enabled:
                    return nil
                case .requiresApproval:
                    return loginItemsHint
                default:
                    try service.register()
                    if service.status == .requiresApproval {
                        return loginItemsHint
                    }
                    return nil
                }
            } else {
                if service.status != .notRegistered {
                    try service.unregister()
                }
                return nil
            }
        } catch {
            return "Could not update Start with macOS: \(error.localizedDescription)"
        }
    }

    static func statusMessage() -> String? {
        guard #available(macOS 13.0, *) else { return nil }
        if SMAppService.mainApp.status == .requiresApproval {
            return loginItemsHint
        }
        return nil
    }

    private static let loginItemsHint =
        "Allow MonitorSuhu in System Settings → General → Login Items."
}
