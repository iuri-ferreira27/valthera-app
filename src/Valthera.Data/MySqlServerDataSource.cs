using Dapper;
using MySqlConnector;
using Valthera.Core.Contracts;
using Valthera.Core.Models;

namespace Valthera.Data;

/// <summary>
/// Read-only <see cref="IServerDataSource"/> backed by the game server's MySQL/MariaDB database.
/// </summary>
/// <remarks>
/// Only <c>SELECT</c> statements are issued and every user-supplied value is a query parameter. The only text
/// spliced into SQL is the ranking column, which comes from the fixed whitelist in <see cref="HighscoreCategories"/>.
/// Deleted characters (<c>deletion = 1</c>) and staff characters (<c>group_id &gt;= 3</c>) are always excluded.
/// A read-only database user is recommended.
/// </remarks>
public sealed class MySqlServerDataSource : IServerDataSource, IDisposable
{
    /// <summary>Largest page size accepted.</summary>
    public const int MaxPageSize = 100;

    private const int CommandTimeoutSeconds = 10;
    private const string VisiblePlayers = "p.deletion = 0 AND p.group_id < 3";

    private readonly MySqlDataSource _source;

    /// <summary>Creates the data source. The connection string is never logged or exposed.</summary>
    public MySqlServerDataSource(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        _source = new MySqlDataSource(connectionString);
    }

    /// <inheritdoc />
    public async Task<HighscorePage> GetHighscoresAsync(
        HighscoreCategory category, int? vocationId, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var offset = (page - 1) * pageSize;

        // Whitelisted column name: safe to splice. Everything else is parameterized.
        var column = HighscoreCategories.GetColumn(category);
        var vocationFilter = vocationId is null ? string.Empty : " AND p.vocation = @vocation";

        var countSql = $"SELECT COUNT(*) FROM players p WHERE {VisiblePlayers}{vocationFilter}";
        var rowsSql =
            $"SELECT p.name AS Name, p.level AS Level, p.vocation AS Vocation, CAST(p.{column} AS SIGNED) AS Value " +
            $"FROM players p WHERE {VisiblePlayers}{vocationFilter} " +
            $"ORDER BY p.{column} DESC, p.level DESC, p.name ASC LIMIT @limit OFFSET @offset";

        var parameters = new { vocation = vocationId, limit = pageSize, offset };

        await using var connection = await _source.OpenConnectionAsync(ct).ConfigureAwait(false);
        var total = await connection.ExecuteScalarAsync<int>(Command(countSql, parameters, ct)).ConfigureAwait(false);
        var rows = await connection.QueryAsync<HighscoreRow>(Command(rowsSql, parameters, ct)).ConfigureAwait(false);

        var entries = rows
            .Select((r, i) => new HighscoreEntry(offset + i + 1, r.Name, r.Level, VocationCatalog.GetName(r.Vocation), r.Value))
            .ToList();
        return new HighscorePage(HighscoreCategories.GetKey(category), page, pageSize, total, entries);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<OnlinePlayer>> GetOnlinePlayersAsync(CancellationToken ct = default)
    {
        const string sql =
            "SELECT p.name AS Name, p.level AS Level, p.vocation AS Vocation " +
            "FROM players_online o INNER JOIN players p ON p.id = o.player_id " +
            $"WHERE {VisiblePlayers} ORDER BY p.level DESC, p.name ASC";

        await using var connection = await _source.OpenConnectionAsync(ct).ConfigureAwait(false);
        var rows = await connection.QueryAsync<OnlineRow>(Command(sql, null, ct)).ConfigureAwait(false);
        return rows.Select(r => new OnlinePlayer(r.Name, r.Level, VocationCatalog.GetName(r.Vocation))).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DeathEntry>> GetRecentDeathsAsync(int limit, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, MaxPageSize);
        const string sql =
            "SELECT p.name AS Player, d.level AS Level, d.killed_by AS KilledBy, " +
            "CAST(d.is_player AS SIGNED) AS ByPlayer, CAST(d.`time` AS SIGNED) AS Time " +
            "FROM player_deaths d INNER JOIN players p ON p.id = d.player_id " +
            $"WHERE {VisiblePlayers} ORDER BY d.`time` DESC LIMIT @limit";

        await using var connection = await _source.OpenConnectionAsync(ct).ConfigureAwait(false);
        var rows = await connection.QueryAsync<DeathRow>(Command(sql, new { limit }, ct)).ConfigureAwait(false);
        return rows.Select(ToDeath).ToList();
    }

    /// <inheritdoc />
    public async Task<PlayerProfile?> GetPlayerAsync(string name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        const string playerSql =
            "SELECT p.id AS Id, p.name AS Name, p.level AS Level, p.vocation AS Vocation, " +
            "p.maglevel AS MagicLevel, CAST(p.lastlogin AS SIGNED) AS LastLogin " +
            $"FROM players p WHERE p.name = @name AND {VisiblePlayers} LIMIT 1";
        const string onlineSql = "SELECT COUNT(*) FROM players_online WHERE player_id = @id";
        const string deathsSql =
            "SELECT p.name AS Player, d.level AS Level, d.killed_by AS KilledBy, " +
            "CAST(d.is_player AS SIGNED) AS ByPlayer, CAST(d.`time` AS SIGNED) AS Time " +
            "FROM player_deaths d INNER JOIN players p ON p.id = d.player_id " +
            "WHERE d.player_id = @id ORDER BY d.`time` DESC LIMIT 10";

        await using var connection = await _source.OpenConnectionAsync(ct).ConfigureAwait(false);
        var player = await connection.QuerySingleOrDefaultAsync<PlayerRow>(
            Command(playerSql, new { name = name.Trim() }, ct)).ConfigureAwait(false);
        if (player is null)
        {
            return null;
        }

        var online = await connection.ExecuteScalarAsync<int>(Command(onlineSql, new { id = player.Id }, ct)).ConfigureAwait(false);
        var deaths = await connection.QueryAsync<DeathRow>(Command(deathsSql, new { id = player.Id }, ct)).ConfigureAwait(false);

        return new PlayerProfile(
            player.Name,
            player.Level,
            VocationCatalog.GetName(player.Vocation),
            player.MagicLevel,
            player.LastLogin > 0 ? DateTimeOffset.FromUnixTimeSeconds(player.LastLogin) : null,
            online > 0,
            deaths.Select(ToDeath).ToList());
    }

    /// <inheritdoc />
    public async Task<int> GetOnlineCountAsync(CancellationToken ct = default)
    {
        const string sql =
            "SELECT COUNT(*) FROM players_online o INNER JOIN players p ON p.id = o.player_id " +
            $"WHERE {VisiblePlayers}";

        await using var connection = await _source.OpenConnectionAsync(ct).ConfigureAwait(false);
        return await connection.ExecuteScalarAsync<int>(Command(sql, null, ct)).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose() => _source.Dispose();

    private static CommandDefinition Command(string sql, object? parameters, CancellationToken ct) =>
        new(sql, parameters, commandTimeout: CommandTimeoutSeconds, cancellationToken: ct);

    private static DeathEntry ToDeath(DeathRow r) =>
        new(r.Player, r.Level, r.KilledBy ?? string.Empty, r.ByPlayer != 0, DateTimeOffset.FromUnixTimeSeconds(r.Time));

    private sealed class HighscoreRow
    {
        public string Name { get; set; } = string.Empty;
        public int Level { get; set; }
        public int Vocation { get; set; }
        public long Value { get; set; }
    }

    private sealed class OnlineRow
    {
        public string Name { get; set; } = string.Empty;
        public int Level { get; set; }
        public int Vocation { get; set; }
    }

    private sealed class DeathRow
    {
        public string Player { get; set; } = string.Empty;
        public int Level { get; set; }
        public string? KilledBy { get; set; }
        public long ByPlayer { get; set; }
        public long Time { get; set; }
    }

    private sealed class PlayerRow
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Level { get; set; }
        public int Vocation { get; set; }
        public int MagicLevel { get; set; }
        public long LastLogin { get; set; }
    }
}
