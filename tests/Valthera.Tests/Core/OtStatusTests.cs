using System.Net;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Valthera.Core.Status;

namespace Valthera.Tests.Core;

public class OtStatusTests
{
    private const string SampleXml =
        "<?xml version=\"1.0\"?><tsqp version=\"1.0\">" +
        "<serverinfo uptime=\"3725\" ip=\"127.0.0.1\" servername=\"Some Server\" port=\"7171\" location=\"BR\" url=\"\" server=\"TFS\" version=\"1.4\" client=\"10.98\"/>" +
        "<owner name=\"\" email=\"\"/>" +
        "<players online=\"8\" max=\"500\" peak=\"21\"/>" +
        "<monsters total=\"1234\"/>" +
        "<map name=\"world\" author=\"someone\" width=\"1000\" height=\"1000\"/>" +
        "<motd>Welcome</motd></tsqp>";

    private static readonly DateTimeOffset Now = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    [Fact]
    public void RequestPacket_HasTheExactBytes() =>
        Assert.Equal(new byte[] { 0x06, 0x00, 0xFF, 0xFF, 0x69, 0x6E, 0x66, 0x6F }, OtStatusClient.RequestPacket);

    [Fact]
    public void Parse_ReadsAValidAnswer()
    {
        var status = OtStatusParser.Parse(SampleXml, "Valthera", Now);

        Assert.True(status.Online);
        Assert.Equal("Valthera", status.Name);
        Assert.Equal(8, status.PlayersOnline);
        Assert.Equal(500, status.PlayersMax);
        Assert.Equal(21, status.PlayersPeak);
        Assert.Equal(TimeSpan.FromSeconds(3725), status.Uptime);
        Assert.Equal("1.4", status.Version);
        Assert.Equal(Now, status.CheckedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not xml at all")]
    [InlineData("<tsqp><players online=\"1\"")]
    [InlineData("<html><body>hello</body></html>")]
    [InlineData("<tsqp><serverinfo uptime=\"1\"/></tsqp>")]
    public void Parse_ReturnsOfflineForInvalidInput(string? xml)
    {
        var status = OtStatusParser.Parse(xml, "Valthera", Now);

        Assert.False(status.Online);
        Assert.Equal("Valthera", status.Name);
        Assert.Equal(0, status.PlayersOnline);
        Assert.Null(status.Uptime);
    }

    [Fact]
    public void Parse_RejectsDtds()
    {
        const string xxe = "<?xml version=\"1.0\"?><!DOCTYPE x [<!ENTITY e \"boom\">]><tsqp><players online=\"1\" max=\"2\" peak=\"3\"/></tsqp>";
        Assert.False(OtStatusParser.Parse(xxe, "Valthera", Now).Online);
    }

    [Fact]
    public async Task Client_SendsTheRequestPacketAndParsesTheAnswer()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        byte[]? received = null;
        var server = Task.Run(async () =>
        {
            using var peer = await listener.AcceptTcpClientAsync();
            var stream = peer.GetStream();
            var buffer = new byte[OtStatusClient.RequestPacket.Length];
            var read = 0;
            while (read < buffer.Length)
            {
                var n = await stream.ReadAsync(buffer.AsMemory(read));
                if (n == 0)
                {
                    break;
                }

                read += n;
            }

            received = buffer;
            await stream.WriteAsync(Encoding.UTF8.GetBytes(SampleXml));
        });

        var client = new OtStatusClient(
            new OtStatusOptions { Host = "127.0.0.1", Port = port, TimeoutMs = 2000 },
            NullLogger<OtStatusClient>.Instance);

        var status = await client.GetStatusAsync();
        await server;

        Assert.Equal(OtStatusClient.RequestPacket, received);
        Assert.True(status.Online);
        Assert.Equal(8, status.PlayersOnline);
    }

    [Fact]
    public async Task Client_ReportsOfflineWhenNothingListens()
    {
        int port;
        using (var probe = new TcpListener(IPAddress.Loopback, 0))
        {
            probe.Start();
            port = ((IPEndPoint)probe.LocalEndpoint).Port;
        }

        var client = new OtStatusClient(
            new OtStatusOptions { Host = "127.0.0.1", Port = port, TimeoutMs = 1000 },
            NullLogger<OtStatusClient>.Instance);

        Assert.False((await client.GetStatusAsync()).Online);
    }

    [Fact]
    public async Task Client_ReportsOfflineOnTimeout()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;

        var accepted = listener.AcceptTcpClientAsync(); // accepts but never answers
        var client = new OtStatusClient(
            new OtStatusOptions { Host = "127.0.0.1", Port = port, TimeoutMs = 300 },
            NullLogger<OtStatusClient>.Instance);

        var status = await client.GetStatusAsync();

        Assert.False(status.Online);
        (await accepted).Dispose();
    }
}
