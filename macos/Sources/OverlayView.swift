import SwiftUI

struct OverlayView: View {
    @ObservedObject var store: SettingsStore
    @ObservedObject var sensors: SensorService

    private var rows: [SensorReading] {
        sensors.snapshot.readings.filter { store.settings.isKindVisible($0.kind) }
    }

    var body: some View {
        let shape = RoundedRectangle(cornerRadius: 10, style: .continuous)
        return VStack(alignment: .leading, spacing: 4) {
            if rows.isEmpty {
                Text("No sensors")
                    .fontWeight(.regular)
                    .foregroundStyle(.white.opacity(0.55))
            } else if store.settings.compactHud {
                Text(rows.map { "\($0.label) \(store.settings.displayValue($0))" }.joined(separator: "  "))
                    .foregroundStyle(color(for: ReadingLevel.worst(rows, settings: store.settings)))
                    .monospacedDigit()
            } else {
                ForEach(rows) { row in
                    HStack(spacing: 14) {
                        Text(row.label)
                            .fontWeight(.regular)
                            .foregroundStyle(.white.opacity(0.65))
                        if store.settings.showSparkline {
                            Sparkline(values: sensors.history(for: row.kind))
                                .stroke(color(for: row), style: StrokeStyle(lineWidth: 1.25, lineJoin: .round))
                                .frame(width: 36, height: 12)
                        }
                        Spacer(minLength: 6)
                        Text(store.settings.displayValue(row))
                            .foregroundStyle(color(for: row))
                            .monospacedDigit()
                    }
                }
            }
        }
        .font(.system(size: store.settings.fontSize, weight: .semibold, design: .default))
        .padding(.horizontal, 12)
        .padding(.vertical, 9)
        .background(Color(red: 0.043, green: 0.043, blue: 0.051).opacity(store.settings.overlayOpacity), in: shape)
        .overlay(
            shape.strokeBorder(
                store.settings.locked ? Color.white.opacity(0.12) : store.settings.accentColor.opacity(0.9),
                lineWidth: 1
            )
        )
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
