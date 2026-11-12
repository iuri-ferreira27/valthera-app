using Valthera.Core.Contracts;
using Valthera.Core.Sample;
using Valthera.Core.Spells;
using Valthera.Core.Status;
using Valthera.Data;

namespace Valthera.Api.Configuration;

/// <summary>Dependency injection setup for the Valthera services.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the data source, status provider and spell catalog according to the <c>Valthera</c> configuration.
    /// </summary>
    /// <exception cref="InvalidOperationException">MySQL mode was selected but no connection string is configured.</exception>
    public static IServiceCollection AddValtheraServices(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(ValtheraOptions.SectionName).Get<ValtheraOptions>() ?? new ValtheraOptions();
        services.AddSingleton(options);

        switch (options.DataSource)
        {
            case DataSourceKind.MySql:
                var connectionString = configuration.GetConnectionString(ValtheraOptions.ConnectionStringName);
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    throw new InvalidOperationException(
                        $"Valthera:DataSource is 'MySql' but connection string '{ValtheraOptions.ConnectionStringName}' is not configured. " +
                        "Set it with user-secrets ('dotnet user-secrets set \"ConnectionStrings:Valthera\" \"<value>\"') " +
                        "or the ConnectionStrings__Valthera environment variable.");
                }

                services.AddSingleton<IServerDataSource>(_ => new MySqlServerDataSource(connectionString));
                services.AddSingleton(new OtStatusOptions
                {
                    ServerName = options.ServerName,
                    Host = options.Status.Host,
                    Port = options.Status.Port,
                    TimeoutMs = options.Status.TimeoutMs,
                });
                services.AddSingleton<IServerStatusProvider, OtStatusClient>();
                break;

            default:
                services.AddSingleton<IServerDataSource, SampleServerDataSource>();
                services.AddSingleton<IServerStatusProvider>(sp =>
                    new SampleStatusProvider(options.ServerName, sp.GetRequiredService<IServerDataSource>()));
                break;
        }

        services.AddSingleton<ISpellCatalog>(sp =>
        {
            var folder = options.Spells.Folder;
            if (string.IsNullOrWhiteSpace(folder))
            {
                return SampleSpells.CreateCatalog();
            }

            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<ValspellCatalog>();
            var catalog = ValspellCatalog.FromFolder(folder, logger);
            logger.LogInformation(
                "Spell catalog loaded: {Files} file(s), {Skipped} skipped, {Classes} class(es).",
                catalog.LoadedFiles, catalog.SkippedFiles, catalog.GetClasses().Count);
            return catalog;
        });

        return services;
    }
}
