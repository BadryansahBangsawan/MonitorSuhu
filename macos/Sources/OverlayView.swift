import SwiftUI

struct OverlayView: View {
    @ObservedObject var store: SettingsStore
    @ObservedObject var sensors: SensorService

    private var rows: [SensorReading] {
        sensors.snapshot.readings.filter { store.settings.isKindVisible($0.kind) }
    }

    var body: some View {
        HStack(alignment: .top, spacing: 0) {
            Rectangle()
                .fill(store.settings.accentColor)
                .frame(width: 3)

            VStack(alignment: .leading, spacing: 3) {
                if rows.isEmpty {
                    Text("NO SENSORS")
                        .font(.system(size: store.settings.fontSize - 1, weight: .semibold, design: .monospaced))
                        .foregroundStyle(.white.opacity(0.55))
                } else {
                    ForEach(rows) { row in
                        HStack(spacing: 16) {
                            Text(row.label)
                                .foregroundStyle(.white.opacity(0.78))
                            Spacer(minLength: 12)
                            Text(store.settings.displayTemperature(row.celsius))
                                .foregroundStyle(color(for: row))
                                .monospacedDigit()
                        }
                    }
                }
            }
            .font(.system(size: store.settings.fontSize, weight: .semibold, design: .default))
            .padding(.horizontal, 10)
            .padding(.vertical, 8)
        }
        .background(Color.black.opacity(store.settings.overlayOpacity))
        .overlay(
            RoundedRectangle(cornerRadius: 2)
                .stroke(store.settings.locked ? Color.clear : store.settings.accentColor.opacity(0.85), lineWidth: 1)
        )
        .clipShape(RoundedRectangle(cornerRadius: 2))
        .shadow(color: .black.opacity(0.35), radius: 8, y: 2)
        .fixedSize()
    }

    private func color(for row: SensorReading) -> Color {
        let t = store.settings.thresholds(for: row.kind)
        if row.celsius >= t.critical { return Color(red: 1, green: 0.23, blue: 0.19) }
        if row.celsius >= t.warn { return Color(red: 0.96, green: 0.77, blue: 0.09) }
        return store.settings.accentColor
    }
}
