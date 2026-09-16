namespace MonitorSuhu.Core;

public static class Versioning
{
    public static bool IsNewer(string latest, string current)
    {
        var a = Parse(latest);
        var b = Parse(current);
        var n = Math.Max(a.Count, b.Count);
        for (var i = 0; i < n; i++)
        {
            var x = i < a.Count ? a[i] : 0;
            var y = i < b.Count ? b[i] : 0;
            if (x != y) return x > y;
        }
        return false;
    }

    public static string Trim(string version)
    {
        var parts = Parse(version);
        while (parts.Count > 3) parts.RemoveAt(parts.Count - 1);
        while (parts.Count > 1 && parts[^1] == 0 && parts.Count > 3) parts.RemoveAt(parts.Count - 1);
        if (parts.Count == 4 && parts[3] == 0) parts.RemoveAt(3);
        return string.Join('.', parts);
    }

    public static List<int> Parse(string version) =>
        version.Split('.')
            .Select(part => int.TryParse(new string(part.Where(char.IsDigit).ToArray()), out var n) ? n : 0)
            .ToList();
}
