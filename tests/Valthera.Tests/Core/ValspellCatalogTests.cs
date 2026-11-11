using Valthera.Core.Sample;
using Valthera.Core.Spells;

namespace Valthera.Tests.Core;

public sealed class ValspellCatalogTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "valthera-tests-" + Guid.NewGuid().ToString("N"));

    public ValspellCatalogTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private static string Doc(string title, params string[] names)
    {
        var spells = string.Join(",", names.Select(n =>
            "{ \"name\": \"" + n + "\", \"group\": \"Attack\", \"idea\": \"Idea of " + n + ".\"," +
            " \"extra_field\": { \"ignored\": true }," +
            " \"reference\": { \"level\": 150, \"cd\": \"4 s\", \"mana\": \"100\", \"damage\": \"High\", \"range\": \"5\", \"area\": \"Single\", \"fx\": \"effect 1\", \"role\": \"ignored\" }," +
            " \"variants\": [ { \"name\": \"Base\", \"note\": \"Base note\", \"stages\": [] } ] }"));
        return "{ \"schema_version\": 2, \"title\": \"" + title + "\", \"spells\": [ " + spells + " ] }";
    }

    private void Write(string relativePath, string content)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    [Fact]
    public void FromFolder_UsesTheTitleTextBeforeTheEmDashAsClass()
    {
        Write("a.valspell.json", Doc("Thunderlord — Chain Lightning (approved)", "Chain Lightning", "Storm Step"));

        var catalog = ValspellCatalog.FromFolder(_root);

        var classes = catalog.GetClasses();
        Assert.Equal("Thunderlord", Assert.Single(classes).Name);
        Assert.Equal(2, classes[0].SpellCount);
        Assert.Equal(["Chain Lightning", "Storm Step"], catalog.GetSpells("thunderlord").Select(s => s.Name));
    }

    [Fact]
    public void FromFolder_ReadsReferenceVariantsAndIgnoresExtraFields()
    {
        Write("a.valspell.json", Doc("Mage — Bolt", "Bolt"));

        var spell = ValspellCatalog.FromFolder(_root).GetSpell("mage", "BOLT");

        Assert.NotNull(spell);
        Assert.Equal("Attack", spell.Group);
        Assert.Equal("Idea of Bolt.", spell.Idea);
        Assert.Equal("150", spell.Reference.Level);
        Assert.Equal("4 s", spell.Reference.Cooldown);
        Assert.Equal("effect 1", spell.Reference.Effects);
        var variant = Assert.Single(spell.Variants);
        Assert.Equal(("Base", "Base note"), (variant.Name, variant.Note));
    }

    [Fact]
    public void FromFolder_FallsBackToTheSubfolderNameThenToGeral()
    {
        Write("Rangers/no-dash.valspell.json", Doc("Just a title", "Arrow Rain"));
        Write("root-level.valspell.json", Doc("Another title without separator", "Quick Shot"));

        var catalog = ValspellCatalog.FromFolder(_root);

        Assert.Equal(["Geral", "Rangers"], catalog.GetClasses().Select(c => c.Name));
        Assert.NotNull(catalog.GetSpell("Rangers", "Arrow Rain"));
        Assert.NotNull(catalog.GetSpell(ValspellCatalog.DefaultClass, "Quick Shot"));
    }

    [Fact]
    public void FromFolder_AcceptsASpacedHyphenAsSeparatorButNotABareHyphen()
    {
        Write("a.valspell.json", Doc("Soul-Rune - Hex", "Hex"));
        Write("b.valspell.json", Doc("Soul-Rune", "Plain"));

        var catalog = ValspellCatalog.FromFolder(_root);

        Assert.NotNull(catalog.GetSpell("Soul-Rune", "Hex"));
        Assert.NotNull(catalog.GetSpell("Geral", "Plain"));
    }

    [Fact]
    public void FromFolder_TreatsALeftPartEqualToTheSpellNameAsNoClass()
    {
        Write("Mages/a.valspell.json", Doc("Fire Bolt — Fire Bolt (approved)", "Fire Bolt"));
        Write("b.valspell.json", Doc("Ice Wall - Ice Wall", "Ice Wall"));

        var catalog = ValspellCatalog.FromFolder(_root);

        Assert.NotNull(catalog.GetSpell("Mages", "Fire Bolt"));
        Assert.NotNull(catalog.GetSpell("Geral", "Ice Wall"));
    }

    [Fact]
    public void FromFolder_SearchesRecursively()
    {
        Write("x/y/z/deep.valspell.json", Doc("Deep — Spell", "Deep Spell"));

        Assert.Single(ValspellCatalog.FromFolder(_root).GetClasses());
    }

    [Fact]
    public void FromFolder_SkipsInvalidFilesWithoutFailing()
    {
        Write("good.valspell.json", Doc("Good — Spell", "Spell"));
        Write("broken.valspell.json", "{ this is not json");
        Write("wrong-shape.valspell.json", "{ \"title\": \"Nope\" }");
        Write("ignored.json", Doc("Other — Spell", "Spell"));

        var catalog = ValspellCatalog.FromFolder(_root);

        Assert.Equal(1, catalog.LoadedFiles);
        Assert.Equal(2, catalog.SkippedFiles);
        Assert.Equal("Good", Assert.Single(catalog.GetClasses()).Name);
    }

    [Fact]
    public void FromFolder_ReturnsAnEmptyCatalogWhenTheFolderIsMissing()
    {
        var catalog = ValspellCatalog.FromFolder(Path.Combine(_root, "missing"));

        Assert.Empty(catalog.GetClasses());
    }

    [Fact]
    public void GetSpell_ReturnsNullForUnknownSpells()
    {
        Write("a.valspell.json", Doc("Mage — Bolt", "Bolt"));
        var catalog = ValspellCatalog.FromFolder(_root);

        Assert.Null(catalog.GetSpell("mage", "nothing"));
        Assert.Null(catalog.GetSpell("nobody", "bolt"));
    }

    [Fact]
    public void SampleSpells_LoadsTheThreeEmbeddedDocuments()
    {
        var catalog = SampleSpells.CreateCatalog();

        Assert.Equal(0, catalog.SkippedFiles);
        Assert.Equal(["Emberwarden", "Stormcaller", "Tidebinder"], catalog.GetClasses().Select(c => c.Name));
        Assert.Equal(3, catalog.GetSpells().Count);
    }
}
