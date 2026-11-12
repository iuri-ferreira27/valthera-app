using Valthera.Components.Services;

namespace Valthera.App.Services;

/// <summary>Opens links in the system browser through the MAUI launcher.</summary>
public sealed class LauncherExternalLinkOpener : IExternalLinkOpener
{
    /// <inheritdoc />
    public Task OpenAsync(Uri uri) => Launcher.Default.OpenAsync(uri);
}
