using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging;
using Valthera.Core.Contracts;
using Valthera.Core.Models;

namespace Valthera.Core.Status;

/// <summary>Connection settings for <see cref="OtStatusClient"/>.</summary>
public sealed class OtStatusOptions
{
    /// <summary>Name reported in <see cref="ServerStatus.Name"/>.</summary>
    public string ServerName { get; set; } = "Valthera";

    /// <summary>Host of the game server status port.</summary>
    public string Host { get; set; } = "127.0.0.1";

    /// <summary>TCP port of the game server (the login/status port).</summary>
    public int Port { get; set; } = 7171;

    /// <summary>Overall timeout (connect + request + read) in milliseconds.</summary>
    public int TimeoutMs { get; set; } = 3000;
}

/// <summary>
/// Queries a game server through the Open Tibia status protocol over TCP.
/// </summary>
public sealed class OtStatusClient : IServerStatusProvider
{
    /// <summary>
    /// Request packet: 2-byte little-endian length (6), 0xFF 0xFF (status request marker) and the ASCII text "info".
    /// </summary>
    public static readonly byte[] RequestPacket = [0x06, 0x00, 0xFF, 0xFF, 0x69, 0x6E, 0x66, 0x6F];

    private const int MaxResponseBytes = 64 * 1024;

    private readonly OtStatusOptions _options;
    private readonly ILogger<OtStatusClient> _logger;
    private readonly TimeProvider _time;

    /// <summary>Creates a client.</summary>
    public OtStatusClient(OtStatusOptions options, ILogger<OtStatusClient> logger, TimeProvider? time = null)
    {
        _options = options;
        _logger = logger;
        _time = time ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<ServerStatus> GetStatusAsync(CancellationToken ct = default)
    {
        var now = _time.GetUtcNow();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(_options.TimeoutMs);

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(_options.Host, _options.Port, timeout.Token).ConfigureAwait(false);
            var stream = client.GetStream();
            await stream.WriteAsync(RequestPacket, timeout.Token).ConfigureAwait(false);

            using var buffer = new MemoryStream();
            var chunk = new byte[4096];
            int read;
            while ((read = await stream.ReadAsync(chunk, timeout.Token).ConfigureAwait(false)) > 0)
            {
                buffer.Write(chunk, 0, read);
                if (buffer.Length > MaxResponseBytes)
                {
                    _logger.LogWarning("Status response from the game server exceeded {Max} bytes; treating it as offline.", MaxResponseBytes);
                    return OtStatusParser.Offline(_options.ServerName, now);
                }
            }

            var xml = Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
            return OtStatusParser.Parse(xml, _options.ServerName, now);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Status request to the game server timed out after {Timeout} ms.", _options.TimeoutMs);
            return OtStatusParser.Offline(_options.ServerName, now);
        }
        catch (Exception ex) when (ex is SocketException or IOException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Status request to the game server failed.");
            return OtStatusParser.Offline(_options.ServerName, now);
        }
    }
}
