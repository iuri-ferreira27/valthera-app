using System.Globalization;

namespace Valthera.Components.Services;

/// <summary>Display helpers: Brazilian Portuguese labels and number/date formatting.</summary>
public static class Formatting
{
    private static readonly CultureInfo Culture = new("pt-BR");

    /// <summary>Category keys with their Portuguese labels, in display order.</summary>
    public static IReadOnlyList<(string Key, string Label)> Categories { get; } =
    [
        ("level", "Nível"),
        ("magiclevel", "Nível mágico"),
        ("fist", "Combate sem arma (Fist)"),
        ("club", "Clava (Club)"),
        ("sword", "Espada (Sword)"),
        ("axe", "Machado (Axe)"),
        ("distance", "Distância"),
        ("shielding", "Escudo"),
        ("fishing", "Pesca"),
    ];

    /// <summary>Gets the Portuguese label of a category key (falls back to the key).</summary>
    public static string CategoryLabel(string key) =>
        Categories.FirstOrDefault(c => c.Key == key).Label ?? key;

    /// <summary>Formats an integer with thousands separators.</summary>
    public static string Number(long value) => value.ToString("N0", Culture);

    /// <summary>Formats a date and time in the viewer's local time zone.</summary>
    public static string DateTime(DateTimeOffset value) =>
        value.ToLocalTime().ToString("dd/MM/yyyy HH:mm", Culture);

    /// <summary>Formats a duration compactly, for example <c>2d 4h 10min</c>.</summary>
    public static string Duration(TimeSpan value)
    {
        if (value.TotalMinutes < 1)
        {
            return "menos de 1 min";
        }

        var parts = new List<string>();
        if (value.Days > 0)
        {
            parts.Add($"{value.Days}d");
        }

        if (value.Hours > 0)
        {
            parts.Add($"{value.Hours}h");
        }

        if (value.Minutes > 0)
        {
            parts.Add($"{value.Minutes}min");
        }

        return string.Join(' ', parts);
    }

    /// <summary>Builds the character page link for a name.</summary>
    public static string PlayerLink(string name) => "personagem?nome=" + Uri.EscapeDataString(name);
}
