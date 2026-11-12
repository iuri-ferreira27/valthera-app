namespace Valthera.Core.Models;

/// <summary>Ranking categories supported by the highscores.</summary>
public enum HighscoreCategory
{
    Level,
    MagicLevel,
    Fist,
    Club,
    Sword,
    Axe,
    Distance,
    Shielding,
    Fishing,
}

/// <summary>Parsing and column mapping for <see cref="HighscoreCategory"/>.</summary>
/// <remarks>
/// The column mapping is a fixed whitelist: user text is never concatenated into SQL.
/// </remarks>
public static class HighscoreCategories
{
    private static readonly IReadOnlyDictionary<HighscoreCategory, string> Columns =
        new Dictionary<HighscoreCategory, string>
        {
            [HighscoreCategory.Level] = "experience",
            [HighscoreCategory.MagicLevel] = "maglevel",
            [HighscoreCategory.Fist] = "skill_fist",
            [HighscoreCategory.Club] = "skill_club",
            [HighscoreCategory.Sword] = "skill_sword",
            [HighscoreCategory.Axe] = "skill_axe",
            [HighscoreCategory.Distance] = "skill_dist",
            [HighscoreCategory.Shielding] = "skill_shielding",
            [HighscoreCategory.Fishing] = "skill_fishing",
        };

    private static readonly IReadOnlyDictionary<string, HighscoreCategory> Keys =
        new Dictionary<string, HighscoreCategory>(StringComparer.OrdinalIgnoreCase)
        {
            ["level"] = HighscoreCategory.Level,
            ["magiclevel"] = HighscoreCategory.MagicLevel,
            ["maglevel"] = HighscoreCategory.MagicLevel,
            ["fist"] = HighscoreCategory.Fist,
            ["club"] = HighscoreCategory.Club,
            ["sword"] = HighscoreCategory.Sword,
            ["axe"] = HighscoreCategory.Axe,
            ["distance"] = HighscoreCategory.Distance,
            ["shielding"] = HighscoreCategory.Shielding,
            ["fishing"] = HighscoreCategory.Fishing,
        };

    /// <summary>Gets all supported categories.</summary>
    public static IReadOnlyList<HighscoreCategory> All { get; } = Enum.GetValues<HighscoreCategory>();

    /// <summary>Tries to parse a category key (case-insensitive). Unknown or empty text is rejected.</summary>
    public static bool TryParse(string? text, out HighscoreCategory category)
    {
        category = default;
        return !string.IsNullOrWhiteSpace(text) && Keys.TryGetValue(text.Trim(), out category);
    }

    /// <summary>Gets the SQL column that holds the ranked value for a category.</summary>
    public static string GetColumn(HighscoreCategory category) =>
        Columns.TryGetValue(category, out var column)
            ? column
            : throw new ArgumentOutOfRangeException(nameof(category), category, "Unsupported highscore category.");

    /// <summary>Gets the lowercase public key of a category, for example <c>magiclevel</c>.</summary>
    public static string GetKey(HighscoreCategory category) => category.ToString().ToLowerInvariant();
}
