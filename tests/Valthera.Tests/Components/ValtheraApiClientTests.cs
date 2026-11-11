using System.Net;
using System.Text;
using Valthera.Components.Services;

namespace Valthera.Tests.Components;

public sealed class ValtheraApiClientTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(respond(request));
        }
    }

    private sealed class FakeStore(string url) : IApiEndpointStore
    {
        public string Url { get; private set; } = url;

        public string GetBaseUrl() => Url;

        public void SetBaseUrl(string value) => Url = value;
    }

    private static HttpResponseMessage Json(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static (ValtheraApiClient Client, StubHandler Handler, FakeStore Store) Create(
        Func<HttpRequestMessage, HttpResponseMessage> respond, string baseUrl = "http://api.test")
    {
        var handler = new StubHandler(respond);
        var store = new FakeStore(baseUrl);
        return (new ValtheraApiClient(new HttpClient(handler), store), handler, store);
    }

    [Fact]
    public async Task GetStatusAsync_ParsesTheStatusJson()
    {
        var (client, handler, _) = Create(_ => Json("""
            { "name": "Valthera", "online": true, "playersOnline": 7, "playersMax": 100, "playersPeak": 12,
              "uptime": "1.02:03:04", "version": "10.98", "checkedAt": "2026-01-02T03:04:05+00:00" }
            """));

        var result = await client.GetStatusAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal("Valthera", result.Value!.Name);
        Assert.True(result.Value.Online);
        Assert.Equal(7, result.Value.PlayersOnline);
        Assert.Equal(new TimeSpan(1, 2, 3, 4), result.Value.Uptime);
        Assert.Equal(new Uri("http://api.test/api/status"), Assert.Single(handler.Requests));
    }

    [Fact]
    public async Task GetHighscoresAsync_ParsesEntriesAndSendsTheFilters()
    {
        var (client, handler, _) = Create(_ => Json("""
            { "category": "level", "page": 2, "pageSize": 25, "total": 40,
              "entries": [ { "rank": 26, "name": "Aldric Stormborn", "level": 150, "vocation": "Elite Knight", "value": 5000000 } ] }
            """));

        var result = await client.GetHighscoresAsync("level", 8, 2, 25);

        Assert.True(result.IsSuccess);
        Assert.Equal(40, result.Value!.Total);
        var entry = Assert.Single(result.Value.Entries);
        Assert.Equal(("Aldric Stormborn", 26, 5_000_000L), (entry.Name, entry.Rank, entry.Value));
        Assert.Equal(
            "http://api.test/api/highscores?category=level&vocation=8&page=2&pageSize=25",
            Assert.Single(handler.Requests).ToString());
    }

    [Fact]
    public async Task GetPlayerAsync_ReturnsNotFoundFor404()
    {
        var (client, handler, _) = Create(_ => Json("{}", HttpStatusCode.NotFound));

        var result = await client.GetPlayerAsync("Nobody Here");

        Assert.True(result.IsNotFound);
        Assert.Null(result.Value);
        Assert.Contains("não encontrado", result.Message);
        Assert.EndsWith("/api/players/Nobody%20Here", Assert.Single(handler.Requests).AbsoluteUri);
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsAnErrorResultWhenTheApiIsUnreachable()
    {
        var (client, _, _) = Create(_ => throw new HttpRequestException("connection refused"));

        var result = await client.GetStatusAsync();

        Assert.Equal(ApiStatus.Error, result.Status);
        Assert.Contains("conectar", result.Message);
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsAnErrorResultOnServerErrorAndRateLimit()
    {
        var (serverError, _, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var (limited, _, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));

        Assert.Contains("500", (await serverError.GetStatusAsync()).Message);
        Assert.Contains("Muitas requisições", (await limited.GetStatusAsync()).Message);
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsAnErrorResultForMalformedJson()
    {
        var (client, _, _) = Create(_ => Json("<html>not json</html>"));

        Assert.Equal(ApiStatus.Error, (await client.GetStatusAsync()).Status);
    }

    [Fact]
    public async Task Requests_FollowTheStoredBaseUrlWithoutRecreatingTheClient()
    {
        var (client, handler, store) = Create(_ => Json("[]"));

        await client.GetOnlineAsync();
        store.SetBaseUrl("https://other.test/");
        await client.GetOnlineAsync();

        Assert.Equal(["http://api.test/api/online", "https://other.test/api/online"], handler.Requests.Select(r => r.ToString()));
    }

    [Fact]
    public async Task InvalidBaseUrl_ReturnsAnErrorResultWithoutCallingTheApi()
    {
        var (client, handler, _) = Create(_ => Json("{}"), baseUrl: "not a url");

        var result = await client.GetStatusAsync();

        Assert.Equal(ApiStatus.Error, result.Status);
        Assert.Empty(handler.Requests);
        Assert.Null(client.GetHighscoresCsvUrl("level", null));
    }

    [Fact]
    public void GetHighscoresCsvUrl_BuildsTheExportUrl()
    {
        var (client, _, _) = Create(_ => Json("{}"));

        Assert.Equal(
            "http://api.test/api/highscores/export.csv?category=magiclevel&vocation=3",
            client.GetHighscoresCsvUrl("magiclevel", 3)!.ToString());
    }
}
