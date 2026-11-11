using Valthera.Core.Csv;
using Valthera.Core.Models;

namespace Valthera.Tests.Core;

public class HighscoreCsvTests
{
    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("with,comma", "\"with,comma\"")]
    [InlineData("with \"quote\"", "\"with \"\"quote\"\"\"")]
    [InlineData("line\nbreak", "\"line\nbreak\"")]
    [InlineData("", "")]
    public void Escape_QuotesOnlyWhenNeeded(string input, string expected) =>
        Assert.Equal(expected, HighscoreCsv.Escape(input));

    [Fact]
    public void Build_WritesHeaderAndEscapedRows()
    {
        var csv = HighscoreCsv.Build(
        [
            new HighscoreEntry(1, "Aldric, \"The Wall\"", 480, "Elite Knight", 1820295800),
            new HighscoreEntry(2, "Brenna", 471, "Master Sorcerer", 1719402000),
        ]);

        Assert.Equal(
            "Rank,Name,Level,Vocation,Value\r\n" +
            "1,\"Aldric, \"\"The Wall\"\"\",480,Elite Knight,1820295800\r\n" +
            "2,Brenna,471,Master Sorcerer,1719402000\r\n",
            csv);
    }

    [Fact]
    public void Build_WithNoRowsStillWritesTheHeader() =>
        Assert.Equal("Rank,Name,Level,Vocation,Value\r\n", HighscoreCsv.Build([]));
}
