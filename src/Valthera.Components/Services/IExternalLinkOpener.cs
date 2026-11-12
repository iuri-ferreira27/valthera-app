namespace Valthera.Components.Services;

/// <summary>Opens a URL outside the app, for example in the system browser.</summary>
public interface IExternalLinkOpener
{
    /// <summary>Opens the given absolute URL.</summary>
    Task OpenAsync(Uri uri);
}
