using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using MonitorSuhu.Core.Models;
using MonitorSuhu.Core.Services;
using MonitorSuhu.Linux.Services;

namespace MonitorSuhu.Linux.ViewModels;

public sealed class OverlayRow : ObservableObject
{
    public required string Id { get; init; }

    private string _label = "";
    public string Label
    {
        get => _label;
        set => SetProperty(ref _label, value);
    }

    private string _value = "";
    public string Value
    {
        get => _value;
        set => SetProperty(ref _value, value);
    }

    private IBrush _color = Brushes.LimeGreen;
    public IBrush Color
    {
        get => _color;
        set => SetProperty(ref _color, value);
    }

    private Points _points = new();
    public Points Points
    {
        get => _points;
        set => SetProperty(ref _points, value);
    }
}

public sealed class OverlayViewModel : ObservableObject
{
    private readonly SettingsStore _store;
    private readonly HwmonSensorService _sensors;
    private HardwareSnapshot _last = new();
    private string _accentHex = "";
    private IBrush _accentBrush = Brushes.LimeGreen;

    public ObservableCollection<OverlayRow> Rows { get; } = [];
    public IBrush Accent => AccentBrush;
    public double Opacity => _store.Settings.OverlayOpacity;
    public double FontSize => _store.Settings.FontSize;
    public bool Locked => _store.Settings.Locked;
    public bool CompactHud => _store.Settings.CompactHud;
    public bool ShowSparkline => _store.Settings.ShowSparkline;
    public bool ShowEmpty => Rows.Count == 0;
    public bool ShowCompact => CompactHud && Rows.Count > 0;
    public bool ShowStacked => !CompactHud && Rows.Count > 0;
    public IBrush ChromeBrush => Locked ? Brushes.Transparent : Accent;

    private string _compactLine = "";
    public string CompactLine
    {
        get => _compactLine;
        private set => SetProperty(ref _compactLine, value);
    }

    private IBrush _compactColor = Brushes.LimeGreen;
    public IBrush CompactColor
    {
        get => _compactColor;
        private set => SetProperty(ref _compactColor, value);
    }

    public OverlayViewModel(SettingsStore store, HwmonSensorService sensors)
    {
        _store = store;
        _sensors = sensors;
        sensors.Updated += snapshot =>
        {
            var ui = Dispatcher.UIThread;
            if (ui.CheckAccess())
            {
                Apply(snapshot);
                return;
            }
            ui.Post(() => Apply(snapshot));
        };
        Apply(sensors.Snapshot);
    }

    public void RefreshTheme()
    {
        Apply(_last);
        OnPropertyChanged(nameof(Accent));
        OnPropertyChanged(nameof(Opacity));
        OnPropertyChanged(nameof(FontSize));
        OnPropertyChanged(nameof(Locked));
        OnPropertyChanged(nameof(CompactHud));
        OnPropertyChanged(nameof(ShowSparkline));
        OnPropertyChanged(nameof(CompactLine));
        OnPropertyChanged(nameof(CompactColor));
        OnPropertyChanged(nameof(ShowEmpty));
        OnPropertyChanged(nameof(ShowCompact));
        OnPropertyChanged(nameof(ShowStacked));
        OnPropertyChanged(nameof(ChromeBrush));
    }

    public void Apply(HardwareSnapshot snapshot)
    {
        _last = snapshot;
        var settings = _store.Settings;
        var visible = snapshot.Readings.Where(r => settings.IsKindVisible(r.Kind)).ToList();
        var sameIds = Rows.Count == visible.Count
            && Rows.Select((row, i) => row.Id == visible[i].Id).All(x => x);

        if (!sameIds)
        {
            Rows.Clear();
            foreach (var reading in visible)
            {
                Rows.Add(MakeRow(reading));
            }
        }
        else
        {
            for (var i = 0; i < visible.Count; i++)
            {
                var reading = visible[i];
                var row = Rows[i];
                var value = settings.FormatValue(reading);
                var color = ColorFor(reading);
                if (row.Label != reading.Label) row.Label = reading.Label;
                if (row.Value != value) row.Value = value;
                if (!ReferenceEquals(row.Color, color)) row.Color = color;
                row.Points = BuildPoints(_sensors.History(reading.Kind));
            }
        }

        CompactLine = string.Join("  ", visible.Select(r => r.Label + " " + settings.FormatValue(r)));
        CompactColor = visible.Count == 0
            ? AccentBrush
            : ColorFor(ReadingLevelUtil.Worst(visible, settings));
        OnPropertyChanged(nameof(CompactHud));
        OnPropertyChanged(nameof(ShowSparkline));
        OnPropertyChanged(nameof(ShowEmpty));
        OnPropertyChanged(nameof(ShowCompact));
        OnPropertyChanged(nameof(ShowStacked));
        OnPropertyChanged(nameof(ChromeBrush));
    }

    private OverlayRow MakeRow(SensorReading reading) => new()
    {
        Id = reading.Id,
        Label = reading.Label,
        Value = _store.Settings.FormatValue(reading),
        Color = ColorFor(reading),
        Points = BuildPoints(_sensors.History(reading.Kind))
    };

    private static Points BuildPoints(IReadOnlyList<double> history)
    {
        if (history.Count <= 1) return new Points();
        const double width = 36;
        const double height = 12;
        var min = history.Min();
        var max = history.Max();
        var range = max - min;
        var midline = range == 0;
        var points = new Points();
        var last = history.Count - 1;
        for (var i = 0; i < history.Count; i++)
        {
            var x = i * (width - 1) / last;
            var y = midline
                ? height / 2
                : (height - 1) * (1 - (history[i] - min) / range);
            points.Add(new Point(x, y));
        }
        return points;
    }

    private IBrush ColorFor(SensorReading reading) =>
        ColorFor(ReadingLevelUtil.Of(reading, _store.Settings));

    private IBrush ColorFor(ReadingLevel level) => level switch
    {
        ReadingLevel.Critical => ResourceBrush("CritBrush", Brushes.Red),
        ReadingLevel.Warn => ResourceBrush("WarnBrush", Brushes.Gold),
        _ => AccentBrush
    };

    private IBrush AccentBrush
    {
        get
        {
            var hex = _store.Settings.AccentHex;
            if (!string.Equals(hex, _accentHex, StringComparison.OrdinalIgnoreCase))
            {
                _accentHex = hex;
                _accentBrush = Parse(hex);
            }
            return _accentBrush;
        }
    }

    private static IBrush ResourceBrush(string key, IBrush fallback)
    {
        var app = Application.Current;
        if (app is null) return fallback;
        if (app.TryGetResource(key, app.ActualThemeVariant, out var value) && value is IBrush brush)
        {
            return brush;
        }
        return fallback;
    }

    private static IBrush Parse(string hex)
    {
        if (Color.TryParse(hex, out var color))
        {
            return new SolidColorBrush(color);
        }
        return Brushes.LimeGreen;
    }
}
