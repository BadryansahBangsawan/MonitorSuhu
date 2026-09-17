using MonitorSuhu.Core.Models;
using Xunit;

namespace MonitorSuhu.Core.Tests;

public sealed class KeyChordTests
{
    [Fact]
    public void TryCreate_RequiresModifierAndLetterOrDigit()
    {
        Assert.Null(KeyChord.TryCreate(0x54, false, false, false, false));
        Assert.Null(KeyChord.TryCreate(0x09, true, false, false, false));
        Assert.Null(KeyChord.TryCreate(0x20, true, false, false, false));

        var chord = KeyChord.TryCreate(0x54, true, true, false, false);
        Assert.NotNull(chord);
        Assert.Equal("Ctrl+Shift+T", chord!.Display);
    }

    [Fact]
    public void TryCreate_AcceptsDigits()
    {
        var chord = KeyChord.TryCreate(0x31, true, false, false, false);
        Assert.Equal("Ctrl+1", chord!.Display);
    }

    [Fact]
    public void Equals_MatchesModifiersAndKey()
    {
        var a = KeyChord.TryCreate(0x54, true, true, false, false)!;
        var b = KeyChord.TryCreate(0x54, true, true, false, false)!;
        var c = KeyChord.TryCreate(0x45, true, true, false, false)!;

        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }
}
