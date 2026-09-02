using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Wts.Calendar.AspNetCore;

/// <summary>
/// The server-side wire contract accepted by <c>@wts-calendar/core</c>. Known
/// fields are typed and future/custom fields are preserved in
/// <see cref="AdditionalData"/>.
/// </summary>
public sealed record WtsCalendarEvent
{
    /// <summary>Stable event identifier.</summary>
    public string? Id { get; init; }

    /// <summary>Visible event title.</summary>
    public required string Title { get; init; }

    /// <summary>Event start as an all-day date or offset-aware instant.</summary>
    public required WtsCalendarDateTime Start { get; init; }

    /// <summary>Optional exclusive event end.</summary>
    public WtsCalendarDateTime? End { get; init; }

    /// <summary>Optional event description.</summary>
    public string? Description { get; init; }

    /// <summary>Optional safe navigation target.</summary>
    public string? Url { get; init; }

    /// <summary>Explicitly marks an all-day event.</summary>
    public bool? IsAllDay { get; init; }

    /// <summary>Controls whether the event can be edited.</summary>
    public bool? Editable { get; init; }

    /// <summary>Controls whether the event can be removed.</summary>
    public bool? Removeable { get; init; }

    /// <summary>Controls event rendering mode.</summary>
    public string? Display { get; init; }

    /// <summary>Foreground event color.</summary>
    public string? Color { get; init; }

    /// <summary>Foreground event text color.</summary>
    public string? TextColor { get; init; }

    /// <summary>Additional event class names.</summary>
    public IReadOnlyList<string>? ClassNames { get; init; }

    /// <summary>Assigned resource identifier.</summary>
    public string? ResourceId { get; init; }

    /// <summary>Capacity consumed on the assigned resource.</summary>
    public decimal? ResourceUnits { get; init; }

    /// <summary>Optional recurrence rule.</summary>
    public string? RRule { get; init; }

    /// <summary>IANA time zone used for recurrence expansion.</summary>
    public string? RecurrenceTimeZone { get; init; }

    /// <summary>Additional recurrence dates.</summary>
    public IReadOnlyList<WtsCalendarDateTime>? RDate { get; init; }

    /// <summary>Recurrence exception dates.</summary>
    public IReadOnlyList<WtsCalendarDateTime>? ExDate { get; init; }

    /// <summary>FullCalendar-style custom properties.</summary>
    public JsonObject? ExtendedProps { get; init; }

    /// <summary>WTS metadata.</summary>
    public JsonObject? Meta { get; init; }

    /// <summary>
    /// Unknown top-level properties preserved for forwards compatibility.
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalData { get; init; }
}

/// <summary>A page returned by a WTS calendar event store.</summary>
public sealed record WtsCalendarEventPage(
    IReadOnlyList<WtsCalendarEvent> Records,
    string? Cursor = null,
    string? Version = null,
    object? Meta = null);

/// <summary>An event plus its opaque storage version.</summary>
public sealed record WtsCalendarStoredEvent(WtsCalendarEvent Event, string Version);

/// <summary>A bounded calendar event query.</summary>
public sealed record WtsCalendarEventQuery(
    DateTimeOffset Start,
    DateTimeOffset End,
    string? TimeZone,
    string? Cursor,
    int Limit);

/// <summary>The outcome of a storage mutation.</summary>
public sealed record WtsCalendarMutationResult(
    WtsCalendarMutationStatus Status,
    WtsCalendarStoredEvent? StoredEvent = null,
    string? Message = null);

/// <summary>Storage mutation outcomes understood by the HTTP adapter.</summary>
public enum WtsCalendarMutationStatus
{
    /// <summary>An event was created.</summary>
    Created,

    /// <summary>An event was updated.</summary>
    Updated,

    /// <summary>An event was deleted.</summary>
    Deleted,

    /// <summary>The requested event does not exist.</summary>
    NotFound,

    /// <summary>The id or expected version conflicts with current storage.</summary>
    Conflict,

    /// <summary>The storage implementation rejected the mutation.</summary>
    Rejected,
}
