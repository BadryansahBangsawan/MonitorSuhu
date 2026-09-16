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
}

public sealed class OverlayViewModel : ObservableObject
{
    private readonly SettingsStore _store;
    private HardwareSnapshot _last = new();
    public ObservableCollection<OverlayRow> Rows { get; } = [];
    public Brush Accent => Parse(_store.Settings.AccentHex);
    public double Opacity => _store.Settings.OverlayOpacity;
    public double FontSize => _store.Settings.FontSize;
    public bool Locked => _store.Settings.Locked;

    public OverlayViewModel(SettingsStore store, SensorService sensors)
    {
        _store = store;
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
                row.Label = reading.Label;
                row.Value = settings.FormatTemperature(reading.Celsius);
                row.Color = ColorFor(reading);
            }
        }

        OnPropertyChanged(nameof(Accent));
        OnPropertyChanged(nameof(Opacity));
        OnPropertyChanged(nameof(FontSize));
        OnPropertyChanged(nameof(Locked));
    }

    private OverlayRow MakeRow(SensorReading reading) => new()
    {
        Id = reading.Id,
        Label = reading.Label,
        Value = _store.Settings.FormatTemperature(reading.Celsius),
        Color = ColorFor(reading)
    };

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
        return Parse(_store.Settings.AccentHex);
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
