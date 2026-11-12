using System.Text;
using Valthera.Core.Models;

namespace Valthera.Core.Csv;

/// <summary>Builds CSV exports of highscore tables.</summary>
public static class HighscoreCsv
{
    /// <summary>CSV header line.</summary>
    public const string Header = "Rank,Name,Level,Vocation,Value";

    /// <summary>Builds a CSV document (CRLF line endings) from highscore entries.</summary>
    public static string Build(IEnumerable<HighscoreEntry> entries)
    {
        var sb = new StringBuilder(Header).Append("\r\n");
        foreach (var e in entries)
        {
            sb.Append(e.Rank).Append(',')
              .Append(Escape(e.Name)).Append(',')
              .Append(e.Level).Append(',')
              .Append(Escape(e.Vocation)).Append(',')
              .Append(e.Value).Append("\r\n");
        }

        return sb.ToString();
    }

    /// <summary>Quotes a field when it contains a comma, quote or line break, doubling embedded quotes.</summary>
    public static string Escape(string value)
    {
        if (value.AsSpan().IndexOfAny(",\"\r\n") < 0)
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
