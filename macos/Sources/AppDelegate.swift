import AppKit
import SwiftUI
import Combine

final class AppDelegate: NSObject, NSApplicationDelegate {
    private let store = SettingsStore()
    private let sensors = SensorService()
    private let hotkeys = HotkeyService()
    private var overlay: OverlayController!
    private var statusItem: NSStatusItem?
    private var settingsWindow: NSWindow?
    private var cancellables = Set<AnyCancellable>()

    func applicationDidFinishLaunching(_ notification: Notification) {
        overlay = OverlayController(store: store, sensors: sensors)
        sensors.start(intervalMs: store.settings.pollIntervalMs)
        installStatusItem()
        registerHotkeys()
        if store.settings.startWithOs {
            AutostartService.apply(true)
        }

        store.$settings
            .map(\.pollIntervalMs)
            .removeDuplicates()
            .sink { [weak self] ms in self?.sensors.start(intervalMs: ms) }
            .store(in: &cancellables)

        if store.settings.overlayVisible {
            overlay.show()
        }
        NSLog("MonitorSuhu: launched overlayVisible=%@ sensors=%d toggle=%@ edit=%@",
              store.settings.overlayVisible ? "yes" : "no",
              sensors.snapshot.readings.count,
              store.settings.toggleHotkey.display,
              store.settings.editHotkey.display)
    }

    func applicationWillTerminate(_ notification: Notification) {
        store.saveNow()
        hotkeys.stop()
        sensors.stop()
    }

    private func installStatusItem() {
        let item = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
        if let button = item.button {
            button.title = "°C"
            button.toolTip = "MonitorSuhu"
        }
        item.menu = buildMenu()
        statusItem = item
    }

    private func buildMenu() -> NSMenu {
        let menu = NSMenu()
        menu.addItem(item("Show / Hide Overlay", #selector(toggleOverlay), key: "t"))
        menu.addItem(item("Edit Layout", #selector(editLayout), key: "e"))
        menu.addItem(.separator())
        menu.addItem(item("Settings…", #selector(openSettings), key: ","))
        menu.addItem(.separator())
        let quit = NSMenuItem(title: "Quit MonitorSuhu", action: #selector(NSApplication.terminate(_:)), keyEquivalent: "q")
        quit.keyEquivalentModifierMask = [.command]
        menu.addItem(quit)
        return menu
    }

    private func item(_ title: String, _ sel: Selector, key: String) -> NSMenuItem {
        let item = NSMenuItem(title: title, action: sel, keyEquivalent: key)
        item.keyEquivalentModifierMask = [.control, .shift]
        item.target = self
        return item
    }

    private func registerHotkeys() {
        hotkeys.start(
            toggle: store.settings.toggleHotkey,
            edit: store.settings.editHotkey,
            onToggle: { [weak self] in self?.toggleOverlay() },
            onEdit: { [weak self] in self?.editLayout() }
        )
    }

    @objc func toggleOverlay() {
        overlay.toggleVisible()
    }

    @objc func editLayout() {
        store.settings.locked = false
        overlay.applyLock()
        overlay.show()
    }

    @objc func openSettings() {
        if settingsWindow == nil {
            let root = SettingsView(store: store, overlay: overlay) { [weak self] enabled in
                AutostartService.apply(enabled)
                self?.store.settings.startWithOs = enabled
            }
            let hosting = NSHostingController(rootView: root)
            let window = NSWindow(contentViewController: hosting)
            window.title = "MonitorSuhu Settings"
            window.styleMask = [.titled, .closable, .miniaturizable, .resizable]
            window.isReleasedWhenClosed = false
            window.setContentSize(NSSize(width: 540, height: 520))
            window.minSize = NSSize(width: 480, height: 420)
            window.center()
            window.delegate = self
            settingsWindow = window
        }
        settingsWindow?.makeKeyAndOrderFront(nil)
        NSApp.activate(ignoringOtherApps: true)
    }
}

extension AppDelegate: NSWindowDelegate {
    func windowWillClose(_ notification: Notification) {
        if notification.object as? NSWindow === settingsWindow {
            settingsWindow = nil
        }
    }
}
