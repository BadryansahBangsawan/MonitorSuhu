using System.Collections.ObjectModel;
using System.Windows.Media;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using CommunityToolkit.Mvvm.ComponentModel;
using MonitorSuhu.Core.Models;
using MonitorSuhu.Core.Services;
using MonitorSuhu.Sensors;

namespace MonitorSuhu.App.ViewModels;

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

    private Brush _color = Brushes.LimeGreen;
    public Brush Color
    {
        get => _color;
        set => SetProperty(ref _color, value);
    }

    private PointCollection _points = new();
    public PointCollection Points
    {
        get => _points;
        set => SetProperty(ref _points, value);
    }
}

public sealed class OverlayViewModel : ObservableObject
{
    private readonly SettingsStore _store;
    private readonly SensorService _sensors;
    private HardwareSnapshot _last = new();
    public ObservableCollection<OverlayRow> Rows { get; } = [];
    public Brush Accent => AccentBrush;
    private string _accentHex = "";
    private Brush _accentBrush = Brushes.LimeGreen;
    public double Opacity => _store.Settings.OverlayOpacity;
    public double FontSize => _store.Settings.FontSize;
    public bool Locked => _store.Settings.Locked;
    public bool CompactHud => _store.Settings.CompactHud;
    public bool ShowSparkline => _store.Settings.ShowSparkline;

    private string _compactLine = "";
    public string CompactLine
    {
        get => _compactLine;
        private set => SetProperty(ref _compactLine, value);
    }

    private Brush _compactColor = Brushes.LimeGreen;
    public Brush CompactColor
    {
        get => _compactColor;
        private set => SetProperty(ref _compactColor, value);
    }

    public OverlayViewModel(SettingsStore store, SensorService sensors)
    {
        _store = store;
        _sensors = sensors;
        sensors.Updated += snapshot =>
        {
            var app = System.Windows.Application.Current;
            if (app?.Dispatcher is not { } dispatcher) return;
            if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished) return;
            dispatcher.BeginInvoke(() => Apply(snapshot));
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

        CompactLine = string.Join("  ", visible.Select(r => $"{r.Label} {settings.FormatValue(r)}"));
        CompactColor = visible.Count == 0
            ? AccentBrush
            : ColorFor(visible.MaxBy(r => r.Celsius)!);
        OnPropertyChanged(nameof(CompactHud));
        OnPropertyChanged(nameof(ShowSparkline));
    }

    private OverlayRow MakeRow(SensorReading reading) => new()
    {
        Id = reading.Id,
        Label = reading.Label,
        Value = _store.Settings.FormatValue(reading),
        Color = ColorFor(reading),
        Points = BuildPoints(_sensors.History(reading.Kind))
    };

    private static PointCollection BuildPoints(IReadOnlyList<double> history)
    {
        if (history.Count <= 1) return new PointCollection();
        const double width = 36;
        const double height = 12;
        var min = history.Min();
        var max = history.Max();
        var range = max - min;
        var midline = range == 0;
        var points = new PointCollection(history.Count);
        var last = history.Count - 1;
        for (var i = 0; i < history.Count; i++)
        {
            var x = i * (width - 1) / last;
            var y = midline
                ? height / 2
                : (height - 1) * (1 - (history[i] - min) / range);
            points.Add(new System.Windows.Point(x, y));
        }
        return points;
    }

    private Brush ColorFor(SensorReading reading)
    {
        var t = _store.Settings.ThresholdsFor(reading.Kind);
        var app = System.Windows.Application.Current;
        if (reading.Celsius >= t.Critical)
        {
            return app?.TryFindResource("CritBrush") as Brush ?? Brushes.Red;
        }
        if (reading.Celsius >= t.Warn)
        {
            return app?.TryFindResource("WarnBrush") as Brush ?? Brushes.Gold;
        }
        return AccentBrush;
    }

    private Brush AccentBrush
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

    private static Brush Parse(string hex)
    {
        try
        {
            return (Brush)new BrushConverter().ConvertFromString(hex)!;
        }
        catch
        {
            return Brushes.LimeGreen;
        }
    }
}
