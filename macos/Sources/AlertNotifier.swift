import AppKit
import UserNotifications

enum AlertNotifier {
    static func request() {
        UNUserNotificationCenter.current().requestAuthorization(options: [.alert, .sound, .provisional]) { _, _ in }
    }

    static func deliver(_ message: String) {
        UNUserNotificationCenter.current().getNotificationSettings { settings in
            switch settings.authorizationStatus {
            case .authorized, .provisional, .ephemeral:
                let content = UNMutableNotificationContent()
                content.title = "MonitorSuhu"
                content.body = message
                content.sound = .default
                let request = UNNotificationRequest(
                    identifier: "alert-\(UUID().uuidString)",
                    content: content,
                    trigger: nil
                )
                UNUserNotificationCenter.current().add(request) { error in
                    if error != nil {
                        DispatchQueue.main.async { NSSound.beep() }
                    }
                }
            default:
                DispatchQueue.main.async { NSSound.beep() }
            }
        }
    }
}
