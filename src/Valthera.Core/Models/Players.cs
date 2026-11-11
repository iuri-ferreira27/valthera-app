namespace Valthera.Core.Models;

/// <summary>A single row of a highscore table.</summary>
/// <param name="Rank">1-based position in the ranking.</param>
/// <param name="Name">Character name.</param>
/// <param name="Level">Character level.</param>
/// <param name="Vocation">Vocation display name.</param>
/// <param name="Value">Ranked value (experience or skill level, depending on the category).</param>
public sealed record HighscoreEntry(int Rank, string Name, int Level, string Vocation, long Value);

/// <summary>One page of a highscore table.</summary>
/// <param name="Category">Category key, for example <c>level</c>.</param>
/// <param name="Page">1-based page number.</param>
/// <param name="PageSize">Requested page size.</param>
/// <param name="Total">Total number of ranked characters matching the filters.</param>
/// <param name="Entries">Entries of this page.</param>
public sealed record HighscorePage(
    string Category,
    int Page,
    int PageSize,
    int Total,
    IReadOnlyList<HighscoreEntry> Entries);

/// <summary>A character that is currently online.</summary>
public sealed record OnlinePlayer(string Name, int Level, string Vocation);

/// <summary>A recorded character death.</summary>
/// <param name="Player">Name of the character that died.</param>
/// <param name="Level">Level at the time of death.</param>
/// <param name="KilledBy">Name of the killer (monster or player).</param>
/// <param name="ByPlayer">Whether the killer was a player.</param>
/// <param name="Time">When the death happened.</param>
public sealed record DeathEntry(string Player, int Level, string KilledBy, bool ByPlayer, DateTimeOffset Time);

/// <summary>Public profile of a character.</summary>
public sealed record PlayerProfile(
    string Name,
    int Level,
    string Vocation,
    int MagicLevel,
    DateTimeOffset? LastLogin,
    bool Online,
    IReadOnlyList<DeathEntry> RecentDeaths);
