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
                } else if store.settings.compactHud {
                    Text(rows.map { "\($0.label) \(store.settings.displayValue($0))" }.joined(separator: "  "))
                        .foregroundStyle(color(for: ReadingLevel.worst(rows, settings: store.settings)))
                } else {
                    ForEach(rows) { row in
                        HStack(spacing: 16) {
                            Text(row.label)
                                .foregroundStyle(.white.opacity(0.78))
                            if store.settings.showSparkline {
                                Sparkline(values: sensors.history(for: row.kind))
                                    .stroke(color(for: row), lineWidth: 1)
                                    .frame(width: 36, height: 12)
                            }
                            Spacer(minLength: 12)
                            Text(store.settings.displayValue(row))
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
        color(for: ReadingLevel.of(row, settings: store.settings))
    }

    private func color(for level: ReadingLevel) -> Color {
        switch level {
        case .critical: return Color(red: 1, green: 0.23, blue: 0.19)
        case .warn: return Color(red: 0.96, green: 0.77, blue: 0.09)
        case .ok: return store.settings.accentColor
        }
    }
}

private struct Sparkline: Shape {
    let values: [Double]

    func path(in rect: CGRect) -> Path {
        guard values.count >= 2 else { return Path() }
        let lo = values.min() ?? 0
        let hi = values.max() ?? 0
        let span = hi - lo
        let n = values.count
        var path = Path()
        for i in 0..<n {
            let x = rect.minX + rect.width * CGFloat(i) / CGFloat(n - 1)
            let y: CGFloat
            if span == 0 {
                y = rect.midY
            } else {
                let t = (values[i] - lo) / span
                y = rect.maxY - rect.height * CGFloat(t)
            }
            let point = CGPoint(x: x, y: y)
            if i == 0 {
                path.move(to: point)
            } else {
                path.addLine(to: point)
            }
        }
        return path
    }
}
