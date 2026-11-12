namespace Valthera.Core.Models;

/// <summary>Live status of the game server as reported by its status protocol.</summary>
/// <param name="Name">Server name.</param>
/// <param name="Online">Whether the server answered the status request.</param>
/// <param name="PlayersOnline">Players currently online.</param>
/// <param name="PlayersMax">Maximum players allowed.</param>
/// <param name="PlayersPeak">Highest concurrent player count since the last restart.</param>
/// <param name="Uptime">Time since the server started, when known.</param>
/// <param name="Version">Server software version, when known.</param>
/// <param name="CheckedAt">When the status was collected.</param>
public sealed record ServerStatus(
    string Name,
    bool Online,
    int PlayersOnline,
    int PlayersMax,
    int PlayersPeak,
    TimeSpan? Uptime,
    string? Version,
    DateTimeOffset CheckedAt);
