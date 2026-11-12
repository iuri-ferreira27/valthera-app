namespace Valthera.Api.Configuration;

/// <summary>Where the API reads its game data from.</summary>
public enum DataSourceKind
{
    /// <summary>Deterministic fictional data embedded in the application (default).</summary>
    Sample,

    /// <summary>The game server's MySQL/MariaDB database (read-only).</summary>
    MySql,
}

/// <summary>Strongly typed view of the <c>Valthera</c> configuration section.</summary>
public sealed class ValtheraOptions
{
    /// <summary>Name of the configuration section.</summary>
    public const string SectionName = "Valthera";

    /// <summary>Name of the connection string used in <see cref="DataSourceKind.MySql"/> mode.</summary>
    public const string ConnectionStringName = "Valthera";

    /// <summary>Selected data source. Defaults to <see cref="DataSourceKind.Sample"/>.</summary>
    public DataSourceKind DataSource { get; set; } = DataSourceKind.Sample;

    /// <summary>Server name shown to clients.</summary>
    public string ServerName { get; set; } = "Valthera";

    /// <summary>Settings of the game server status protocol.</summary>
    public StatusOptions Status { get; set; } = new();

    /// <summary>Settings of the spell catalog.</summary>
    public SpellOptions Spells { get; set; } = new();

    /// <summary>Settings of the per-client rate limit.</summary>
    public RateLimitOptions RateLimit { get; set; } = new();
}

/// <summary>Game server status endpoint settings.</summary>
public sealed class StatusOptions
{
    /// <summary>Host of the game server.</summary>
    public string Host { get; set; } = "127.0.0.1";

    /// <summary>Game server port.</summary>
    public int Port { get; set; } = 7171;

    /// <summary>Timeout of a status query in milliseconds.</summary>
    public int TimeoutMs { get; set; } = 3000;
}

/// <summary>Spell catalog settings.</summary>
public sealed class SpellOptions
{
    /// <summary>Folder with <c>*.valspell.json</c> files. Empty means: use the embedded sample spells.</summary>
    public string? Folder { get; set; }
}

/// <summary>Rate limit settings.</summary>
public sealed class RateLimitOptions
{
    /// <summary>Requests allowed per client address per minute on <c>/api</c> routes.</summary>
    public int PermitPerMinute { get; set; } = 60;
}
