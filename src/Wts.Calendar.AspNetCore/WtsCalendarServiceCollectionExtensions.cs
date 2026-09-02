using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Dependency-injection registration for WTS Calendar.</summary>
public static class WtsCalendarServiceCollectionExtensions
{
    /// <summary>Adds WTS Calendar endpoint services without choosing storage.</summary>
    public static IServiceCollection AddWtsCalendarAspNetCore(
        this IServiceCollection services,
        Action<Wts.Calendar.AspNetCore.WtsCalendarEndpointOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = services.AddOptions<Wts.Calendar.AspNetCore.WtsCalendarEndpointOptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        services.TryAddSingleton<Wts.Calendar.AspNetCore.WtsCalendarEventValidator>();
        return services;
    }

    /// <summary>
    /// Adds process-local storage for samples and tests. Do not use this store
    /// when events must survive restarts or multiple server instances.
    /// </summary>
    public static IServiceCollection AddWtsCalendarInMemoryStore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton<
            Wts.Calendar.AspNetCore.IWtsCalendarEventStore,
            Wts.Calendar.AspNetCore.InMemoryWtsCalendarEventStore>();
        return services;
    }
}
