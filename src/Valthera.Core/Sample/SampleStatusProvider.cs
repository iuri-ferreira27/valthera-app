using Valthera.Core.Contracts;
using Valthera.Core.Models;

namespace Valthera.Core.Sample;

/// <summary>Status provider that always reports a healthy fictional server. Used in Sample mode.</summary>
public sealed class SampleStatusProvider : IServerStatusProvider
{
    private readonly string _serverName;
    private readonly IServerDataSource _data;
    private readonly TimeProvider _time;

    /// <summary>Creates the provider.</summary>
    public SampleStatusProvider(string serverName, IServerDataSource data, TimeProvider? time = null)
    {
        _serverName = serverName;
        _data = data;
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<ServerStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var online = await _data.GetOnlineCountAsync(ct).ConfigureAwait(false);
        return new ServerStatus(
            _serverName,
            Online: true,
            PlayersOnline: online,
            PlayersMax: 500,
            PlayersPeak: online + 12,
            Uptime: TimeSpan.FromHours(51) + TimeSpan.FromMinutes(24),
            Version: "sample",
            CheckedAt: _time.GetUtcNow());
    }
}
