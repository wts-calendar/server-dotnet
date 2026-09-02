using System.Text;
using System.Text.Json;

namespace Wts.Calendar.AspNetCore;

/// <summary>Validates WTS Calendar event wire contracts at the API boundary.</summary>
public sealed class WtsCalendarEventValidator
{
    /// <summary>Validates an event and returns errors grouped by JSON field name.</summary>
    public IDictionary<string, string[]> Validate(
        WtsCalendarEvent calendarEvent,
        WtsCalendarEndpointOptions options)
    {
        ArgumentNullException.ThrowIfNull(calendarEvent);
        ArgumentNullException.ThrowIfNull(options);

        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        if (string.IsNullOrWhiteSpace(calendarEvent.Title))
        {
            Add(errors, "title", "A non-empty title is required.");
        }
        else if (calendarEvent.Title.Length > options.MaxTitleLength)
        {
            Add(errors, "title", $"Title must not exceed {options.MaxTitleLength} characters.");
        }

        if (!calendarEvent.Start.IsValid)
        {
            Add(errors, "start", "A valid start is required.");
        }

        if (calendarEvent.Id is { } id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 200 || ContainsControlCharacter(id))
            {
                Add(errors, "id", "Id must be 1-200 printable characters.");
            }
        }

        if (calendarEvent.Description?.Length > options.MaxDescriptionLength)
        {
            Add(errors, "description",
                $"Description must not exceed {options.MaxDescriptionLength} characters.");
        }

        if (calendarEvent.ResourceId is { Length: > 200 })
        {
            Add(errors, "resourceId", "ResourceId must not exceed 200 characters.");
        }

        if (calendarEvent.ResourceUnits is <= 0)
        {
            Add(errors, "resourceUnits", "ResourceUnits must be greater than zero.");
        }

        if (calendarEvent.RRule?.Length > 4_096)
        {
            Add(errors, "rrule", "RRule must not exceed 4096 characters.");
        }

        if (calendarEvent.ClassNames is { Count: > 64 } ||
            calendarEvent.ClassNames?.Any(value => value.Length > 200) == true)
        {
            Add(errors, "classNames", "At most 64 class names of 200 characters each are allowed.");
        }

        if (calendarEvent.Start.IsValid && calendarEvent.End is { IsValid: true } end)
        {
            if (calendarEvent.Start.IsDateOnly != end.IsDateOnly)
            {
                Add(errors, "end", "Start and end must both be dates or both be date-times.");
            }
            else if (calendarEvent.Start.IsDateOnly)
            {
                if (end.AsDateOnly() <= calendarEvent.Start.AsDateOnly())
                {
                    Add(errors, "end", "End must be later than start.");
                }
            }
            else if (end.AsDateTimeOffset() <= calendarEvent.Start.AsDateTimeOffset())
            {
                Add(errors, "end", "End must be later than start.");
            }
        }

        if (calendarEvent.IsAllDay == true && calendarEvent.Start.IsValid &&
            (!calendarEvent.Start.IsDateOnly || calendarEvent.End is { IsDateOnly: false }))
        {
            Add(errors, "isAllDay", "All-day events must use yyyy-MM-dd start and end values.");
        }

        ValidateUrl(calendarEvent.Url, errors);
        ValidateCustomData(calendarEvent, options, errors);

        return errors.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToArray(),
            StringComparer.Ordinal);
    }

    private static void ValidateUrl(string? value, Dictionary<string, List<string>> errors)
    {
        if (value is null)
        {
            return;
        }

        if (value.Length > 2_048 || ContainsControlCharacter(value))
        {
            Add(errors, "url", "Url must not exceed 2048 printable characters.");
            return;
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute) &&
            absolute.Scheme is not ("http" or "https"))
        {
            Add(errors, "url", "Absolute URLs must use HTTP or HTTPS.");
        }
    }

    private static void ValidateCustomData(
        WtsCalendarEvent calendarEvent,
        WtsCalendarEndpointOptions options,
        Dictionary<string, List<string>> errors)
    {
        try
        {
            var size = 0;
            if (calendarEvent.ExtendedProps is not null)
            {
                size += Encoding.UTF8.GetByteCount(calendarEvent.ExtendedProps.ToJsonString());
            }

            if (calendarEvent.Meta is not null)
            {
                size += Encoding.UTF8.GetByteCount(calendarEvent.Meta.ToJsonString());
            }

            if (calendarEvent.AdditionalData is not null)
            {
                size += JsonSerializer.SerializeToUtf8Bytes(calendarEvent.AdditionalData).Length;
            }

            if (size > options.MaxCustomDataBytes)
            {
                Add(errors, "extendedProps",
                    $"Combined custom data must not exceed {options.MaxCustomDataBytes} UTF-8 bytes.");
            }
        }
        catch (JsonException)
        {
            Add(errors, "extendedProps", "Custom data must be valid JSON.");
        }
        catch (InvalidOperationException)
        {
            Add(errors, "extendedProps", "Custom data must not contain JSON cycles.");
        }
    }

    private static bool ContainsControlCharacter(string value) =>
        value.Any(character => char.IsControl(character));

    private static void Add(
        Dictionary<string, List<string>> errors,
        string field,
        string message)
    {
        if (!errors.TryGetValue(field, out var messages))
        {
            messages = [];
            errors[field] = messages;
        }

        messages.Add(message);
    }
}
