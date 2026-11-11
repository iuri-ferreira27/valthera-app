using Valthera.Core.Models;

namespace Valthera.Tests.Core;

public class HighscoreCategoryTests
{
    [Theory]
    [InlineData(HighscoreCategory.Level, "experience")]
    [InlineData(HighscoreCategory.MagicLevel, "maglevel")]
    [InlineData(HighscoreCategory.Fist, "skill_fist")]
    [InlineData(HighscoreCategory.Club, "skill_club")]
    [InlineData(HighscoreCategory.Sword, "skill_sword")]
    [InlineData(HighscoreCategory.Axe, "skill_axe")]
    [InlineData(HighscoreCategory.Distance, "skill_dist")]
    [InlineData(HighscoreCategory.Shielding, "skill_shielding")]
    [InlineData(HighscoreCategory.Fishing, "skill_fishing")]
    public void GetColumn_MapsEveryCategoryToItsWhitelistedColumn(HighscoreCategory category, string column) =>
        Assert.Equal(column, HighscoreCategories.GetColumn(category));

    [Fact]
    public void GetColumn_CoversEveryEnumValue()
    {
        foreach (var category in Enum.GetValues<HighscoreCategory>())
        {
            Assert.False(string.IsNullOrWhiteSpace(HighscoreCategories.GetColumn(category)));
        }
    }

    [Fact]
    public void GetColumn_RejectsUndefinedValues() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => HighscoreCategories.GetColumn((HighscoreCategory)999));

    [Theory]
    [InlineData("level", HighscoreCategory.Level)]
    [InlineData("LEVEL", HighscoreCategory.Level)]
    [InlineData("magiclevel", HighscoreCategory.MagicLevel)]
    [InlineData("maglevel", HighscoreCategory.MagicLevel)]
    [InlineData(" distance ", HighscoreCategory.Distance)]
    public void TryParse_AcceptsKnownKeys(string text, HighscoreCategory expected)
    {
        Assert.True(HighscoreCategories.TryParse(text, out var category));
        Assert.Equal(expected, category);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bogus")]
    [InlineData("level; DROP TABLE players")]
    [InlineData("1")]
    [InlineData("skill_fist")]
    public void TryParse_RejectsEverythingElse(string? text) =>
        Assert.False(HighscoreCategories.TryParse(text, out _));
}
