import AppKit
import SwiftUI
import Combine

final class AppDelegate: NSObject, NSApplicationDelegate {
    private let store = SettingsStore()
    private let sensors = SensorService()
    private let alertGate = AlertGate()
    private let hotkeys = HotkeyService()
    private let updates = UpdateChecker()
    private var overlay: OverlayController!
    private var statusItem: NSStatusItem?
    private var settingsWindow: NSWindow?
    private var updateMenuItem: NSMenuItem?
    private var cancellables = Set<AnyCancellable>()

    func applicationDidFinishLaunching(_ notification: Notification) {
        overlay = OverlayController(store: store, sensors: sensors)
        restartSensors()
        installStatusItem()
        registerHotkeys()
        if store.settings.startWithOs {
            if let message = AutostartService.apply(true) {
                NSLog("MonitorSuhu: autostart: \(message)")
            }
        }

        store.$settings
            .map(\.pollIntervalMs)
            .removeDuplicates()
            .sink { [weak self] _ in self?.restartSensors() }
            .store(in: &cancellables)

        store.$settings
            .map(\.sensorBindings)
            .removeDuplicates()
            .sink { [weak self] _ in self?.restartSensors() }
            .store(in: &cancellables)

        sensors.$snapshot
            .receive(on: DispatchQueue.main)
            .sink { [weak self] _ in
                guard let self else { return }
                self.refreshStatusItem()
                self.alertGate.evaluate(readings: self.sensors.snapshot.readings, settings: self.store.settings)
            }
            .store(in: &cancellables)

        store.$settings
            .receive(on: DispatchQueue.main)
            .sink { [weak self] _ in self?.refreshStatusItem() }
            .store(in: &cancellables)

        if store.settings.overlayVisible {
            overlay.show()
        }

        updates.$hasUpdate
            .receive(on: DispatchQueue.main)
            .sink { [weak self] available in
                guard let self else { return }
                if available, let latest = self.updates.latestVersion {
                    self.updateMenuItem?.title = "Download \(latest)…"
                } else {
                    self.updateMenuItem?.title = "Check for Updates…"
                }
            }
            .store(in: &cancellables)

        DispatchQueue.main.asyncAfter(deadline: .now() + 2) { [weak self] in
            self?.updates.check()
        }
        NSLog("MonitorSuhu: launched overlayVisible=%@ sensors=%d toggle=%@ edit=%@",
              store.settings.overlayVisible ? "yes" : "no",
              sensors.snapshot.readings.count,
              store.settings.toggleHotkey.display,
              store.settings.editHotkey.display)
    }

    func applicationShouldHandleReopen(_ sender: NSApplication, hasVisibleWindows flag: Bool) -> Bool {
        openSettings()
        return true
    }

    func applicationShouldTerminateAfterLastWindowClosed(_ sender: NSApplication) -> Bool {
        false
    }

    func applicationWillTerminate(_ notification: Notification) {
        store.saveNow()
        hotkeys.stop()
        sensors.stop()
    }

    private func restartSensors() {
        sensors.start(
            intervalMs: store.settings.pollIntervalMs,
            bindings: { [weak self] in self?.store.settings.sensorBindings ?? [:] }
        )
    }

    private func installStatusItem() {
        let item = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
        item.menu = buildMenu()
        statusItem = item
        refreshStatusItem()
    }

    private func refreshStatusItem() {
        guard let button = statusItem?.button else { return }
        let cpu: SensorReading? = store.settings.isKindVisible(.cpu)
            ? sensors.snapshot.readings.first(where: { $0.kind == .cpu })
            : nil
        let title = store.settings.menuBarTitle(cpuCelsius: cpu?.celsius)
        let critical = cpu.map { $0.celsius >= store.settings.thresholds(for: .cpu).critical } ?? false
        button.attributedTitle = NSAttributedString(
            string: title,
            attributes: [
                .font: NSFont.menuBarFont(ofSize: 0),
                .foregroundColor: critical ? NSColor.systemRed : NSColor.labelColor
            ]
        )
        button.toolTip = cpu.map { store.settings.displayValue($0) } ?? "MonitorSuhu"
    }

    private func buildMenu() -> NSMenu {
        let menu = NSMenu()
        menu.addItem(item("Show / Hide Overlay", #selector(toggleOverlay), key: "t"))
        menu.addItem(item("Edit Layout", #selector(editLayout), key: "e"))
        menu.addItem(.separator())
        menu.addItem(item("Settings…", #selector(openSettings), key: ","))
        let check = NSMenuItem(title: "Check for Updates…", action: #selector(checkForUpdates), keyEquivalent: "")
        check.target = self
        menu.addItem(check)
        updateMenuItem = check
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

    @objc func checkForUpdates() {
        if updates.hasUpdate {
            updates.openDownloadPage()
            return
        }
        updates.check(userInitiated: true)
    }

    @objc func openSettings() {
        if settingsWindow == nil {
            let root = SettingsView(store: store, sensors: sensors, updates: updates, overlay: overlay)
            let hosting = NSHostingController(rootView: root)
            let window = NSWindow(contentViewController: hosting)
            window.title = "MonitorSuhu Settings"
            window.styleMask = [.titled, .closable, .miniaturizable, .resizable]
            window.isReleasedWhenClosed = false
            window.setContentSize(NSSize(width: 560, height: 640))
            window.minSize = NSSize(width: 520, height: 500)
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
