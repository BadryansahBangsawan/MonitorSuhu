import AppKit
import Combine
import SwiftUI

struct SettingsView: View {
    @ObservedObject var store: SettingsStore
    @ObservedObject var sensors: SensorService
    @ObservedObject var updates: UpdateChecker
    var overlay: OverlayController
    var hotkeyMessage: String? = nil
    var restartHotkeys: () -> String? = { nil }

    @State private var autostartMessage: String?
    @State private var confirmReset = false
    @StateObject private var recorder = HotkeyRecorder()

    private static let accents: [(name: String, hex: String)] = [
        ("NVIDIA", AppSettings.nvidiaGreen),
        ("Cyan", "3DDCFF"),
        ("White", "FFFFFF"),
        ("Orange", "FF9F0A")
    ]

    var body: some View {
        TabView {
            general
                .tabItem { Label("General", systemImage: "slider.horizontal.3") }
            sensorsTab
                .tabItem { Label("Sensors", systemImage: "thermometer") }
            appearance
                .tabItem { Label("Appearance", systemImage: "paintpalette") }
        }
        .frame(minWidth: 520, minHeight: 440)
        .confirmationDialog(
            "Reset settings to defaults?",
            isPresented: $confirmReset,
            titleVisibility: .visible
        ) {
            Button("Reset", role: .destructive) { resetDefaults() }
            Button("Cancel", role: .cancel) {}
        } message: {
            Text("Overlay position and visibility stay as they are.")
        }
    }

    private var general: some View {
        settingsScroll {
            SettingsSection("Overlay") {
                SettingsToggleRow("Show overlay", isOn: $store.settings.overlayVisible) { visible in
                    if visible { overlay.show() } else { overlay.hide() }
                }
                SettingsRowDivider()
                SettingsToggleRow(
                    "Lock overlay",
                    isOn: $store.settings.locked,
                    help: "When locked, clicks pass through the HUD."
                ) { _ in
                    overlay.applyLock()
                }
                SettingsRowDivider()
                SettingsToggleRow("Start with macOS", isOn: $store.settings.startWithOs) { value in
                    autostartMessage = AutostartService.apply(value)
                }
            } caption: {
                if let autostartMessage, !autostartMessage.isEmpty {
                    SettingsCallout(autostartMessage)
                } else {
                    SettingsCaption("May ask for permission in System Settings → General → Login Items.")
                }
            }

            SettingsSection("Alerts") {
                SettingsToggleRow(
                    "Critical alerts",
                    isOn: $store.settings.alertsEnabled,
                    help: "Notification (or a beep if permission is denied) when a visible reading crosses critical."
                )
                SettingsRowDivider()
                Button("Mute 15 minutes") {
                    store.settings.muteAlerts()
                }
                .padding(.vertical, 8)
            } caption: {
                if let mute = store.settings.muteCaption() {
                    SettingsCallout(mute)
                } else {
                    SettingsCaption("HUD colors still change while muted. Mute lasts 15 minutes and survives relaunch.")
                }
            }

            SettingsSection("Position") {
                LazyVGrid(
                    columns: [GridItem(.flexible(), spacing: 8), GridItem(.flexible(), spacing: 8)],
                    spacing: 8
                ) {
                    ForEach(CornerPreset.allCases, id: \.self) { preset in
                        Button(action: { overlay.applyPreset(preset) }) {
                            Label(preset.title, systemImage: preset.symbol)
                                .labelStyle(.titleAndIcon)
                                .lineLimit(1)
                                .minimumScaleFactor(0.85)
                                .frame(maxWidth: .infinity)
                                .padding(.vertical, 5)
                        }
                        .buttonStyle(.bordered)
                        .controlSize(.regular)
                        .help("Move the HUD to the \(preset.title.lowercased()) corner.")
                    }
                }
                .padding(.vertical, 10)
            } caption: {
                SettingsCaption("Unlock, then drag the HUD. It snaps to edges and remembers the corner.")
            }

            SettingsSection("Shortcuts") {
                SettingsKeyRow(
                    "Toggle overlay",
                    chord: store.settings.toggleHotkey.display,
                    recording: recorder.slot == .toggle,
                    onStart: { recorder.begin(.toggle) }
                )
                SettingsRowDivider()
                SettingsKeyRow(
                    "Edit layout",
                    chord: store.settings.editHotkey.display,
                    recording: recorder.slot == .edit,
                    onStart: { recorder.begin(.edit) }
                )
            } caption: {
                if recorder.slot != nil {
                    SettingsCaption("Press a shortcut with a modifier. Esc cancels.")
                } else if let conflict = recorder.conflict, !conflict.isEmpty {
                    SettingsCallout(conflict)
                } else if let live = recorder.failedMessage, !live.isEmpty {
                    SettingsCallout(live)
                } else {
                    SettingsCaption("Click a shortcut to rebind. At least one modifier plus a letter or number.")
                }
            }

            SettingsSection("About") {
                HStack {
                    Text("Version")
                    Spacer()
                    Text(updates.currentVersion)
                        .foregroundStyle(.secondary)
                        .monospacedDigit()
                }
                .padding(.vertical, 8)
                SettingsRowDivider()
                if updates.installing {
                    VStack(alignment: .leading, spacing: 8) {
                        Text("Installing \(updates.latestVersion ?? "update")…")
                        ProgressView(value: updates.progress)
                    }
                    .padding(.vertical, 8)
                } else if updates.hasUpdate, let latest = updates.latestVersion {
                    Button(updates.latestAsset == nil ? "Download \(latest)…" : "Install \(latest)…") {
                        if updates.latestAsset == nil {
                            updates.openDownloadPage()
                        } else {
                            updates.apply()
                        }
                    }
                    .padding(.vertical, 8)
                } else {
                    Button(updates.checking ? "Checking…" : "Check for Updates…") {
                        updates.check(userInitiated: true)
                    }
                    .disabled(updates.checking)
                    .padding(.vertical, 8)
                }
            } caption: {
                if updates.hasUpdate, let latest = updates.latestVersion {
                    SettingsCallout(
                        updates.latestAsset == nil
                            ? "MonitorSuhu \(latest) is available. Download it from GitHub, then replace the app in Applications."
                            : "MonitorSuhu \(latest) is available. Install from here — settings stay put."
                    )
                } else {
                    SettingsCaption("Checks GitHub Releases and can install the macOS disk image from this window.")
                }
            }

            HStack {
                Button("Reset to defaults…") { confirmReset = true }
                Spacer()
            }
            .padding(.top, 4)
        }
        .onAppear {
            autostartMessage = AutostartService.statusMessage()
            recorder.failedMessage = hotkeyMessage
            recorder.otherChord = { slot in
                slot == .toggle ? store.settings.editHotkey : store.settings.toggleHotkey
            }
            recorder.apply = { slot, chord in
                if slot == .toggle {
                    store.settings.toggleHotkey = chord
                } else {
                    store.settings.editHotkey = chord
                }
                return restartHotkeys()
            }
        }
    }

    private var sensorsTab: some View {
        settingsScroll {
            SettingsSection("Show in HUD") {
                SettingsToggleRow("CPU", isOn: $store.settings.showCpu)
                SettingsRowDivider()
                SettingsToggleRow("GPU", isOn: $store.settings.showGpu)
                SettingsRowDivider()
                SettingsToggleRow("SSD", isOn: $store.settings.showSsd)
                SettingsRowDivider()
                SettingsToggleRow("Motherboard", isOn: $store.settings.showBoard)
                SettingsRowDivider()
                SettingsToggleRow(
                    "RAM",
                    isOn: $store.settings.showRam,
                    help: "Hidden in the HUD when this Mac has no RAM temperature sensor."
                )
                SettingsRowDivider()
                SettingsToggleRow(
                    "Fan",
                    isOn: $store.settings.showFan,
                    help: "Hidden in the HUD when this Mac has no fan RPM sensor."
                )
                SettingsRowDivider()
                SettingsToggleRow(
                    "CPU load",
                    isOn: $store.settings.showCpuLoad,
                    help: "Off by default. Hidden when this Mac has no CPU utilization reading."
                )
                SettingsRowDivider()
                SettingsToggleRow(
                    "GPU load",
                    isOn: $store.settings.showGpuLoad,
                    help: "Off by default. Hidden when this Mac has no GPU utilization reading."
                )
                SettingsRowDivider()
                SettingsToggleRow(
                    "Power",
                    isOn: $store.settings.showPower,
                    help: "Off by default. Hidden when this Mac has no system power reading."
                )
            } caption: {
                if !anySensorEnabled {
                    SettingsCallout("Turn on at least one sensor or the HUD shows NO SENSORS.")
                } else if sensors.snapshot.readings.isEmpty {
                    SettingsCallout("No temperature sensors found on this Mac yet. The HUD fills in after the first poll.")
                } else {
                    SettingsCaption("Extra rows stay off until you turn them on. Missing sensors stay omitted, same as RAM and fan.")
                }
            }

            SettingsSection("Assignments") {
                ForEach(SensorKind.allCases) { kind in
                    assignmentRow(kind)
                    if kind != SensorKind.allCases.last {
                        SettingsRowDivider()
                    }
                }
            } caption: {
                SettingsCaption("Auto uses name tokens (tdie, nand, …). Pick a row to pin that sensor.")
            }

            SettingsSection("Thresholds") {
                Grid(alignment: .leading, horizontalSpacing: 12, verticalSpacing: 8) {
                    GridRow {
                        Text("Sensor")
                        Color.clear
                            .gridCellUnsizedAxes(.vertical)
                            .frame(maxWidth: .infinity)
                        Text("Warn")
                            .gridColumnAlignment(.center)
                            .frame(width: 72)
                        Text("Crit")
                            .gridColumnAlignment(.center)
                            .frame(width: 72)
                    }
                    .font(.caption.weight(.semibold))
                    .foregroundStyle(.secondary)

                    GridRow {
                        Text("CPU")
                        Color.clear
                            .gridCellUnsizedAxes(.vertical)
                            .frame(maxWidth: .infinity)
                        thresholdField(.cpu, isWarn: true)
                        thresholdField(.cpu, isWarn: false)
                    }
                    GridRow {
                        Text("GPU")
                        Color.clear
                            .gridCellUnsizedAxes(.vertical)
                            .frame(maxWidth: .infinity)
                        thresholdField(.gpu, isWarn: true)
                        thresholdField(.gpu, isWarn: false)
                    }
                    GridRow {
                        Text("SSD")
                        Color.clear
                            .gridCellUnsizedAxes(.vertical)
                            .frame(maxWidth: .infinity)
                        thresholdField(.ssd, isWarn: true)
                        thresholdField(.ssd, isWarn: false)
                    }
                    GridRow {
                        Text("Board")
                        Color.clear
                            .gridCellUnsizedAxes(.vertical)
                            .frame(maxWidth: .infinity)
                        thresholdField(.board, isWarn: true)
                        thresholdField(.board, isWarn: false)
                    }
                    GridRow {
                        Text("RAM")
                        Color.clear
                            .gridCellUnsizedAxes(.vertical)
                            .frame(maxWidth: .infinity)
                        thresholdField(.ram, isWarn: true)
                        thresholdField(.ram, isWarn: false)
                    }
                    GridRow {
                        Text("Fan")
                        Color.clear
                            .gridCellUnsizedAxes(.vertical)
                            .frame(maxWidth: .infinity)
                        thresholdField(.fan, isWarn: true)
                        thresholdField(.fan, isWarn: false)
                    }
                    GridRow {
                        Text("CPU%")
                        Color.clear
                            .gridCellUnsizedAxes(.vertical)
                            .frame(maxWidth: .infinity)
                        thresholdField(.cpuLoad, isWarn: true)
                        thresholdField(.cpuLoad, isWarn: false)
                    }
                    GridRow {
                        Text("GPU%")
                        Color.clear
                            .gridCellUnsizedAxes(.vertical)
                            .frame(maxWidth: .infinity)
                        thresholdField(.gpuLoad, isWarn: true)
                        thresholdField(.gpuLoad, isWarn: false)
                    }
                    GridRow {
                        Text("PWR")
                        Color.clear
                            .gridCellUnsizedAxes(.vertical)
                            .frame(maxWidth: .infinity)
                        thresholdField(.power, isWarn: true)
                        thresholdField(.power, isWarn: false)
                    }
                }
                .padding(.vertical, 10)
            } caption: {
                SettingsCaption("Warn turns the reading yellow. Critical turns it red. Temperatures are °C, fans are RPM, load is %, power is watts.")
            }

            SettingsSection("Polling") {
                VStack(alignment: .leading, spacing: 8) {
                    HStack {
                        Text("Poll interval")
                        Spacer()
                        Text(verbatim: "\(Int(store.settings.pollIntervalMs)) ms")
                            .foregroundStyle(.secondary)
                            .monospacedDigit()
                    }
                    Slider(value: $store.settings.pollIntervalMs, in: 400...3000, step: 100)
                        .accessibilityLabel("Poll interval")
                }
                .padding(.vertical, 10)
            } caption: {
                SettingsCaption("How often sensors are read. 1000 ms is a good default.")
            }
        }
    }

    private var appearance: some View {
        settingsScroll {
            VStack(alignment: .leading, spacing: 8) {
                Text("Preview")
                    .font(.headline)
                HStack {
                    Spacer(minLength: 0)
                    OverlayView(store: store, sensors: sensors)
                    Spacer(minLength: 0)
                }
                .padding(.vertical, 20)
                .padding(.horizontal, 16)
                .frame(maxWidth: .infinity, minHeight: 96)
                .background(
                    RoundedRectangle(cornerRadius: 10, style: .continuous)
                        .fill(Color.black.opacity(0.55))
                )
                .overlay(
                    RoundedRectangle(cornerRadius: 10, style: .continuous)
                        .strokeBorder(Color.primary.opacity(0.06), lineWidth: 1)
                )
            }

            SettingsSection("Display") {
                SettingsToggleRow("Use Fahrenheit", isOn: $store.settings.useFahrenheit)
                SettingsRowDivider()
                SettingsToggleRow("Compact HUD", isOn: $store.settings.compactHud)
                SettingsRowDivider()
                SettingsToggleRow("Sparkline (30 samples)", isOn: $store.settings.showSparkline)
                SettingsRowDivider()
                VStack(alignment: .leading, spacing: 8) {
                    HStack {
                        Text("Opacity")
                        Spacer()
                        Text(verbatim: "\(Int((store.settings.overlayOpacity * 100).rounded()))%")
                            .foregroundStyle(.secondary)
                            .monospacedDigit()
                    }
                    Slider(value: $store.settings.overlayOpacity, in: 0.4...0.95, step: 0.01)
                        .accessibilityLabel("Opacity")
                }
                .padding(.vertical, 10)
                SettingsRowDivider()
                VStack(alignment: .leading, spacing: 8) {
                    HStack {
                        Text("Font size")
                        Spacer()
                        Text(verbatim: "\(Int(store.settings.fontSize.rounded())) pt")
                            .foregroundStyle(.secondary)
                            .monospacedDigit()
                    }
                    Slider(value: $store.settings.fontSize, in: 11...18, step: 1)
                        .accessibilityLabel("Font size")
                }
                .padding(.vertical, 10)
            } caption: {
                SettingsCaption("Compact puts every sensor on one line. Sparkline is the last 30 polls.")
            }

            SettingsSection("Accent") {
                HStack(spacing: 8) {
                    ForEach(Self.accents, id: \.hex) { item in
                        let selected = AppSettings.normalizeHex(store.settings.accentHex)
                            == AppSettings.normalizeHex(item.hex)
                        Button {
                            store.settings.accentHex = item.hex
                        } label: {
                            VStack(spacing: 8) {
                                ZStack {
                                    RoundedRectangle(cornerRadius: 6, style: .continuous)
                                        .fill(Color(hex: item.hex) ?? .green)
                                        .frame(height: 28)
                                        .overlay(
                                            RoundedRectangle(cornerRadius: 6, style: .continuous)
                                                .strokeBorder(
                                                    Color.primary.opacity(item.hex == "FFFFFF" ? 0.28 : 0.12),
                                                    lineWidth: 1
                                                )
                                        )
                                    if selected {
                                        Image(systemName: "checkmark")
                                            .font(.caption.weight(.bold))
                                            .foregroundStyle(item.hex == "FFFFFF" ? Color.black : Color.white)
                                    }
                                }
                                Text(item.name)
                                    .font(.caption)
                                    .foregroundStyle(selected ? .primary : .secondary)
                            }
                            .padding(8)
                            .frame(maxWidth: .infinity)
                            .background(
                                RoundedRectangle(cornerRadius: 14, style: .continuous)
                                    .fill(Color.primary.opacity(selected ? 0.08 : 0.04))
                            )
                            .overlay(
                                RoundedRectangle(cornerRadius: 14, style: .continuous)
                                    .strokeBorder(
                                        selected
                                            ? (Color(hex: item.hex) ?? .accentColor)
                                            : Color.primary.opacity(0.08),
                                        lineWidth: 1
                                    )
                            )
                        }
                        .buttonStyle(.plain)
                        .accessibilityLabel(item.name)
                        .accessibilityAddTraits(selected ? .isSelected : [])
                    }
                }
                .padding(.vertical, 10)
            }
        }
    }

    private var anySensorEnabled: Bool {
        store.settings.showCpu
            || store.settings.showGpu
            || store.settings.showSsd
            || store.settings.showBoard
            || store.settings.showRam
            || store.settings.showFan
            || store.settings.showCpuLoad
            || store.settings.showGpuLoad
            || store.settings.showPower
    }

    private func settingsScroll<Content: View>(@ViewBuilder content: () -> Content) -> some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 18) {
                content()
            }
            .padding(20)
            .frame(maxWidth: .infinity, alignment: .topLeading)
        }
    }


    private func thresholdField(_ kind: SensorKind, isWarn: Bool) -> some View {
        TextField(
            isWarn ? "Warn" : "Crit",
            value: Binding(
                get: {
                    let t = store.settings.thresholds(for: kind)
                    return isWarn ? t.warn : t.critical
                },
                set: { newValue in
                    if isWarn {
                        store.settings.setThresholds(for: kind, warn: newValue)
                    } else {
                        store.settings.setThresholds(for: kind, critical: newValue)
                    }
                }
            ),
            format: .number.precision(.fractionLength(0))
        )
        .multilineTextAlignment(.center)
        .frame(width: 72)
        .textFieldStyle(.roundedBorder)
        .gridColumnAlignment(.center)
        .accessibilityLabel("\(kind.hudLabel) \(isWarn ? "warn" : "critical")")
    }

    private func assignmentRow(_ kind: SensorKind) -> some View {
        let options = sensors.catalog.filter { $0.hint == kind.catalogHint }
        return HStack(alignment: .center, spacing: 12) {
            Text(kind.hudLabel)
                .frame(maxWidth: .infinity, alignment: .leading)
            Picker(kind.hudLabel, selection: Binding(
                get: { store.settings.sensorBindings[kind.rawValue] ?? "" },
                set: { store.settings.sensorBindings[kind.rawValue] = $0 }
            )) {
                Text("Auto").tag("")
                ForEach(options, id: \.id) { entry in
                    Text(entry.name).tag(entry.name)
                }
            }
            .labelsHidden()
            .pickerStyle(.menu)
            .frame(maxWidth: 240)
        }
        .padding(.vertical, 8)
        .accessibilityElement(children: .combine)
    }

    private func resetDefaults() {
        let position = store.settings.position
        let visible = store.settings.overlayVisible
        var next = AppSettings.default
        next.position = position
        next.overlayVisible = visible
        store.settings = next
        autostartMessage = AutostartService.apply(next.startWithOs)
        overlay.applyLock()
        if visible { overlay.show() } else { overlay.hide() }
    }
}

private struct SettingsSection<Content: View, Caption: View>: View {
    let title: String
    @ViewBuilder var content: () -> Content
    @ViewBuilder var caption: () -> Caption

    init(
        _ title: String,
        @ViewBuilder content: @escaping () -> Content,
        @ViewBuilder caption: @escaping () -> Caption
    ) {
        self.title = title
        self.content = content
        self.caption = caption
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 8) {
            Text(title)
                .font(.headline)
            VStack(alignment: .leading, spacing: 0) {
                content()
            }
            .padding(.horizontal, 12)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(
                RoundedRectangle(cornerRadius: 10, style: .continuous)
                    .fill(Color(nsColor: .controlBackgroundColor))
            )
            .overlay(
                RoundedRectangle(cornerRadius: 10, style: .continuous)
                    .strokeBorder(Color.primary.opacity(0.06), lineWidth: 1)
            )
            if Caption.self != EmptyView.self {
                caption()
                    .fixedSize(horizontal: false, vertical: true)
                    .padding(.horizontal, 4)
            }
        }
    }
}

extension SettingsSection where Caption == EmptyView {
    init(_ title: String, @ViewBuilder content: @escaping () -> Content) {
        self.init(title, content: content, caption: { EmptyView() })
    }
}

private struct SettingsToggleRow: View {
    let title: String
    @Binding var isOn: Bool
    var help: String?
    var onChange: ((Bool) -> Void)?

    init(
        _ title: String,
        isOn: Binding<Bool>,
        help: String? = nil,
        onChange: ((Bool) -> Void)? = nil
    ) {
        self.title = title
        self._isOn = isOn
        self.help = help
        self.onChange = onChange
    }

    var body: some View {
        // Label leading, switch trailing. A bare Toggle+.switch in a VStack
        // lays out iOS-style (switch on the left) and looks broken in a
        // macOS settings window.
        HStack(alignment: .center, spacing: 12) {
            Text(title)
                .frame(maxWidth: .infinity, alignment: .leading)
            Toggle(title, isOn: $isOn)
                .labelsHidden()
                .toggleStyle(.switch)
                .controlSize(.regular)
        }
        .padding(.vertical, 8)
        .contentShape(Rectangle())
        .help(help ?? "")
        .accessibilityElement(children: .combine)
        .onChange(of: isOn) { _, value in
            onChange?(value)
        }
    }
}

private struct SettingsRowDivider: View {
    var body: some View {
        Divider()
            .opacity(0.7)
    }
}

private struct SettingsKeyRow: View {
    let title: String
    let chord: String
    var recording: Bool = false
    var onStart: (() -> Void)? = nil

    init(_ title: String, chord: String, recording: Bool = false, onStart: (() -> Void)? = nil) {
        self.title = title
        self.chord = chord
        self.recording = recording
        self.onStart = onStart
    }

    var body: some View {
        HStack {
            Text(title)
            Spacer(minLength: 12)
            Text(recording ? "Press a shortcut…" : chord)
                .font(.body.monospaced())
                .foregroundStyle(recording ? Color.accentColor : Color.primary)
                .padding(.horizontal, 8)
                .padding(.vertical, 4)
                .background(
                    RoundedRectangle(cornerRadius: 6, style: .continuous)
                        .fill(Color.primary.opacity(recording ? 0.12 : 0.06))
                )
                .overlay(
                    RoundedRectangle(cornerRadius: 6, style: .continuous)
                        .strokeBorder(Color.primary.opacity(0.08), lineWidth: 1)
                )
                .accessibilityLabel(recording ? "Press a shortcut" : chord)
                .help("Click to rebind")
                .contentShape(Rectangle())
                .onTapGesture { onStart?() }
        }
        .padding(.vertical, 8)
    }
}

private final class HotkeyRecorder: ObservableObject {
    enum Slot: Equatable { case toggle, edit }

    @Published var slot: Slot?
    @Published var conflict: String?
    @Published var failedMessage: String?

    var otherChord: ((Slot) -> KeyChord)?
    var apply: ((Slot, KeyChord) -> String?)?

    private var monitor: Any?

    func begin(_ slot: Slot) {
        if self.slot == slot {
            cancel()
            return
        }
        conflict = nil
        self.slot = slot
        startMonitor()
    }

    func cancel() {
        slot = nil
        stopMonitor()
    }

    private func startMonitor() {
        stopMonitor()
        monitor = NSEvent.addLocalMonitorForEvents(matching: .keyDown) { [weak self] event in
            self?.handle(event) ?? event
        }
    }

    private func stopMonitor() {
        if let monitor {
            NSEvent.removeMonitor(monitor)
        }
        monitor = nil
    }

    private func handle(_ event: NSEvent) -> NSEvent? {
        guard slot != nil else { return event }
        if event.keyCode == 53 {
            DispatchQueue.main.async { self.cancel() }
            return nil
        }
        let flags = event.modifierFlags.intersection(.deviceIndependentFlagsMask)
        guard let chord = KeyChord.from(
            keyCode: UInt32(event.keyCode),
            control: flags.contains(.control),
            option: flags.contains(.option),
            shift: flags.contains(.shift),
            command: flags.contains(.command)
        ) else { return nil }

        let current = slot
        DispatchQueue.main.async {
            self.stopMonitor()
            self.slot = nil
            guard let current else { return }
            if let other = self.otherChord?(current), other == chord {
                self.conflict = "That shortcut is already used by the other action."
                return
            }
            self.conflict = nil
            self.failedMessage = self.apply?(current, chord)
        }
        return nil
    }

    deinit { stopMonitor() }
}

private struct SettingsCaption: View {
    let text: String

    init(_ text: String) {
        self.text = text
    }

    var body: some View {
        Text(text)
            .font(.caption)
            .foregroundStyle(.secondary)
            .fixedSize(horizontal: false, vertical: true)
    }
}

private struct SettingsCallout: View {
    let text: String

    init(_ text: String) {
        self.text = text
    }

    var body: some View {
        HStack(alignment: .top, spacing: 8) {
            Image(systemName: "exclamationmark.triangle.fill")
                .foregroundStyle(.orange)
                .accessibilityHidden(true)
            Text(text)
                .foregroundStyle(.primary)
                .fixedSize(horizontal: false, vertical: true)
        }
        .font(.caption)
        .padding(10)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(
            RoundedRectangle(cornerRadius: 8, style: .continuous)
                .fill(Color.orange.opacity(0.14))
        )
    }
}
