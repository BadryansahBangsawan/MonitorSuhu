import AppKit
import SwiftUI
import Combine

final class OverlayController: NSObject {
    private var window: NSPanel?
    private let store: SettingsStore
    private let sensors: SensorService
    private var hosting: NSHostingView<OverlayView>?
    private var cancellables = Set<AnyCancellable>()
    private var isFinalizing = false
    /// First snapshots are empty; restore again once the HUD has its real size.
    private var needsRestore = false

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
        // The panel must be on-screen before we read screen.visibleFrame /
        // set a restored origin; otherwise Cocoa maps the frame through a
        // not-yet-attached coordinate space and the HUD lands ~menu-bar off.
        window?.orderFrontRegardless()
        needsRestore = true
        restorePosition()
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
        let visible = screen.visibleFrame
        let size = window.frame.size
        let margin = Self.edgeMargin
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
        isFinalizing = true
        window.setFrameOrigin(origin)
        finalizePosition(forceSnap: true)
        isFinalizing = false
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
        var size = hosting.fittingSize
        if size.width < 96 { size.width = 96 }
        if size.height < 28 { size.height = 28 }

        let oldFrame = window.frame
        hosting.frame.size = size
        var frame = oldFrame
        frame.size = size
        // Pin the edges that are already closer to the screen border so a
        // font/sensor-row change does not walk a corner HUD inward.
        if let screen = window.screen ?? NSScreen.main {
            let visible = screen.visibleFrame
            if oldFrame.midX >= visible.midX {
                frame.origin.x = oldFrame.maxX - size.width
            }
            if oldFrame.midY >= visible.midY {
                frame.origin.y = oldFrame.maxY - size.height
            }
        }
        frame = Self.clamp(frame, to: (window.screen ?? NSScreen.main)?.visibleFrame)
        if abs(oldFrame.width - frame.width) >= 0.5
            || abs(oldFrame.height - frame.height) >= 0.5
            || abs(oldFrame.origin.x - frame.origin.x) >= 0.5
            || abs(oldFrame.origin.y - frame.origin.y) >= 0.5 {
            isFinalizing = true
            window.setFrame(frame, display: true)
            isFinalizing = false
        }

        if needsRestore, window.isVisible {
            restorePosition()
            if !sensors.snapshot.readings.isEmpty {
                finalizePosition(forceSnap: true)
                needsRestore = false
                persistPosition()
            }
        }
    }

    private static let edgeMargin: CGFloat = 16

    private static func clamp(_ frame: NSRect, to visible: NSRect?) -> NSRect {
        guard let visible, visible.width > 0, visible.height > 0 else { return frame }
        var f = frame
        let inset = visible.insetBy(dx: edgeMargin, dy: edgeMargin)
        guard inset.width > 0, inset.height > 0 else { return frame }
        if f.width > inset.width { f.size.width = inset.width }
        if f.height > inset.height { f.size.height = inset.height }
        if f.minX < inset.minX { f.origin.x = inset.minX }
        if f.maxX > inset.maxX { f.origin.x = inset.maxX - f.width }
        if f.minY < inset.minY { f.origin.y = inset.minY }
        if f.maxY > inset.maxY { f.origin.y = inset.maxY - f.height }
        return f
    }

    /// Keep the HUD on-screen, then magnet-snap to edges.
    /// Clamp first so a fast drag past the screen edge cannot stick at a
    /// negative origin (snap only looks within 24px of the raw edge).
    private func finalizePosition(forceSnap: Bool = false) {
        guard let window, let screen = window.screen ?? NSScreen.main else { return }
        let visible = screen.visibleFrame
        var frame = Self.clamp(window.frame, to: visible)

        if forceSnap || !store.settings.locked {
            let snap: CGFloat = 24
            let margin = Self.edgeMargin
            if abs(frame.minX - visible.minX) < snap { frame.origin.x = visible.minX + margin }
            if abs(frame.maxX - visible.maxX) < snap { frame.origin.x = visible.maxX - frame.width - margin }
            if abs(frame.minY - visible.minY) < snap { frame.origin.y = visible.minY + margin }
            if abs(frame.maxY - visible.maxY) < snap { frame.origin.y = visible.maxY - frame.height - margin }
            frame = Self.clamp(frame, to: visible)
        }

        if frame != window.frame {
            window.setFrame(frame, display: true)
        }
    }

    private func persistPosition() {
        guard !needsRestore else { return }
        guard let window, let screen = window.screen ?? NSScreen.main else { return }
        let visible = screen.visibleFrame
        let frame = window.frame
        let relX = visible.width <= 0 ? 0 : (frame.minX - visible.minX) / visible.width
        let relY = visible.height <= 0 ? 0 : (frame.minY - visible.minY) / visible.height
        let relMaxX = visible.width <= 0 ? 1 : (frame.maxX - visible.minX) / visible.width
        let relMaxY = visible.height <= 0 ? 1 : (frame.maxY - visible.minY) / visible.height
        let next = OverlayPosition(
            screenName: screen.localizedName,
            relativeX: (relX * 10000).rounded() / 10000,
            relativeY: (relY * 10000).rounded() / 10000,
            relativeMaxX: (relMaxX * 10000).rounded() / 10000,
            relativeMaxY: (relMaxY * 10000).rounded() / 10000
        )
        if store.settings.position == next { return }
        store.settings.position = next
    }

    private func restorePosition() {
        guard let window else { return }
        let screens = NSScreen.screens
        let saved = store.settings.position
        let screen = screens.first(where: { $0.localizedName == saved?.screenName }) ?? NSScreen.main ?? screens.first
        guard let screen else { return }
        let visible = screen.visibleFrame

        if let saved {
            // Old settings only stored the bottom-left. If that is clearly a
            // corner HUD, re-apply the preset so a size change cannot drift.
            if saved.relativeMaxX == nil, saved.relativeMaxY == nil {
                let nearLeft = saved.relativeX < 0.08
                let nearRight = saved.relativeX > 0.75
                let nearBottom = saved.relativeY < 0.08
                let nearTop = saved.relativeY > 0.75
                if nearLeft && nearTop { applyPreset(.topLeft); return }
                if nearRight && nearTop { applyPreset(.topRight); return }
                if nearLeft && nearBottom { applyPreset(.bottomLeft); return }
                if nearRight && nearBottom { applyPreset(.bottomRight); return }
            }

            var frame = window.frame
            let pinRight = saved.relativeMaxX.map { abs($0 - 1) < abs(saved.relativeX) } ?? false
            let pinTop = saved.relativeMaxY.map { abs($0 - 1) < abs(saved.relativeY) } ?? false
            if pinRight, let maxX = saved.relativeMaxX {
                frame.origin.x = visible.minX + maxX * visible.width - frame.width
            } else {
                frame.origin.x = visible.minX + saved.relativeX * visible.width
            }
            if pinTop, let maxY = saved.relativeMaxY {
                frame.origin.y = visible.minY + maxY * visible.height - frame.height
            } else {
                frame.origin.y = visible.minY + saved.relativeY * visible.height
            }
            isFinalizing = true
            window.setFrame(Self.clamp(frame, to: visible), display: true)
            isFinalizing = false
        } else {
            applyPreset(.topRight)
        }
    }
}

extension OverlayController: NSWindowDelegate {
    func windowDidMove(_ notification: Notification) {
        guard !isFinalizing else { return }
        isFinalizing = true
        defer { isFinalizing = false }
        finalizePosition()
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

    var symbol: String {
        switch self {
        case .topLeft: return "arrow.up.left"
        case .topRight: return "arrow.up.right"
        case .bottomLeft: return "arrow.down.left"
        case .bottomRight: return "arrow.down.right"
        }
    }
}
