namespace Wts.Calendar.AspNetCore;

/// <summary>
/// Storage boundary used by the ASP.NET Core endpoints. Implement this
/// interface for EF Core, Dapper, document databases, or existing services.
/// </summary>
public interface IWtsCalendarEventStore
{
    /// <summary>Returns events overlapping the requested range.</summary>
    ValueTask<WtsCalendarEventPage> QueryAsync(
        WtsCalendarEventQuery query,
        CancellationToken cancellationToken);

    /// <summary>Returns one event and its version.</summary>
    ValueTask<WtsCalendarStoredEvent?> FindAsync(
        string id,
        CancellationToken cancellationToken);

    /// <summary>Creates an event.</summary>
    ValueTask<WtsCalendarMutationResult> CreateAsync(
        WtsCalendarEvent calendarEvent,
        CancellationToken cancellationToken);

    /// <summary>Updates an event when the expected version still matches.</summary>
    ValueTask<WtsCalendarMutationResult> UpdateAsync(
        string id,
        WtsCalendarEvent calendarEvent,
        string? expectedVersion,
        CancellationToken cancellationToken);

    /// <summary>Deletes an event when the expected version still matches.</summary>
    ValueTask<WtsCalendarMutationResult> DeleteAsync(
        string id,
        string? expectedVersion,
        CancellationToken cancellationToken);
}
