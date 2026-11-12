namespace Valthera.Core.Models;

/// <summary>A spell class (for example a vocation) and how many spells it contains.</summary>
public sealed record SpellClass(string Name, int SpellCount);

/// <summary>Short description of a spell, used in lists.</summary>
public sealed record SpellSummary(string Class, string Name, string Group, string? Level);

/// <summary>Numeric and descriptive reference data of a spell.</summary>
public sealed record SpellReference(
    string? Level,
    string? Cooldown,
    string? Mana,
    string? Damage,
    string? Range,
    string? Area,
    string? Effects);

/// <summary>A named variant of a spell with a short note.</summary>
public sealed record SpellVariant(string Name, string? Note);

/// <summary>Full description of a spell.</summary>
public sealed record SpellDetail(
    string Class,
    string Name,
    string Group,
    string? Idea,
    SpellReference Reference,
    IReadOnlyList<SpellVariant> Variants);
