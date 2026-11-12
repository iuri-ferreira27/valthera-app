using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using Valthera.Core.Models;

namespace Valthera.Core.Status;

/// <summary>Parses the XML (<c>&lt;tsqp&gt;</c>) answer of the Open Tibia status protocol.</summary>
public static class OtStatusParser
{
    /// <summary>
    /// Parses a status answer. Any malformed or unexpected document yields an offline status instead of an exception.
    /// </summary>
    /// <param name="xml">Raw XML text received from the server.</param>
    /// <param name="serverName">Name to report (the configured name wins over the one in the XML).</param>
    /// <param name="checkedAt">Timestamp to stamp on the result.</param>
    public static ServerStatus Parse(string? xml, string serverName, DateTimeOffset checkedAt)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return Offline(serverName, checkedAt);
        }

        try
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
            using var reader = XmlReader.Create(new StringReader(xml.Trim()), settings);
            var root = XDocument.Load(reader).Root;
            if (root is null || !string.Equals(root.Name.LocalName, "tsqp", StringComparison.OrdinalIgnoreCase))
            {
                return Offline(serverName, checkedAt);
            }

            var info = root.Element("serverinfo");
            var players = root.Element("players");
            if (players is null)
            {
                return Offline(serverName, checkedAt);
            }

            TimeSpan? uptime = TryLong(info?.Attribute("uptime")?.Value) is { } seconds && seconds >= 0
                ? TimeSpan.FromSeconds(seconds)
                : null;

            return new ServerStatus(
                serverName,
                Online: true,
                PlayersOnline: (int)(TryLong(players.Attribute("online")?.Value) ?? 0),
                PlayersMax: (int)(TryLong(players.Attribute("max")?.Value) ?? 0),
                PlayersPeak: (int)(TryLong(players.Attribute("peak")?.Value) ?? 0),
                uptime,
                info?.Attribute("version")?.Value,
                checkedAt);
        }
        catch (XmlException)
        {
            return Offline(serverName, checkedAt);
        }
    }

    /// <summary>Creates the status reported when the server cannot be reached or answers garbage.</summary>
    public static ServerStatus Offline(string serverName, DateTimeOffset checkedAt) =>
        new(serverName, false, 0, 0, 0, null, null, checkedAt);

    private static long? TryLong(string? text) =>
        long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
}
