using System.Text;
using Microsoft.AspNetCore.Mvc;
using Valthera.Core.Contracts;
using Valthera.Core.Csv;
using Valthera.Core.Models;

namespace Valthera.Api.Endpoints;

/// <summary>A vocation id with its display name.</summary>
public sealed record VocationDto(int Id, string Name);

/// <summary>Maps the public REST endpoints under <c>/api</c>.</summary>
public static class ApiEndpoints
{
    /// <summary>Output cache policy names.</summary>
    public static class CachePolicies
    {
        public const string Status = "status";
        public const string Highscores = "highscores";
        public const string Online = "online";
        public const string Deaths = "deaths";
        public const string Players = "players";
        public const string Spells = "spells";
    }

    /// <summary>Name of the rate limiting policy applied to <c>/api</c>.</summary>
    public const string RateLimitPolicy = "api";

    private const int DefaultPageSize = 25;
    private const int MaxPageSize = 100;
    private const int CsvMaxRows = 1000;
    private const int MaxNameLength = 64;

    /// <summary>Maps all API routes.</summary>
    public static IEndpointRouteBuilder MapValtheraApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api").RequireRateLimiting(RateLimitPolicy).RequireCors("get-only");

        api.MapGet("/status", async (IServerStatusProvider status, CancellationToken ct) =>
                Results.Ok(await status.GetStatusAsync(ct)))
            .CacheOutput(CachePolicies.Status)
            .WithName("GetStatus");

        api.MapGet("/highscores", GetHighscores)
            .CacheOutput(CachePolicies.Highscores)
            .WithName("GetHighscores");

        api.MapGet("/highscores/export.csv", ExportHighscoresCsv)
            .CacheOutput(CachePolicies.Highscores)
            .WithName("ExportHighscoresCsv");

        api.MapGet("/online", async (IServerDataSource data, CancellationToken ct) =>
                Results.Ok(await data.GetOnlinePlayersAsync(ct)))
            .CacheOutput(CachePolicies.Online)
            .WithName("GetOnlinePlayers");

        api.MapGet("/deaths", async (IServerDataSource data, int? limit, CancellationToken ct) =>
                Results.Ok(await data.GetRecentDeathsAsync(Math.Clamp(limit ?? 20, 1, MaxPageSize), ct)))
            .CacheOutput(CachePolicies.Deaths)
            .WithName("GetRecentDeaths");

        api.MapGet("/players/{name}", GetPlayer)
            .CacheOutput(CachePolicies.Players)
            .WithName("GetPlayer");

        api.MapGet("/spells/classes", (ISpellCatalog spells) => Results.Ok(spells.GetClasses()))
            .CacheOutput(CachePolicies.Spells)
            .WithName("GetSpellClasses");

        api.MapGet("/spells", ([FromQuery(Name = "class")] string? spellClass, ISpellCatalog spells) =>
                Results.Ok(spells.GetSpells(spellClass)))
            .CacheOutput(CachePolicies.Spells)
            .WithName("GetSpells");

        api.MapGet("/spells/{spellClass}/{name}", (string spellClass, string name, ISpellCatalog spells) =>
                spells.GetSpell(spellClass, name) is { } spell
                    ? Results.Ok(spell)
                    : Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Spell not found"))
            .CacheOutput(CachePolicies.Spells)
            .WithName("GetSpell");

        api.MapGet("/vocations", () =>
                Results.Ok(VocationCatalog.All.Select(v => new VocationDto(v.Key, v.Value)).OrderBy(v => v.Id)))
            .CacheOutput(CachePolicies.Spells)
            .WithName("GetVocations");

        return app;
    }

    private static async Task<IResult> GetHighscores(
        IServerDataSource data, string? category, string? vocation, int? page, int? pageSize, CancellationToken ct)
    {
        if (!TryReadFilters(category, vocation, out var parsedCategory, out var vocationId, out var error))
        {
            return error!;
        }

        var result = await data.GetHighscoresAsync(
            parsedCategory,
            vocationId,
            Math.Max(1, page ?? 1),
            Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize),
            ct);
        return Results.Ok(result);
    }

    private static async Task<IResult> ExportHighscoresCsv(
        IServerDataSource data, string? category, string? vocation, CancellationToken ct)
    {
        if (!TryReadFilters(category, vocation, out var parsedCategory, out var vocationId, out var error))
        {
            return error!;
        }

        var entries = new List<HighscoreEntry>();
        for (var page = 1; entries.Count < CsvMaxRows; page++)
        {
            var result = await data.GetHighscoresAsync(parsedCategory, vocationId, page, MaxPageSize, ct);
            entries.AddRange(result.Entries);
            if (result.Entries.Count < MaxPageSize || entries.Count >= result.Total)
            {
                break;
            }
        }

        var csv = HighscoreCsv.Build(entries.Take(CsvMaxRows));
        return Results.File(
            Encoding.UTF8.GetBytes(csv),
            "text/csv",
            $"valthera-highscores-{HighscoreCategories.GetKey(parsedCategory)}.csv");
    }

    private static async Task<IResult> GetPlayer(string name, IServerDataSource data, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > MaxNameLength)
        {
            return Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid player name");
        }

        var profile = await data.GetPlayerAsync(name, ct);
        return profile is null
            ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Player not found")
            : Results.Ok(profile);
    }

    private static bool TryReadFilters(
        string? category, string? vocation, out HighscoreCategory parsedCategory, out int? vocationId, out IResult? error)
    {
        vocationId = null;
        error = null;

        if (!HighscoreCategories.TryParse(string.IsNullOrWhiteSpace(category) ? "level" : category, out parsedCategory))
        {
            var valid = string.Join(", ", HighscoreCategories.All.Select(HighscoreCategories.GetKey));
            error = Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid highscore category",
                detail: $"Valid categories: {valid}.");
            return false;
        }

        if (!string.IsNullOrWhiteSpace(vocation))
        {
            if (!int.TryParse(vocation, out var id) || id < 0)
            {
                error = Results.Problem(
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Invalid vocation",
                    detail: "The vocation filter must be a non-negative vocation id.");
                return false;
            }

            vocationId = id;
        }

        return true;
    }
}
