using Xunit;

namespace MonitorSuhu.Core.Tests;

public sealed class VersioningTests
{
    [Theory]
    [InlineData("1.0.6", "1.0.5", true)]
    [InlineData("1.0.6", "1.0.6", false)]
    [InlineData("1.0.5", "1.0.6", false)]
    [InlineData("v2.0.0", "1.9.9", true)]
    public void IsNewer(string latest, string current, bool expected) =>
        Assert.Equal(expected, Versioning.IsNewer(latest, current));

    [Fact]
    public void Trim_DropsBuildZero()
    {
        Assert.Equal("1.0.6", Versioning.Trim("1.0.6.0"));
    }
}
