using Microsoft.Extensions.DependencyInjection;

namespace Valthera.Components.Services;

/// <summary>Registration helpers for hosts that render the Valthera components.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ValtheraApiClient"/>. The host must also register <see cref="IApiEndpointStore"/>
    /// and <see cref="IExternalLinkOpener"/>.
    /// </summary>
    public static IServiceCollection AddValtheraComponents(this IServiceCollection services)
    {
        services.AddSingleton(sp => new ValtheraApiClient(
            new HttpClient { Timeout = TimeSpan.FromSeconds(10) },
            sp.GetRequiredService<IApiEndpointStore>()));
        return services;
    }
}
