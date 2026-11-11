using Valthera.Core.Contracts;
using Valthera.Core.Models;

namespace Valthera.Core.Sample;

/// <summary>
/// Deterministic in-memory data source with fictional characters, used for demos, tests and Docker.
/// All values are derived from fixed tables: no randomness and no dependency on the current time.
/// </summary>
public sealed class SampleServerDataSource : IServerDataSource
{
    /// <summary>Largest page size accepted by data sources.</summary>
    public const int MaxPageSize = 100;

    /// <summary>Fixed reference point for the sample deaths and logins.</summary>
    public static readonly DateTimeOffset ReferenceTime = new(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);

    private static readonly string[] Names =
    [
        "Aldric Stormborn", "Brenna Ashveil", "Caldor Ironwick", "Dyrena Frostmere", "Eldrin Vossk",
        "Fenna Duskwhisper", "Gorrim Blackthorn", "Halvar Emberfall", "Isolde Greymantle", "Jorund Stonehelm",
        "Kaelis Moonshade", "Lysandra Veyl", "Marek Thornwood", "Nyssa Brightwater", "Orlan Hollowpeak",
        "Perrin Wildmark", "Quillon Ashford", "Rhosyn Dawnbringer", "Soren Galewing", "Tamsin Rookhaven",
        "Ulric Wintersong", "Vaela Nightbloom", "Wyndham Coldrake", "Xanthe Silverbrook", "Ysmera Stoneveil",
        "Zephyrin Oakhart", "Alwyn Redmoor", "Bryndis Longfell", "Corvane Ashdown", "Delphine Starfall",
        "Evander Mistwalker", "Fiora Embershade", "Garrick Tidewater", "Hesper Lowmoor", "Ilyra Sunspire",
        "Jareth Coldwell", "Kestrin Ravenholt", "Lorcan Fairwind", "Mirelle Thistledown", "Nolwenn Hartfield",
    ];

    // Vocation ids taken from VocationCatalog, cycled in a fixed order.
    private static readonly int[] VocationIds =
    [
        8, 5, 7, 6, 100, 138, 4, 1, 3, 2, 112, 115, 124, 137, 141, 133, 148, 118, 121, 145,
    ];

    private static readonly string[] Monsters =
    [
        "a dragon lord", "a demon", "a hydra", "a behemoth", "a giant spider", "a juggernaut", "a warlock", "a frost dragon",
    ];

    private static readonly SampleCharacter[] Characters = BuildCharacters();
    private static readonly DeathEntry[] Deaths = BuildDeaths();

    /// <inheritdoc />
    public Task<HighscorePage> GetHighscoresAsync(
        HighscoreCategory category, int? vocationId, int page, int pageSize, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var ranked = Characters
            .Where(c => vocationId is null || c.VocationId == vocationId)
            .OrderByDescending(c => ValueOf(c, category))
            .ThenByDescending(c => c.Level)
            .ThenBy(c => c.Name, StringComparer.Ordinal)
            .ToList();

        var offset = (page - 1) * pageSize;
        var entries = ranked
            .Skip(offset)
            .Take(pageSize)
            .Select((c, i) => new HighscoreEntry(offset + i + 1, c.Name, c.Level, VocationCatalog.GetName(c.VocationId), ValueOf(c, category)))
            .ToList();

        return Task.FromResult(new HighscorePage(HighscoreCategories.GetKey(category), page, pageSize, ranked.Count, entries));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<OnlinePlayer>> GetOnlinePlayersAsync(CancellationToken ct = default)
    {
        IReadOnlyList<OnlinePlayer> online = Characters
            .Where(c => c.Online)
            .OrderByDescending(c => c.Level)
            .ThenBy(c => c.Name, StringComparer.Ordinal)
            .Select(c => new OnlinePlayer(c.Name, c.Level, VocationCatalog.GetName(c.VocationId)))
            .ToList();
        return Task.FromResult(online);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DeathEntry>> GetRecentDeathsAsync(int limit, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, MaxPageSize);
        IReadOnlyList<DeathEntry> deaths = Deaths.Take(limit).ToList();
        return Task.FromResult(deaths);
    }

    /// <inheritdoc />
    public Task<PlayerProfile?> GetPlayerAsync(string name, CancellationToken ct = default)
    {
        var character = Characters.FirstOrDefault(c => string.Equals(c.Name, name?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (character is null)
        {
            return Task.FromResult<PlayerProfile?>(null);
        }

        var deaths = Deaths.Where(d => d.Player == character.Name).Take(10).ToList();
        var profile = new PlayerProfile(
            character.Name,
            character.Level,
            VocationCatalog.GetName(character.VocationId),
            character.MagicLevel,
            character.LastLogin,
            character.Online,
            deaths);
        return Task.FromResult<PlayerProfile?>(profile);
    }

    /// <inheritdoc />
    public Task<int> GetOnlineCountAsync(CancellationToken ct = default) =>
        Task.FromResult(Characters.Count(c => c.Online));

    /// <summary>Gets the names of all sample characters (useful for tests and demos).</summary>
    public static IReadOnlyList<string> CharacterNames => Names;

    private static long ValueOf(SampleCharacter c, HighscoreCategory category) => category switch
    {
        HighscoreCategory.Level => c.Experience,
        HighscoreCategory.MagicLevel => c.MagicLevel,
        HighscoreCategory.Fist => c.Fist,
        HighscoreCategory.Club => c.Club,
        HighscoreCategory.Sword => c.Sword,
        HighscoreCategory.Axe => c.Axe,
        HighscoreCategory.Distance => c.Distance,
        HighscoreCategory.Shielding => c.Shielding,
        HighscoreCategory.Fishing => c.Fishing,
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unsupported highscore category."),
    };

    /// <summary>Standard Tibia experience formula.</summary>
    private static long ExperienceForLevel(int level)
    {
        long l = level;
        return 50 * (l * l * l - 6 * l * l + 17 * l - 12) / 3;
    }

    private static SampleCharacter[] BuildCharacters()
    {
        var list = new SampleCharacter[Names.Length];
        for (var i = 0; i < Names.Length; i++)
        {
            var level = 480 - i * 11 + (i * 7 % 5);
            var vocation = VocationIds[i % VocationIds.Length];
            list[i] = new SampleCharacter(
                Names[i],
                level,
                vocation,
                ExperienceForLevel(level) + i * 1_000,
                MagicLevel: 20 + (i * 13 % 90),
                Fist: 10 + (i * 5 % 40),
                Club: 10 + (i * 7 % 70),
                Sword: 10 + (i * 11 % 95),
                Axe: 10 + (i * 17 % 90),
                Distance: 10 + (i * 19 % 100),
                Shielding: 10 + (i * 23 % 85),
                Fishing: 10 + (i * 29 % 60),
                LastLogin: ReferenceTime.AddHours(-(i * 5 + 1)),
                Online: i % 5 == 0);
        }

        return list;
    }

    private static DeathEntry[] BuildDeaths()
    {
        var deaths = new List<DeathEntry>();
        for (var i = 0; i < 25; i++)
        {
            var victim = Characters[(i * 7 + 3) % Characters.Length];
            var byPlayer = i % 6 == 5;
            var killer = byPlayer
                ? Characters[(i * 3 + 1) % Characters.Length].Name
                : Monsters[i % Monsters.Length];
            deaths.Add(new DeathEntry(victim.Name, victim.Level - i % 4, killer, byPlayer, ReferenceTime.AddMinutes(-(i * 47 + 5))));
        }

        return [.. deaths.OrderByDescending(d => d.Time)];
    }

    private sealed record SampleCharacter(
        string Name,
        int Level,
        int VocationId,
        long Experience,
        int MagicLevel,
        int Fist,
        int Club,
        int Sword,
        int Axe,
        int Distance,
        int Shielding,
        int Fishing,
        DateTimeOffset LastLogin,
        bool Online);
}
