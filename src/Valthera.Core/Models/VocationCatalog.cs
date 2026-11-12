namespace Valthera.Core.Models;

/// <summary>Maps vocation ids used by the game database to display names.</summary>
public static class VocationCatalog
{
    private static readonly IReadOnlyDictionary<int, string> Names = new Dictionary<int, string>
    {
        [0] = "None", [1] = "Sorcerer", [2] = "Druid", [3] = "Paladin", [4] = "Knight",
        [5] = "Master Sorcerer", [6] = "Elder Druid", [7] = "Royal Paladin", [8] = "Elite Knight",
        [100] = "Archer", [101] = "Flurry", [102] = "Longshot", [103] = "Bard", [104] = "Cleric",
        [105] = "Spellsinger", [106] = "Berserker", [107] = "Assault", [108] = "Rage", [109] = "Corsair",
        [110] = "Demolitionist", [111] = "Cardmaster", [112] = "Death Knight", [113] = "Blood Knight",
        [114] = "Dark Knight", [115] = "Archlight Druid", [116] = "Tree Spirit", [117] = "Shaman",
        [118] = "Guardian", [119] = "Holy", [120] = "Protector", [121] = "Gunslinger", [122] = "Outlaw",
        [123] = "Trickster", [124] = "Monk", [125] = "Spiritmaster", [126] = "Brawler", [127] = "Necromancer",
        [128] = "Conjurer", [129] = "Witch", [130] = "Rogue", [131] = "Assassination", [132] = "Shadow",
        [133] = "Samurai", [134] = "Ronin", [135] = "Shogun", [136] = "Archlight Sorcerer",
        [137] = "Pyromancer", [138] = "Thunderlord", [139] = "Tamer", [140] = "Beastmaster",
        [141] = "Tracker", [142] = "Hexblade", [143] = "Doomcaller", [144] = "Bladebound",
        [145] = "Astromancer", [146] = "Starweaver", [147] = "Eclipse", [148] = "Priest",
        [149] = "Hierophant", [150] = "Inquisitor", [151] = "Aprendiz", [152] = "Mago", [153] = "Suporte",
        [154] = "Ocultista", [155] = "Guerreiro", [156] = "Defensor", [157] = "Caçador",
        [158] = "Pistoleiro", [159] = "Lâmina",
    };

    /// <summary>Gets all known vocations keyed by id.</summary>
    public static IReadOnlyDictionary<int, string> All => Names;

    /// <summary>Gets the display name of a vocation id, or <c>Unknown (id)</c> when it is not known.</summary>
    public static string GetName(int id) =>
        Names.TryGetValue(id, out var name) ? name : $"Unknown ({id})";
}
