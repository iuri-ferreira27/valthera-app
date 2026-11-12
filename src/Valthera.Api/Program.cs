using System.Threading.RateLimiting;
using Valthera.Api.Configuration;
using Valthera.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

var options = builder.Configuration.GetSection(ValtheraOptions.SectionName).Get<ValtheraOptions>() ?? new ValtheraOptions();

builder.Services.AddValtheraServices(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

builder.Services.AddCors(cors => cors.AddPolicy("get-only", policy => policy
    .AllowAnyOrigin()
    .WithMethods("GET")));

builder.Services.AddOutputCache(cache =>
{
    cache.AddPolicy(ApiEndpoints.CachePolicies.Status, p => p.Expire(TimeSpan.FromSeconds(15)));
    cache.AddPolicy(ApiEndpoints.CachePolicies.Online, p => p.Expire(TimeSpan.FromSeconds(15)));
    cache.AddPolicy(ApiEndpoints.CachePolicies.Deaths, p => p.Expire(TimeSpan.FromSeconds(30)).SetVaryByQuery("limit"));
    cache.AddPolicy(ApiEndpoints.CachePolicies.Highscores, p => p
        .Expire(TimeSpan.FromSeconds(60))
        .SetVaryByQuery("category", "vocation", "page", "pageSize"));
    cache.AddPolicy(ApiEndpoints.CachePolicies.Players, p => p
        .Expire(TimeSpan.FromSeconds(30))
        .SetVaryByRouteValue("name"));
    cache.AddPolicy(ApiEndpoints.CachePolicies.Spells, p => p
        .Expire(TimeSpan.FromMinutes(5))
        .SetVaryByQuery("class")
        .SetVaryByRouteValue("spellClass", "name"));
});

builder.Services.AddRateLimiter(limiter =>
{
    limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    limiter.AddPolicy(ApiEndpoints.RateLimitPolicy, httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = Math.Max(1, options.RateLimit.PermitPerMinute),
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            }));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    // Returns a ProblemDetails body without stack traces.
    app.UseExceptionHandler();
}

app.UseStatusCodePages();
app.UseCors();
app.UseRateLimiter();
app.UseOutputCache();

app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok(new { name = "Valthera API", endpoints = "/api", health = "/health" }));
app.MapValtheraApi();

app.Run();

/// <summary>Entry point marker so integration tests can reference the application.</summary>
public partial class Program;
