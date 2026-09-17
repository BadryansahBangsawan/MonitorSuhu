using Xunit;

namespace MonitorSuhu.Core.Tests;

public sealed class VersioningTests
{
    [Theory]
    [InlineData("1.0.8", "1.0.7", true)]
    [InlineData("1.0.8", "1.0.8", false)]
    [InlineData("1.0.7", "1.0.8", false)]
    [InlineData("v2.0.0", "1.9.9", true)]
    public void IsNewer(string latest, string current, bool expected) =>
        Assert.Equal(expected, Versioning.IsNewer(latest, current));

    [Fact]
    public void Trim_DropsBuildZero()
    {
        Assert.Equal("1.0.8", Versioning.Trim("1.0.8.0"));
    }
}
