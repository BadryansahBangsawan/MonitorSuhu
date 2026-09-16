import AppKit

@main
enum MonitorSuhuMain {
    // NSApplication.delegate is weak; keep the AppDelegate alive for the process lifetime.
    static var delegate: AppDelegate?

    static func main() {
        let app = NSApplication.shared
        let delegate = AppDelegate()
        Self.delegate = delegate
        app.delegate = delegate
        app.setActivationPolicy(.regular)
        app.run()
    }
}
