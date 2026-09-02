using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wts.Calendar.AspNetCore;

/// <summary>
/// Represents either an ISO 8601 instant with an explicit offset or an all-day
/// calendar date in <c>yyyy-MM-dd</c> form.
/// </summary>
[JsonConverter(typeof(WtsCalendarDateTimeJsonConverter))]
public readonly record struct WtsCalendarDateTime
{
    private readonly string? _value;

    private WtsCalendarDateTime(string value, bool isDateOnly)
    {
        _value = value;
        IsDateOnly = isDateOnly;
    }

    /// <summary>Gets whether this value represents an all-day calendar date.</summary>
    public bool IsDateOnly { get; }

    /// <summary>Gets whether the value was initialized with a valid date or instant.</summary>
    public bool IsValid => _value is not null;

    /// <summary>Creates an all-day calendar date.</summary>
    public static WtsCalendarDateTime FromDate(DateOnly value) =>
        new(value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), true);

    /// <summary>Creates an ISO 8601 instant while preserving its UTC offset.</summary>
    public static WtsCalendarDateTime FromDateTimeOffset(DateTimeOffset value) =>
        new(value.ToString("O", CultureInfo.InvariantCulture), false);

    /// <summary>Parses a WTS date or date-time value.</summary>
    public static WtsCalendarDateTime Parse(string value)
    {
        if (!TryParse(value, out var parsed))
        {
            throw new FormatException(
                "Calendar date-times must be yyyy-MM-dd or ISO 8601 with Z/an explicit UTC offset.");
        }

        return parsed;
    }

    /// <summary>Attempts to parse a WTS date or date-time value.</summary>
    public static bool TryParse(string? value, out WtsCalendarDateTime parsed)
    {
        parsed = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim();
        if (candidate.Length == 10 &&
            DateOnly.TryParseExact(candidate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
        {
            parsed = FromDate(date);
            return true;
        }

        if (!HasExplicitOffset(candidate) ||
            !DateTimeOffset.TryParse(candidate, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.RoundtripKind, out var instant))
        {
            return false;
        }

        parsed = FromDateTimeOffset(instant);
        return true;
    }

    /// <summary>Gets the all-day date.</summary>
    public DateOnly AsDateOnly() => IsValid && IsDateOnly
        ? DateOnly.ParseExact(_value!, "yyyy-MM-dd", CultureInfo.InvariantCulture)
        : throw new InvalidOperationException("The value is not an all-day date.");

    /// <summary>Gets the offset-aware date-time.</summary>
    public DateTimeOffset AsDateTimeOffset() => IsValid && !IsDateOnly
        ? DateTimeOffset.Parse(_value!, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
        : throw new InvalidOperationException("The value is not an offset-aware date-time.");

    internal DateTimeOffset ToRangeInstant(TimeZoneInfo? timeZone)
    {
        if (!IsDateOnly)
        {
            return AsDateTimeOffset();
        }

        var date = AsDateOnly();
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var zone = timeZone ?? TimeZoneInfo.Utc;
        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }

    /// <inheritdoc />
    public override string ToString() => _value ?? string.Empty;

    private static bool HasExplicitOffset(string value)
    {
        if (!value.Contains('T', StringComparison.Ordinal))
        {
            return false;
        }

        if (value.EndsWith('Z') || value.EndsWith('z'))
        {
            return true;
        }

        return value.Length >= 6 &&
               (value[^6] == '+' || value[^6] == '-') &&
               value[^3] == ':';
    }
}

/// <summary>JSON converter for <see cref="WtsCalendarDateTime"/>.</summary>
public sealed class WtsCalendarDateTimeJsonConverter : JsonConverter<WtsCalendarDateTime>
{
    /// <inheritdoc />
    public override WtsCalendarDateTime Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var value = reader.TokenType == JsonTokenType.String ? reader.GetString() : null;
        if (!WtsCalendarDateTime.TryParse(value, out var parsed))
        {
            throw new JsonException(
                "Calendar date-times must be yyyy-MM-dd or ISO 8601 with Z/an explicit UTC offset.");
        }

        return parsed;
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        WtsCalendarDateTime value,
        JsonSerializerOptions options)
    {
        if (!value.IsValid)
        {
            throw new JsonException("An uninitialized calendar date-time cannot be serialized.");
        }

        writer.WriteStringValue(value.ToString());
    }
}
