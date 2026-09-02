namespace Wts.Calendar.AspNetCore;

/// <summary>Safety and compatibility limits for WTS Calendar endpoints.</summary>
public sealed class WtsCalendarEndpointOptions
{
    /// <summary>Default number of events returned by a range query.</summary>
    public int DefaultPageSize { get; set; } = 250;

    /// <summary>Maximum number of events returned by one range query.</summary>
    public int MaxPageSize { get; set; } = 1_000;

    /// <summary>Maximum accepted query window.</summary>
    public TimeSpan MaxQueryWindow { get; set; } = TimeSpan.FromDays(366);

    /// <summary>Maximum request body size enforced when Content-Length is known.</summary>
    public long MaxPayloadBytes { get; set; } = 128 * 1024;

    /// <summary>Maximum title length.</summary>
    public int MaxTitleLength { get; set; } = 300;

    /// <summary>Maximum description length.</summary>
    public int MaxDescriptionLength { get; set; } = 16_000;

    /// <summary>Maximum serialized custom-property size.</summary>
    public int MaxCustomDataBytes { get; set; } = 32 * 1024;

    /// <summary>Require If-Match on update requests.</summary>
    public bool RequireIfMatchForUpdate { get; set; }

    /// <summary>Require If-Match on delete requests.</summary>
    public bool RequireIfMatchForDelete { get; set; }

    internal void Validate()
    {
        if (DefaultPageSize <= 0 || MaxPageSize <= 0 || DefaultPageSize > MaxPageSize)
        {
            throw new InvalidOperationException(
                "DefaultPageSize must be positive and no larger than MaxPageSize.");
        }

        if (MaxQueryWindow <= TimeSpan.Zero || MaxPayloadBytes <= 0 ||
            MaxTitleLength <= 0 || MaxDescriptionLength <= 0 || MaxCustomDataBytes <= 0)
        {
            throw new InvalidOperationException("WTS Calendar endpoint limits must be positive.");
        }
    }
}
