import SwiftUI

struct SettingsView: View {
    @ObservedObject var store: SettingsStore
    var overlay: OverlayController
    var onAutostart: (Bool) -> Void

    var body: some View {
        TabView {
            general
                .tabItem { Label("General", systemImage: "slider.horizontal.3") }
            sensors
                .tabItem { Label("Sensors", systemImage: "thermometer") }
            appearance
                .tabItem { Label("Appearance", systemImage: "paintpalette") }
        }
        .padding(16)
        .frame(minWidth: 520, minHeight: 480)
    }

    private var general: some View {
        // VStack instead of Form: macOS Form squeezes custom rows into a
        // trailing column, which truncated "Top right" / "Bottom right".
        VStack(alignment: .leading, spacing: 14) {
            Toggle("Show overlay", isOn: $store.settings.overlayVisible)
                .onChange(of: store.settings.overlayVisible) { _, visible in
                    if visible { overlay.show() } else { overlay.hide() }
                }
            Toggle("Lock overlay (click-through)", isOn: $store.settings.locked)
                .onChange(of: store.settings.locked) { _, _ in overlay.applyLock() }
            Toggle("Start with macOS", isOn: $store.settings.startWithOs)
                .onChange(of: store.settings.startWithOs) { _, value in onAutostart(value) }

            Text("Position")
                .font(.headline)
            LazyVGrid(columns: [GridItem(.flexible()), GridItem(.flexible())], spacing: 8) {
                ForEach(CornerPreset.allCases, id: \.self) { preset in
                    Button(action: { overlay.applyPreset(preset) }) {
                        Text(preset.title)
                            .frame(maxWidth: .infinity)
                    }
                    .buttonStyle(.bordered)
                    .controlSize(.regular)
                }
            }

            HStack {
                Text("Toggle overlay")
                Spacer()
                Text(store.settings.toggleHotkey.display)
                    .monospaced()
                    .foregroundStyle(.secondary)
            }
            HStack {
                Text("Edit layout")
                Spacer()
                Text(store.settings.editHotkey.display)
                    .monospaced()
                    .foregroundStyle(.secondary)
            }
            Text("Default shortcuts: ⌃⇧T show/hide, ⌃⇧E unlock and drag.")
                .font(.caption)
                .foregroundStyle(.secondary)
            Spacer(minLength: 0)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
    }

    private var sensors: some View {
        Form {
            Toggle("CPU", isOn: $store.settings.showCpu)
            Toggle("GPU", isOn: $store.settings.showGpu)
            Toggle("SSD", isOn: $store.settings.showSsd)
            Toggle("Motherboard / board", isOn: $store.settings.showBoard)
            Toggle("RAM (only if a sensor exists)", isOn: $store.settings.showRam)

            thresholdRow("CPU", kind: .cpu)
            thresholdRow("GPU", kind: .gpu)
            thresholdRow("SSD", kind: .ssd)
            thresholdRow("Board", kind: .board)

            Stepper(value: $store.settings.pollIntervalMs, in: 400...3000, step: 100) {
                // Text("… \(Int)") uses LocalizedStringKey and groups 1000 as "1.000".
                Text(verbatim: "Poll interval: \(Int(store.settings.pollIntervalMs)) ms")
            }
        }
    }

    private var appearance: some View {
        VStack(alignment: .leading, spacing: 14) {
            Toggle("Use Fahrenheit", isOn: $store.settings.useFahrenheit)

            HStack {
                Text("Opacity")
                Spacer()
                Text(verbatim: "\(Int((store.settings.overlayOpacity * 100).rounded()))%")
                    .foregroundStyle(.secondary)
                    .monospacedDigit()
            }
            Slider(value: $store.settings.overlayOpacity, in: 0.4...0.95)
                .accessibilityLabel("Opacity")

            HStack {
                Text("Font size")
                Spacer()
                Text(verbatim: "\(Int(store.settings.fontSize.rounded())) pt")
                    .foregroundStyle(.secondary)
                    .monospacedDigit()
            }
            Slider(value: $store.settings.fontSize, in: 11...18)
                .accessibilityLabel("Font size")

            Picker("Accent", selection: $store.settings.accentHex) {
                Text("NVIDIA green").tag(AppSettings.nvidiaGreen)
                Text("Cyan").tag("3DDCFF")
                Text("White").tag("FFFFFF")
                Text("Orange").tag("FF9F0A")
            }

            Spacer(minLength: 0)
        }
        .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .topLeading)
    }

    private func thresholdRow(_ title: String, kind: SensorKind) -> some View {
        let current = store.settings.thresholds(for: kind)
        return HStack {
            Text("\(title) warn/crit")
            Spacer()
            TextField("warn", value: Binding(
                get: { current.warn },
                set: { store.settings.thresholds[kind.rawValue] = Thresholds(warn: $0, critical: current.critical) }
            ), format: .number)
            .frame(width: 50)
            TextField("crit", value: Binding(
                get: { current.critical },
                set: { store.settings.thresholds[kind.rawValue] = Thresholds(warn: current.warn, critical: $0) }
            ), format: .number)
            .frame(width: 50)
        }
    }
}
