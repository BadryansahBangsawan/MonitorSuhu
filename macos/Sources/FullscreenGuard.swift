import AppKit

final class FullscreenGuard {
    private let store: SettingsStore
    private let overlay: OverlayController
    private var timer: Timer?

    init(store: SettingsStore, overlay: OverlayController) {
        self.store = store
        self.overlay = overlay
    }

    func start() {
        stop()
        let timer = Timer.scheduledTimer(withTimeInterval: 0.5, repeats: true) { [weak self] _ in
            self?.tick()
        }
        RunLoop.main.add(timer, forMode: .common)
        self.timer = timer
        tick()
    }

    func stop() {
        timer?.invalidate()
        timer = nil
    }

    private func tick() {
        let fullscreen = store.settings.hideInFullscreen && Self.isOtherAppFullscreen()
        let capturing = store.settings.hideDuringCapture && Self.isCapturing()
        overlay.applySuppressed(FullscreenPolicy.shouldHide(
            settings: store.settings,
            fullscreen: fullscreen,
            capturing: capturing
        ))
    }

    private static func isOtherAppFullscreen() -> Bool {
        let options: CGWindowListOption = [.optionOnScreenOnly, .excludeDesktopElements]
        guard let info = CGWindowListCopyWindowInfo(options, kCGNullWindowID) as? [[String: Any]] else {
            return false
        }
        let ourPid = ProcessInfo.processInfo.processIdentifier
        let screens = NSScreen.screens
        for window in info {
            let layer = window[kCGWindowLayer as String] as? Int ?? -1
            if layer != 0 { continue }
            let ownerPid = window[kCGWindowOwnerPID as String] as? pid_t ?? 0
            if ownerPid == ourPid { continue }
            guard let bounds = window[kCGWindowBounds as String] as? [String: Any] else { continue }
            let width = (bounds["Width"] as? NSNumber)?.doubleValue ?? 0
            let height = (bounds["Height"] as? NSNumber)?.doubleValue ?? 0
            if width < 64 || height < 64 { continue }
            for screen in screens {
                let frame = screen.frame
                if FullscreenPolicy.coversScreen(
                    width: width,
                    height: height,
                    screenWidth: frame.width,
                    screenHeight: frame.height,
                    scale: screen.backingScaleFactor
                ) {
                    return true
                }
            }
        }
        return false
    }

    private static func isCapturing() -> Bool {
        guard CGPreflightScreenCaptureAccess() else { return false }
        let options: CGWindowListOption = [.optionOnScreenOnly]
        guard let info = CGWindowListCopyWindowInfo(options, kCGNullWindowID) as? [[String: Any]] else {
            return false
        }
        for window in info {
            let owner = window[kCGWindowOwnerName as String] as? String ?? ""
            if FullscreenPolicy.isCaptureOwner(owner) {
                return true
            }
        }
        return false
    }
}
