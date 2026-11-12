using Valthera.Core.Spells;

namespace Valthera.Core.Sample;

/// <summary>Provides the fictional spells embedded in this assembly, in the <c>.valspell.json</c> format.</summary>
public static class SampleSpells
{
    private const string ResourceSuffix = ".valspell.json";

    /// <summary>Builds a catalog from the embedded sample documents.</summary>
    public static ValspellCatalog CreateCatalog()
    {
        var assembly = typeof(SampleSpells).Assembly;
        var catalog = new ValspellCatalog();
        foreach (var resource in assembly.GetManifestResourceNames()
                     .Where(n => n.EndsWith(ResourceSuffix, StringComparison.OrdinalIgnoreCase))
                     .Order(StringComparer.Ordinal))
        {
            using var stream = assembly.GetManifestResourceStream(resource)!;
            using var reader = new StreamReader(stream);
            catalog.AddJson(reader.ReadToEnd(), null, resource);
        }

        return catalog;
    }
}
