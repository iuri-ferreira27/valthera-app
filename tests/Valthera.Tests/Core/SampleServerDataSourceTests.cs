using Valthera.Core.Models;
using Valthera.Core.Sample;

namespace Valthera.Tests.Core;

public class SampleServerDataSourceTests
{
    private readonly SampleServerDataSource _data = new();

    [Fact]
    public async Task Highscores_AreOrderedByValueDescendingWithContiguousRanks()
    {
        var page = await _data.GetHighscoresAsync(HighscoreCategory.Level, null, 1, 100);

        Assert.Equal("level", page.Category);
        Assert.Equal(40, page.Total);
        Assert.Equal(40, page.Entries.Count);
        Assert.Equal(Enumerable.Range(1, 40), page.Entries.Select(e => e.Rank));
        Assert.Equal(page.Entries.Select(e => e.Value).OrderByDescending(v => v), page.Entries.Select(e => e.Value));
    }

    [Fact]
    public async Task Highscores_PaginateWithoutOverlap()
    {
        var first = await _data.GetHighscoresAsync(HighscoreCategory.Sword, null, 1, 15);
        var second = await _data.GetHighscoresAsync(HighscoreCategory.Sword, null, 2, 15);
        var third = await _data.GetHighscoresAsync(HighscoreCategory.Sword, null, 3, 15);

        Assert.Equal(15, first.Entries.Count);
        Assert.Equal(15, second.Entries.Count);
        Assert.Equal(10, third.Entries.Count);
        Assert.Equal(16, second.Entries[0].Rank);
        Assert.Equal(31, third.Entries[0].Rank);
        Assert.Equal(40, first.Entries.Concat(second.Entries).Concat(third.Entries).Select(e => e.Name).Distinct().Count());
    }

    [Fact]
    public async Task Highscores_FilterByVocation()
    {
        var page = await _data.GetHighscoresAsync(HighscoreCategory.Level, 8, 1, 50);

        Assert.NotEmpty(page.Entries);
        Assert.All(page.Entries, e => Assert.Equal("Elite Knight", e.Vocation));
        Assert.Equal(page.Entries.Count, page.Total);
    }

    [Fact]
    public async Task Highscores_ClampPageSizeAndPage()
    {
        var page = await _data.GetHighscoresAsync(HighscoreCategory.Level, null, -5, 1000);

        Assert.Equal(1, page.Page);
        Assert.Equal(100, page.PageSize);
    }

    [Fact]
    public async Task Highscores_WorkForEveryCategory()
    {
        foreach (var category in HighscoreCategories.All)
        {
            var page = await _data.GetHighscoresAsync(category, null, 1, 5);
            Assert.Equal(5, page.Entries.Count);
            Assert.Equal(HighscoreCategories.GetKey(category), page.Category);
        }
    }

    [Fact]
    public async Task Data_IsDeterministicAcrossInstances()
    {
        var a = await new SampleServerDataSource().GetHighscoresAsync(HighscoreCategory.Axe, null, 1, 100);
        var b = await new SampleServerDataSource().GetHighscoresAsync(HighscoreCategory.Axe, null, 1, 100);

        Assert.Equal(a.Entries, b.Entries);
    }

    [Fact]
    public async Task Players_AreFictionalAndOnlineCountMatches()
    {
        var online = await _data.GetOnlinePlayersAsync();

        Assert.Equal(40, SampleServerDataSource.CharacterNames.Distinct().Count());
        Assert.Equal(8, online.Count);
        Assert.Equal(online.Count, await _data.GetOnlineCountAsync());
    }

    [Fact]
    public async Task Deaths_AreNewestFirstAndRespectTheLimit()
    {
        var deaths = await _data.GetRecentDeathsAsync(10);

        Assert.Equal(10, deaths.Count);
        Assert.Equal(deaths.Select(d => d.Time).OrderByDescending(t => t), deaths.Select(d => d.Time));
    }

    [Fact]
    public async Task GetPlayer_IsCaseInsensitiveAndReturnsNullWhenMissing()
    {
        var profile = await _data.GetPlayerAsync("aldric STORMBORN");

        Assert.NotNull(profile);
        Assert.Equal("Aldric Stormborn", profile.Name);
        Assert.True(profile.Online);
        Assert.Null(await _data.GetPlayerAsync("Nobody Here"));
    }
}
