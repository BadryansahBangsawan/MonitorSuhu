using MonitorSuhu.Core.Models;

namespace MonitorSuhu.Sensors;

public sealed record CatalogEntry(string Id, string Name, double Value, CatalogHint Hint)
{
    public bool IsFan => Hint == CatalogHint.Fan;
}
