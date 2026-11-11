using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Valthera.Core.Models;

namespace Valthera.Tests.Api;

public class ApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _client;

    public ApiTests(WebApplicationFactory<Program> factory) =>
        _client = factory.WithWebHostBuilder(b => b.UseSetting("Valthera:RateLimit:PermitPerMinute", "10000")).CreateClient();

    [Fact]
    public async Task Health_ReturnsOk()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Status_ReturnsTheSampleServer()
    {
        var status = await _client.GetFromJsonAsync<ServerStatus>("/api/status", Json);

        Assert.NotNull(status);
        Assert.True(status.Online);
        Assert.Equal("Valthera", status.Name);
        Assert.Equal(8, status.PlayersOnline);
    }

    [Fact]
    public async Task Highscores_ReturnsAPage()
    {
        var page = await _client.GetFromJsonAsync<HighscorePage>("/api/highscores?category=level&pageSize=5", Json);

        Assert.NotNull(page);
        Assert.Equal(5, page.Entries.Count);
        Assert.Equal(1, page.Entries[0].Rank);
        Assert.Equal(40, page.Total);
    }

    [Fact]
    public async Task Highscores_DefaultsToTheLevelCategory()
    {
        var page = await _client.GetFromJsonAsync<HighscorePage>("/api/highscores", Json);

        Assert.Equal("level", page!.Category);
        Assert.Equal(25, page.PageSize);
    }

    [Fact]
    public async Task Highscores_AcceptsAnEmptyVocationFilter()
    {
        var page = await _client.GetFromJsonAsync<HighscorePage>("/api/highscores?category=axe&vocation=", Json);

        Assert.Equal(40, page!.Total);
    }

    [Theory]
    [InlineData("/api/highscores?category=bogus")]
    [InlineData("/api/highscores?category=level%3B+DROP+TABLE+players")]
    [InlineData("/api/highscores?category=level&vocation=abc")]
    [InlineData("/api/highscores/export.csv?category=bogus")]
    public async Task Highscores_RejectsInvalidInputWithProblemDetails(string url)
    {
        var response = await _client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Highscores_ClampsPageSizeToOneHundred()
    {
        var page = await _client.GetFromJsonAsync<HighscorePage>("/api/highscores?pageSize=5000", Json);

        Assert.Equal(100, page!.PageSize);
    }

    [Fact]
    public async Task Player_ReturnsTheProfile()
    {
        var profile = await _client.GetFromJsonAsync<PlayerProfile>("/api/players/Aldric%20Stormborn", Json);

        Assert.Equal("Aldric Stormborn", profile!.Name);
    }

    [Fact]
    public async Task Player_ReturnsNotFoundForUnknownNames()
    {
        var response = await _client.GetAsync("/api/players/Nobody%20Here");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Csv_HasTheRightContentTypeAndHeader()
    {
        var response = await _client.GetAsync("/api/highscores/export.csv?category=magiclevel");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("Rank,Name,Level,Vocation,Value\r\n", body);
        Assert.Equal(41, body.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public async Task OnlineAndDeaths_ReturnLists()
    {
        var online = await _client.GetFromJsonAsync<List<OnlinePlayer>>("/api/online", Json);
        var deaths = await _client.GetFromJsonAsync<List<DeathEntry>>("/api/deaths?limit=3", Json);

        Assert.Equal(8, online!.Count);
        Assert.Equal(3, deaths!.Count);
    }

    [Fact]
    public async Task Spells_ListClassesSpellsAndDetail()
    {
        var classes = await _client.GetFromJsonAsync<List<SpellClass>>("/api/spells/classes", Json);
        var spells = await _client.GetFromJsonAsync<List<SpellSummary>>("/api/spells?class=Tidebinder", Json);
        var detail = await _client.GetFromJsonAsync<SpellDetail>("/api/spells/Tidebinder/Undertow", Json);
        var missing = await _client.GetAsync("/api/spells/Tidebinder/Nothing");

        Assert.Equal(3, classes!.Count);
        Assert.Equal("Undertow", Assert.Single(spells!).Name);
        Assert.Equal("200", detail!.Reference.Level);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Vocations_ListsTheCatalog()
    {
        var vocations = await _client.GetFromJsonAsync<JsonElement>("/api/vocations", Json);

        Assert.Equal(VocationCatalog.All.Count, vocations.GetArrayLength());
    }

    [Fact]
    public async Task Cors_AllowsGetOnly()
    {
        using var get = new HttpRequestMessage(HttpMethod.Get, "/api/status");
        get.Headers.Add("Origin", "http://example.test");
        var getResponse = await _client.SendAsync(get);

        using var preflight = new HttpRequestMessage(HttpMethod.Options, "/api/status");
        preflight.Headers.Add("Origin", "http://example.test");
        preflight.Headers.Add("Access-Control-Request-Method", "POST");
        var preflightResponse = await _client.SendAsync(preflight);

        Assert.True(getResponse.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(preflightResponse.Headers.TryGetValues("Access-Control-Allow-Methods", out var methods)
            && methods.Any(m => m.Contains("POST", StringComparison.OrdinalIgnoreCase)));
    }
}

public class ApiBehaviorTests
{
    [Fact]
    public async Task RateLimit_Returns429AfterThePermitIsSpent()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b => b.UseSetting("Valthera:RateLimit:PermitPerMinute", "3"));
        var client = factory.CreateClient();

        var codes = new List<HttpStatusCode>();
        for (var i = 0; i < 5; i++)
        {
            codes.Add((await client.GetAsync("/api/online?n=" + i)).StatusCode);
        }

        Assert.Equal(
            [HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests, HttpStatusCode.TooManyRequests],
            codes);
    }

    [Fact]
    public void MySqlMode_WithoutAConnectionString_FailsWithAClearMessage()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b => b.UseSetting("Valthera:DataSource", "MySql"));

        var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains("ConnectionStrings", ex.ToString());
    }
}
