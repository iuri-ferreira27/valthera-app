using Valthera.Core.Models;

namespace Valthera.Tests.Core;

public class VocationCatalogTests
{
    [Theory]
    [InlineData(0, "None")]
    [InlineData(1, "Sorcerer")]
    [InlineData(8, "Elite Knight")]
    [InlineData(100, "Archer")]
    [InlineData(138, "Thunderlord")]
    [InlineData(157, "Caçador")]
    [InlineData(159, "Lâmina")]
    public void GetName_ReturnsKnownVocation(int id, string expected) =>
        Assert.Equal(expected, VocationCatalog.GetName(id));

    [Theory]
    [InlineData(9, "Unknown (9)")]
    [InlineData(-1, "Unknown (-1)")]
    [InlineData(9999, "Unknown (9999)")]
    public void GetName_ReturnsUnknownForMissingId(int id, string expected) =>
        Assert.Equal(expected, VocationCatalog.GetName(id));

    [Fact]
    public void All_ContainsEveryVocationFromZeroToEightAndOneHundredToOneFiftyNine()
    {
        var expected = Enumerable.Range(0, 9).Concat(Enumerable.Range(100, 60));
        Assert.Equal(expected.OrderBy(i => i), VocationCatalog.All.Keys.OrderBy(i => i));
    }
}
