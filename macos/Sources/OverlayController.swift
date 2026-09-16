import AppKit
import SwiftUI
import Combine

final class OverlayController: NSObject {
    private var window: NSPanel?
    private let store: SettingsStore
    private let sensors: SensorService
    private var hosting: NSHostingView<OverlayView>?
    private var cancellables = Set<AnyCancellable>()

    init(store: SettingsStore, sensors: SensorService) {
        self.store = store
        self.sensors = sensors
        super.init()

        store.$settings
            .receive(on: DispatchQueue.main)
            .sink { [weak self] _ in self?.relayout() }
            .store(in: &cancellables)

        sensors.$snapshot
            .receive(on: DispatchQueue.main)
            .sink { [weak self] _ in self?.relayout() }
            .store(in: &cancellables)
    }

    func show() {
        if window == nil { build() }
        applyLock()
        restorePosition()
        window?.orderFrontRegardless()
        store.settings.overlayVisible = true
    }

    func hide() {
        window?.orderOut(nil)
        store.settings.overlayVisible = false
    }

    func toggleVisible() {
        if window?.isVisible == true { hide() } else { show() }
    }

    func toggleLocked() {
        store.settings.locked.toggle()
        applyLock()
        if !store.settings.locked {
            window?.orderFrontRegardless()
            NSApp.activate(ignoringOtherApps: true)
        }
    }

    func applyLock() {
        guard let window else { return }
        let locked = store.settings.locked
        window.ignoresMouseEvents = locked
        window.isMovableByWindowBackground = !locked
        relayout()
    }

    func applyPreset(_ preset: CornerPreset) {
        guard let window, let screen = window.screen ?? NSScreen.main else { return }
        relayout()
        let visible = screen.visibleFrame
        let size = window.frame.size
        let margin: CGFloat = 16
        var origin = visible.origin
        switch preset {
        case .topLeft:
            origin = CGPoint(x: visible.minX + margin, y: visible.maxY - size.height - margin)
        case .topRight:
            origin = CGPoint(x: visible.maxX - size.width - margin, y: visible.maxY - size.height - margin)
        case .bottomLeft:
            origin = CGPoint(x: visible.minX + margin, y: visible.minY + margin)
        case .bottomRight:
            origin = CGPoint(x: visible.maxX - size.width - margin, y: visible.minY + margin)
        }
        window.setFrameOrigin(origin)
        persistPosition()
    }

    private func build() {
        let view = OverlayView(store: store, sensors: sensors)
        let hosting = NSHostingView(rootView: view)
        hosting.frame.size = hosting.fittingSize
        self.hosting = hosting

        let panel = NSPanel(
            contentRect: NSRect(origin: .zero, size: hosting.fittingSize),
            styleMask: [.borderless, .nonactivatingPanel],
            backing: .buffered,
            defer: false
        )
        panel.isFloatingPanel = true
        panel.level = .statusBar
        panel.collectionBehavior = [.canJoinAllSpaces, .fullScreenAuxiliary, .stationary]
        panel.isOpaque = false
        panel.backgroundColor = .clear
        panel.hasShadow = false
        panel.hidesOnDeactivate = false
        panel.becomesKeyOnlyIfNeeded = true
        panel.contentView = hosting
        panel.delegate = self
        window = panel
    }

    private func relayout() {
        guard let window, let hosting else { return }
        hosting.rootView = OverlayView(store: store, sensors: sensors)
        let size = hosting.fittingSize
        hosting.frame.size = size
        window.setContentSize(size)
    }

    private func snapIfNeeded() {
        guard !store.settings.locked, let window, let screen = window.screen ?? NSScreen.main else { return }
        let visible = screen.visibleFrame
        var frame = window.frame
        let snap: CGFloat = 24
        let margin: CGFloat = 16

        if abs(frame.minX - visible.minX) < snap { frame.origin.x = visible.minX + margin }
        if abs(frame.maxX - visible.maxX) < snap { frame.origin.x = visible.maxX - frame.width - margin }
        if abs(frame.minY - visible.minY) < snap { frame.origin.y = visible.minY + margin }
        if abs(frame.maxY - visible.maxY) < snap { frame.origin.y = visible.maxY - frame.height - margin }
        if frame != window.frame {
            window.setFrame(frame, display: true)
        }
    }

    private func persistPosition() {
        guard let window, let screen = window.screen ?? NSScreen.main else { return }
        let visible = screen.visibleFrame
        let frame = window.frame
        let relX = visible.width <= 0 ? 0 : (frame.minX - visible.minX) / visible.width
        let relY = visible.height <= 0 ? 0 : (frame.minY - visible.minY) / visible.height
        store.settings.position = OverlayPosition(
            screenName: screen.localizedName,
            relativeX: relX,
            relativeY: relY
        )
    }

    private func restorePosition() {
        guard let window else { return }
        let screens = NSScreen.screens
        let saved = store.settings.position
        let screen = screens.first(where: { $0.localizedName == saved?.screenName }) ?? NSScreen.main ?? screens.first
        guard let screen else { return }
        relayout()
        let visible = screen.visibleFrame

        if let saved {
            let x = visible.minX + saved.relativeX * visible.width
            let y = visible.minY + saved.relativeY * visible.height
            window.setFrameOrigin(CGPoint(x: x, y: y))
        } else {
            applyPreset(.topRight)
        }
    }
}

extension OverlayController: NSWindowDelegate {
    func windowDidMove(_ notification: Notification) {
        snapIfNeeded()
        persistPosition()
    }
}

enum CornerPreset: String, CaseIterable {
    case topLeft, topRight, bottomLeft, bottomRight

    var title: String {
        switch self {
        case .topLeft: return "Top left"
        case .topRight: return "Top right"
        case .bottomLeft: return "Bottom left"
        case .bottomRight: return "Bottom right"
        }
    }
}
