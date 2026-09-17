import Foundation

enum FullscreenPolicy {
    static func shouldHide(settings: AppSettings, fullscreen: Bool, capturing: Bool) -> Bool {
        (settings.hideInFullscreen && fullscreen) || (settings.hideDuringCapture && capturing)
    }

    static func coversScreen(
        width: Double,
        height: Double,
        screenWidth: Double,
        screenHeight: Double,
        scale: Double
    ) -> Bool {
        func near(_ a: Double, _ b: Double) -> Bool { abs(a - b) < 4 }
        return (near(width, screenWidth) && near(height, screenHeight))
            || (near(width, screenWidth * scale) && near(height, screenHeight * scale))
    }

    static func isCaptureOwner(_ name: String) -> Bool {
        name.lowercased().contains("screencapture")
    }
}
