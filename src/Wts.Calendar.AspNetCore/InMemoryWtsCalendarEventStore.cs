namespace Wts.Calendar.AspNetCore;

/// <summary>
/// Thread-safe, process-local event storage for samples and tests. Data is
/// lost on process restart; use a durable <see cref="IWtsCalendarEventStore"/>
/// implementation in production.
/// </summary>
public sealed class InMemoryWtsCalendarEventStore : IWtsCalendarEventStore
{
    private sealed record Entry(WtsCalendarEvent Event, long Version);

    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _events = new(StringComparer.Ordinal);
    private long _collectionVersion;

    /// <inheritdoc />
    public ValueTask<WtsCalendarEventPage> QueryAsync(
        WtsCalendarEventQuery query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var zone = ResolveTimeZone(query.TimeZone);

        lock (_gate)
        {
            var offset = ParseCursor(query.Cursor);
            var matches = _events.Values
                .Where(entry => Overlaps(entry.Event, query.Start, query.End, zone))
                .OrderBy(entry => entry.Event.Start.ToRangeInstant(zone))
                .ThenBy(entry => entry.Event.Id, StringComparer.Ordinal)
                .ToArray();

            var page = matches
                .Skip(offset)
                .Take(query.Limit)
                .Select(entry => WtsCalendarJson.Clone(entry.Event))
                .ToArray();
            var nextOffset = offset + page.Length;
            var cursor = nextOffset < matches.Length ? nextOffset.ToString() : null;

            return ValueTask.FromResult(new WtsCalendarEventPage(
                page,
                cursor,
                _collectionVersion.ToString()));
        }
    }

    /// <inheritdoc />
    public ValueTask<WtsCalendarStoredEvent?> FindAsync(
        string id,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            return ValueTask.FromResult(
                _events.TryGetValue(id, out var entry)
                    ? new WtsCalendarStoredEvent(
                        WtsCalendarJson.Clone(entry.Event), entry.Version.ToString())
                    : null);
        }
    }

    /// <inheritdoc />
    public ValueTask<WtsCalendarMutationResult> CreateAsync(
        WtsCalendarEvent calendarEvent,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var id = string.IsNullOrWhiteSpace(calendarEvent.Id)
            ? Guid.NewGuid().ToString("N")
            : calendarEvent.Id;
        var created = WtsCalendarJson.Clone(calendarEvent with { Id = id });

        lock (_gate)
        {
            if (_events.ContainsKey(id))
            {
                return ValueTask.FromResult(new WtsCalendarMutationResult(
                    WtsCalendarMutationStatus.Conflict,
                    Message: "An event with this id already exists."));
            }

            var entry = new Entry(created, 1);
            _events.Add(id, entry);
            _collectionVersion++;
            return ValueTask.FromResult(new WtsCalendarMutationResult(
                WtsCalendarMutationStatus.Created,
                ToStored(entry)));
        }
    }

    /// <inheritdoc />
    public ValueTask<WtsCalendarMutationResult> UpdateAsync(
        string id,
        WtsCalendarEvent calendarEvent,
        string? expectedVersion,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_events.TryGetValue(id, out var current))
            {
                return ValueTask.FromResult(new WtsCalendarMutationResult(
                    WtsCalendarMutationStatus.NotFound));
            }

            if (!VersionMatches(current.Version, expectedVersion))
            {
                return ValueTask.FromResult(new WtsCalendarMutationResult(
                    WtsCalendarMutationStatus.Conflict,
                    ToStored(current),
                    "The event changed after it was loaded."));
            }

            var updated = new Entry(
                WtsCalendarJson.Clone(calendarEvent with { Id = id }),
                current.Version + 1);
            _events[id] = updated;
            _collectionVersion++;
            return ValueTask.FromResult(new WtsCalendarMutationResult(
                WtsCalendarMutationStatus.Updated,
                ToStored(updated)));
        }
    }

    /// <inheritdoc />
    public ValueTask<WtsCalendarMutationResult> DeleteAsync(
        string id,
        string? expectedVersion,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_gate)
        {
            if (!_events.TryGetValue(id, out var current))
            {
                return ValueTask.FromResult(new WtsCalendarMutationResult(
                    WtsCalendarMutationStatus.NotFound));
            }

            if (!VersionMatches(current.Version, expectedVersion))
            {
                return ValueTask.FromResult(new WtsCalendarMutationResult(
                    WtsCalendarMutationStatus.Conflict,
                    ToStored(current),
                    "The event changed after it was loaded."));
            }

            _events.Remove(id);
            _collectionVersion++;
            return ValueTask.FromResult(new WtsCalendarMutationResult(
                WtsCalendarMutationStatus.Deleted));
        }
    }

    private static WtsCalendarStoredEvent ToStored(Entry entry) =>
        new(WtsCalendarJson.Clone(entry.Event), entry.Version.ToString());

    private static bool VersionMatches(long version, string? expectedVersion) =>
        expectedVersion is null or "*" ||
        string.Equals(version.ToString(), expectedVersion, StringComparison.Ordinal);

    private static int ParseCursor(string? cursor) =>
        cursor is not null && int.TryParse(cursor, out var parsed) && parsed >= 0 ? parsed : 0;

    private static TimeZoneInfo? ResolveTimeZone(string? id) =>
        string.IsNullOrWhiteSpace(id) ? null : TimeZoneInfo.FindSystemTimeZoneById(id);

    private static bool Overlaps(
        WtsCalendarEvent calendarEvent,
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEnd,
        TimeZoneInfo? zone)
    {
        var eventStart = calendarEvent.Start.ToRangeInstant(zone);
        if (calendarEvent.End is { IsValid: true } end)
        {
            return eventStart < rangeEnd && end.ToRangeInstant(zone) > rangeStart;
        }

        if (calendarEvent.Start.IsDateOnly)
        {
            return eventStart < rangeEnd && eventStart.AddDays(1) > rangeStart;
        }

        return eventStart >= rangeStart && eventStart < rangeEnd;
    }
}
