using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using MonitorSuhu.Core.Models;
using MonitorSuhu.Core.Services;
using MonitorSuhu.Sensors;

namespace MonitorSuhu.App.ViewModels;

public sealed class OverlayRow : ObservableObject
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required string Value { get; init; }
    public required Brush Color { get; init; }
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
            System.Windows.Application.Current.Dispatcher.Invoke(() => Apply(snapshot));
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
        Rows.Clear();
        foreach (var reading in visible)
        {
            Rows.Add(new OverlayRow
            {
                Id = reading.Id,
                Label = reading.Label,
                Value = settings.FormatTemperature(reading.Celsius),
                Color = ColorFor(reading)
            });
        }
        OnPropertyChanged(nameof(Accent));
        OnPropertyChanged(nameof(Opacity));
        OnPropertyChanged(nameof(FontSize));
        OnPropertyChanged(nameof(Locked));
    }

    private Brush ColorFor(SensorReading reading)
    {
        var t = _store.Settings.ThresholdsFor(reading.Kind);
        if (reading.Celsius >= t.Critical) return (Brush)System.Windows.Application.Current.FindResource("CritBrush");
        if (reading.Celsius >= t.Warn) return (Brush)System.Windows.Application.Current.FindResource("WarnBrush");
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
