using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Valthera.Core.Models;

namespace Valthera.Components.Services;

/// <summary>A vocation id with its display name, as returned by <c>/api/vocations</c>.</summary>
public sealed record VocationInfo(int Id, string Name);

/// <summary>
/// Typed client for the Valthera REST API. The base URL is read from <see cref="IApiEndpointStore"/> on every call,
/// so changing it in the settings page takes effect immediately. User-facing messages are in Brazilian Portuguese.
/// </summary>
public sealed class ValtheraApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly IApiEndpointStore _store;

    /// <summary>Creates a client over an existing <see cref="HttpClient"/>.</summary>
    public ValtheraApiClient(HttpClient http, IApiEndpointStore store)
    {
        _http = http;
        _store = store;
    }

    /// <summary>Gets the live server status.</summary>
    public Task<ApiResult<ServerStatus>> GetStatusAsync(CancellationToken ct = default) =>
        GetAsync<ServerStatus>("/api/status", ct);

    /// <summary>Checks that an API answers at the given base URL (used by the settings page).</summary>
    public Task<ApiResult<ServerStatus>> PingAsync(string baseUrl, CancellationToken ct = default) =>
        GetAsync<ServerStatus>("/api/status", ct, baseUrl);

    /// <summary>Gets one page of a highscore table.</summary>
    public Task<ApiResult<HighscorePage>> GetHighscoresAsync(
        string category, int? vocationId, int page, int pageSize, CancellationToken ct = default) =>
        GetAsync<HighscorePage>(
            $"/api/highscores?category={Uri.EscapeDataString(category)}{VocationQuery(vocationId)}&page={page}&pageSize={pageSize}",
            ct);

    /// <summary>Builds the absolute URL of the CSV export, or <c>null</c> when the configured base URL is invalid.</summary>
    public Uri? GetHighscoresCsvUrl(string category, int? vocationId) =>
        TryBuildUri($"/api/highscores/export.csv?category={Uri.EscapeDataString(category)}{VocationQuery(vocationId)}", null, out var uri)
            ? uri
            : null;

    /// <summary>Gets the characters currently online.</summary>
    public Task<ApiResult<IReadOnlyList<OnlinePlayer>>> GetOnlineAsync(CancellationToken ct = default) =>
        GetListAsync<OnlinePlayer>("/api/online", ct);

    /// <summary>Gets the most recent deaths.</summary>
    public Task<ApiResult<IReadOnlyList<DeathEntry>>> GetDeathsAsync(int limit = 20, CancellationToken ct = default) =>
        GetListAsync<DeathEntry>($"/api/deaths?limit={limit}", ct);

    /// <summary>Gets a character profile. A missing character is reported as <see cref="ApiStatus.NotFound"/>.</summary>
    public Task<ApiResult<PlayerProfile>> GetPlayerAsync(string name, CancellationToken ct = default) =>
        GetAsync<PlayerProfile>($"/api/players/{Uri.EscapeDataString(name)}", ct, notFoundMessage: "Personagem não encontrado.");

    /// <summary>Gets the spell classes.</summary>
    public Task<ApiResult<IReadOnlyList<SpellClass>>> GetSpellClassesAsync(CancellationToken ct = default) =>
        GetListAsync<SpellClass>("/api/spells/classes", ct);

    /// <summary>Gets spell summaries, optionally limited to one class.</summary>
    public Task<ApiResult<IReadOnlyList<SpellSummary>>> GetSpellsAsync(string? spellClass = null, CancellationToken ct = default) =>
        GetListAsync<SpellSummary>(
            string.IsNullOrWhiteSpace(spellClass) ? "/api/spells" : $"/api/spells?class={Uri.EscapeDataString(spellClass)}",
            ct);

    /// <summary>Gets the full description of a spell.</summary>
    public Task<ApiResult<SpellDetail>> GetSpellAsync(string spellClass, string name, CancellationToken ct = default) =>
        GetAsync<SpellDetail>(
            $"/api/spells/{Uri.EscapeDataString(spellClass)}/{Uri.EscapeDataString(name)}",
            ct,
            notFoundMessage: "Spell não encontrada.");

    /// <summary>Gets the known vocations.</summary>
    public Task<ApiResult<IReadOnlyList<VocationInfo>>> GetVocationsAsync(CancellationToken ct = default) =>
        GetListAsync<VocationInfo>("/api/vocations", ct);

    private static string VocationQuery(int? vocationId) => vocationId is { } id ? $"&vocation={id}" : string.Empty;

    private async Task<ApiResult<IReadOnlyList<T>>> GetListAsync<T>(string path, CancellationToken ct)
    {
        var result = await GetAsync<List<T>>(path, ct);
        return result.IsSuccess
            ? ApiResult<IReadOnlyList<T>>.Ok(result.Value ?? [])
            : new ApiResult<IReadOnlyList<T>>(result.Status, null, result.Message);
    }

    private bool TryBuildUri(string path, string? baseUrl, out Uri uri)
    {
        var text = (baseUrl ?? _store.GetBaseUrl())?.Trim().TrimEnd('/');
        if (!string.IsNullOrEmpty(text)
            && Uri.TryCreate(text + path, UriKind.Absolute, out var parsed)
            && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps))
        {
            uri = parsed;
            return true;
        }

        uri = null!;
        return false;
    }

    private async Task<ApiResult<T>> GetAsync<T>(
        string path, CancellationToken ct, string? baseUrl = null, string notFoundMessage = "Recurso não encontrado.")
    {
        if (!TryBuildUri(path, baseUrl, out var uri))
        {
            return ApiResult<T>.Error("A URL da API é inválida. Ajuste em Configurações.");
        }

        try
        {
            using var response = await _http.GetAsync(uri, ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return ApiResult<T>.NotFound(notFoundMessage);
            }

            if (!response.IsSuccessStatusCode)
            {
                return ApiResult<T>.Error(DescribeFailure(response.StatusCode));
            }

            var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct);
            return value is null
                ? ApiResult<T>.Error("A API enviou uma resposta vazia.")
                : ApiResult<T>.Ok(value);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return ApiResult<T>.Error("A API demorou demais para responder.");
        }
        catch (HttpRequestException)
        {
            return ApiResult<T>.Error("Não foi possível conectar à API. Verifique se ela está no ar e se a URL em Configurações está correta.");
        }
        catch (JsonException)
        {
            return ApiResult<T>.Error("A API enviou uma resposta em formato inesperado.");
        }
    }

    private static string DescribeFailure(HttpStatusCode status) => status switch
    {
        HttpStatusCode.TooManyRequests => "Muitas requisições em pouco tempo. Aguarde um instante e tente de novo.",
        HttpStatusCode.BadRequest => "A API recusou o pedido (parâmetros inválidos).",
        _ => $"A API respondeu com erro ({(int)status}).",
    };
}
