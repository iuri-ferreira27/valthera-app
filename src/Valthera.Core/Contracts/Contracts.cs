using Valthera.Core.Models;

namespace Valthera.Core.Contracts;

/// <summary>Read-only access to game data (rankings, players, deaths).</summary>
public interface IServerDataSource
{
    /// <summary>Gets one page of a highscore table, optionally filtered by vocation id.</summary>
    Task<HighscorePage> GetHighscoresAsync(
        HighscoreCategory category, int? vocationId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>Gets the characters currently online.</summary>
    Task<IReadOnlyList<OnlinePlayer>> GetOnlinePlayersAsync(CancellationToken ct = default);

    /// <summary>Gets the most recent deaths, newest first.</summary>
    Task<IReadOnlyList<DeathEntry>> GetRecentDeathsAsync(int limit, CancellationToken ct = default);

    /// <summary>Gets a character profile by exact name (case-insensitive), or <c>null</c> when it does not exist.</summary>
    Task<PlayerProfile?> GetPlayerAsync(string name, CancellationToken ct = default);

    /// <summary>Gets the number of characters currently online.</summary>
    Task<int> GetOnlineCountAsync(CancellationToken ct = default);
}

/// <summary>Provides the live server status.</summary>
public interface IServerStatusProvider
{
    /// <summary>Gets the current status. Connectivity problems are reported as an offline server, not as exceptions.</summary>
    Task<ServerStatus> GetStatusAsync(CancellationToken ct = default);
}

/// <summary>Read-only catalog of documented spells.</summary>
public interface ISpellCatalog
{
    /// <summary>Gets all spell classes.</summary>
    IReadOnlyList<SpellClass> GetClasses();

    /// <summary>Gets spell summaries, optionally limited to one class (case-insensitive).</summary>
    IReadOnlyList<SpellSummary> GetSpells(string? spellClass = null);

    /// <summary>Gets one spell, or <c>null</c> when it does not exist.</summary>
    SpellDetail? GetSpell(string spellClass, string name);
}
