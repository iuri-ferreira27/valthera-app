namespace Valthera.Components.Services;

/// <summary>Stores the base URL of the Valthera API. Each host (MAUI app, website) provides its own storage.</summary>
public interface IApiEndpointStore
{
    /// <summary>Default URL used until the user changes it.</summary>
    public const string DefaultBaseUrl = "http://localhost:5080";

    /// <summary>Gets the current API base URL.</summary>
    string GetBaseUrl();

    /// <summary>Saves the API base URL. The change applies to the next request, without restarting the host.</summary>
    void SetBaseUrl(string url);
}
