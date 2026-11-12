using Valthera.Components.Services;

namespace Valthera.App.Services;

/// <summary>Keeps the API base URL in the platform preferences of the MAUI app.</summary>
public sealed class PreferencesApiEndpointStore : IApiEndpointStore
{
    private const string Key = "api_base_url";

    /// <inheritdoc />
    public string GetBaseUrl() => Preferences.Default.Get(Key, IApiEndpointStore.DefaultBaseUrl);

    /// <inheritdoc />
    public void SetBaseUrl(string url) => Preferences.Default.Set(Key, url);
}
