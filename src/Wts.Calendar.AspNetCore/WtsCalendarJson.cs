using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wts.Calendar.AspNetCore;

internal static class WtsCalendarJson
{
    internal static readonly JsonSerializerOptions Default = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };

    internal static WtsCalendarEvent Clone(WtsCalendarEvent value)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, Default);
        return JsonSerializer.Deserialize<WtsCalendarEvent>(bytes, Default)
            ?? throw new InvalidOperationException("The calendar event could not be cloned.");
    }
}
