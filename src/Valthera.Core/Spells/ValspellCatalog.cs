using System.Text.Json;
using Microsoft.Extensions.Logging;
using Valthera.Core.Contracts;
using Valthera.Core.Models;

namespace Valthera.Core.Spells;

/// <summary>
/// Spell catalog loaded from <c>*.valspell.json</c> documents (schema version 2).
/// </summary>
/// <remarks>
/// The spell class is the part of the document <c>title</c> before an em dash (" — ") or a spaced hyphen (" - ").
/// A bare hyphen is never a separator, so names such as "Soul-Rune" stay intact. When the title has no separator,
/// or the left part is just the spell's own name, the name of the containing sub-folder is used, or
/// <see cref="DefaultClass"/> for files in the root. Unknown fields are ignored and invalid files are skipped with a warning.
/// </remarks>
public sealed class ValspellCatalog : ISpellCatalog
{
    /// <summary>Class used when neither the title nor the folder gives one.</summary>
    public const string DefaultClass = "Geral";

    private static readonly string[] TitleSeparators = [" — ", " - "];

    private readonly ILogger? _logger;
    private readonly Dictionary<(string Class, string Name), SpellDetail> _spells = new();

    /// <summary>Creates an empty catalog. Use <see cref="FromFolder"/> or <see cref="AddJson"/> to fill it.</summary>
    public ValspellCatalog(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>Gets the number of files that were skipped because they could not be read or parsed.</summary>
    public int SkippedFiles { get; private set; }

    /// <summary>Gets the number of files that were loaded successfully.</summary>
    public int LoadedFiles { get; private set; }

    /// <summary>Loads every <c>*.valspell.json</c> file found under a folder (recursively).</summary>
    public static ValspellCatalog FromFolder(string folder, ILogger? logger = null)
    {
        var catalog = new ValspellCatalog(logger);
        if (!Directory.Exists(folder))
        {
            logger?.LogWarning("Spells folder '{Folder}' does not exist; the catalog is empty.", folder);
            return catalog;
        }

        var root = Path.GetFullPath(folder);
        foreach (var file in Directory.EnumerateFiles(root, "*.valspell.json", SearchOption.AllDirectories).Order(StringComparer.OrdinalIgnoreCase))
        {
            string json;
            try
            {
                json = File.ReadAllText(file);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger?.LogWarning(ex, "Could not read spell file '{File}'; skipping.", file);
                catalog.SkippedFiles++;
                continue;
            }

            var directory = Path.GetDirectoryName(file);
            var folderName = string.Equals(directory, root, StringComparison.OrdinalIgnoreCase) || directory is null
                ? null
                : Path.GetFileName(directory);

            catalog.AddJson(json, folderName, file);
        }

        return catalog;
    }

    /// <summary>Adds the spells of one document. Returns <c>false</c> (and counts a skipped file) when it is invalid.</summary>
    /// <param name="json">Document text.</param>
    /// <param name="fallbackClass">Class to use when the title has no class part; <c>null</c> means <see cref="DefaultClass"/>.</param>
    /// <param name="source">Name used in log messages.</param>
    public bool AddJson(string json, string? fallbackClass, string source)
    {
        try
        {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("spells", out var spells)
                || spells.ValueKind != JsonValueKind.Array)
            {
                throw new JsonException("Document has no 'spells' array.");
            }

            var title = Text(root, "title");
            var parsed = new List<SpellDetail>();
            foreach (var spell in spells.EnumerateArray())
            {
                if (spell.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var name = Text(spell, "name");
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                parsed.Add(ParseSpell(spell, ResolveClass(title, name, fallbackClass), name));
            }

            foreach (var detail in parsed)
            {
                _spells.TryAdd((detail.Class.ToLowerInvariant(), detail.Name.ToLowerInvariant()), detail);
            }

            LoadedFiles++;
            return true;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            _logger?.LogWarning("Skipping invalid spell file '{Source}': {Reason}", source, ex.Message);
            SkippedFiles++;
            return false;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<SpellClass> GetClasses() =>
        _spells.Values
            .GroupBy(s => s.Class, StringComparer.OrdinalIgnoreCase)
            .Select(g => new SpellClass(g.First().Class, g.Count()))
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <inheritdoc />
    public IReadOnlyList<SpellSummary> GetSpells(string? spellClass = null) =>
        _spells.Values
            .Where(s => string.IsNullOrWhiteSpace(spellClass) || string.Equals(s.Class, spellClass, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.Class, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .Select(s => new SpellSummary(s.Class, s.Name, s.Group, s.Reference.Level))
            .ToList();

    /// <inheritdoc />
    public SpellDetail? GetSpell(string spellClass, string name) =>
        _spells.GetValueOrDefault((spellClass.ToLowerInvariant(), name.ToLowerInvariant()));

    private static string ResolveClass(string? title, string spellName, string? fallbackClass)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            var index = TitleSeparators
                .Select(separator => title.IndexOf(separator, StringComparison.Ordinal))
                .Where(i => i > 0)
                .DefaultIfEmpty(-1)
                .Min();
            if (index > 0)
            {
                var fromTitle = title[..index].Trim();
                if (fromTitle.Length > 0 && !string.Equals(fromTitle, spellName.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return fromTitle;
                }
            }
        }

        return string.IsNullOrWhiteSpace(fallbackClass) ? DefaultClass : fallbackClass.Trim();
    }

    private static SpellDetail ParseSpell(JsonElement spell, string spellClass, string name)
    {
        var reference = spell.TryGetProperty("reference", out var r) && r.ValueKind == JsonValueKind.Object
            ? new SpellReference(
                Text(r, "level"), Text(r, "cd"), Text(r, "mana"), Text(r, "damage"),
                Text(r, "range"), Text(r, "area"), Text(r, "fx"))
            : new SpellReference(null, null, null, null, null, null, null);

        var variants = new List<SpellVariant>();
        if (spell.TryGetProperty("variants", out var v) && v.ValueKind == JsonValueKind.Array)
        {
            foreach (var variant in v.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.Object))
            {
                var variantName = Text(variant, "name");
                if (!string.IsNullOrWhiteSpace(variantName))
                {
                    variants.Add(new SpellVariant(variantName, Text(variant, "note")));
                }
            }
        }

        var group = Text(spell, "group");
        return new SpellDetail(
            spellClass,
            name.Trim(),
            string.IsNullOrWhiteSpace(group) ? "General" : group.Trim(),
            Text(spell, "idea"),
            reference,
            variants);
    }

    /// <summary>Reads a property as text; numbers and booleans are converted, everything else becomes <c>null</c>.</summary>
    private static string? Text(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.GetRawText(),
            _ => null,
        };
    }
}
