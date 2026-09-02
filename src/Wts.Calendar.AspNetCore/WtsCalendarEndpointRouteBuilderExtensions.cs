using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Options;

namespace Wts.Calendar.AspNetCore;

/// <summary>Minimal API endpoint mapping for the WTS Calendar REST adapter.</summary>
public static class WtsCalendarEndpointRouteBuilderExtensions
{
    private const string ProblemBase = "https://wts-calendar.github.io/docs/aspnet-core#";

    /// <summary>
    /// Maps range loading and CRUD endpoints compatible with
    /// <c>createRestCalendarDataAdapter</c>.
    /// </summary>
    /// <param name="endpoints">The application or route group.</param>
    /// <param name="pattern">Route prefix. Defaults to /api/calendar/events.</param>
    /// <returns>A route group that can be decorated with authorization, CORS, or rate limits.</returns>
    public static RouteGroupBuilder MapWtsCalendarEvents(
        this IEndpointRouteBuilder endpoints,
        string pattern = "/api/calendar/events")
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);

        var group = endpoints.MapGroup(pattern).WithTags("WTS Calendar");
        group.MapGet("", QueryAsync).WithName("WtsCalendar_QueryEvents");
        group.MapGet("/{id}", FindAsync).WithName("WtsCalendar_GetEvent");
        group.MapPost("", CreateAsync).WithName("WtsCalendar_CreateEvent");
        group.MapMethods("/{id}", [HttpMethods.Patch, HttpMethods.Put], UpdateAsync)
            .WithName("WtsCalendar_UpdateEvent");
        group.MapDelete("/{id}", DeleteAsync).WithName("WtsCalendar_DeleteEvent");
        return group;
    }

    private static async Task<IResult> QueryAsync(
        HttpContext context,
        IWtsCalendarEventStore store,
        IOptions<WtsCalendarEndpointOptions> configuredOptions,
        CancellationToken cancellationToken)
    {
        var options = GetOptions(configuredOptions);
        if (!TryCreateQuery(context.Request, options, out var query, out var problem))
        {
            return problem!;
        }

        var page = await store.QueryAsync(query!, cancellationToken).ConfigureAwait(false);
        context.Response.Headers.CacheControl = "private, no-store";
        if (!string.IsNullOrWhiteSpace(page.Version))
        {
            SetEntityTag(context.Response, page.Version);
        }

        return Results.Json(page, WtsCalendarJson.Default);
    }

    private static async Task<IResult> FindAsync(
        string id,
        HttpContext context,
        IWtsCalendarEventStore store,
        CancellationToken cancellationToken)
    {
        if (!IsValidRouteId(id))
        {
            return ValidationProblem("id", "Id must be 1-200 printable characters.");
        }

        var found = await store.FindAsync(id, cancellationToken).ConfigureAwait(false);
        if (found is null)
        {
            return NotFoundProblem();
        }

        context.Response.Headers.CacheControl = "private, no-cache";
        SetEntityTag(context.Response, found.Version);
        return Results.Json(found.Event, WtsCalendarJson.Default);
    }

    private static async Task<IResult> CreateAsync(
        HttpContext context,
        IWtsCalendarEventStore store,
        WtsCalendarEventValidator validator,
        IOptions<WtsCalendarEndpointOptions> configuredOptions,
        CancellationToken cancellationToken)
    {
        var options = GetOptions(configuredOptions);
        var (calendarEvent, bodyProblem) = await ReadEventAsync(
            context.Request, options, cancellationToken).ConfigureAwait(false);
        if (bodyProblem is not null)
        {
            return bodyProblem;
        }

        var errors = validator.Validate(calendarEvent!, options);
        if (errors.Count != 0)
        {
            return Results.ValidationProblem(
                errors,
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Calendar event validation failed.",
                type: ProblemBase + "validation");
        }

        var result = await store.CreateAsync(calendarEvent!, cancellationToken).ConfigureAwait(false);
        if (result.Status != WtsCalendarMutationStatus.Created || result.StoredEvent is null)
        {
            return MutationProblem(result);
        }

        SetEntityTag(context.Response, result.StoredEvent.Version);
        var location = $"{context.Request.Path.ToString().TrimEnd('/')}/{Uri.EscapeDataString(result.StoredEvent.Event.Id!)}";
        return Results.Created(location, result.StoredEvent.Event);
    }

    private static async Task<IResult> UpdateAsync(
        string id,
        HttpContext context,
        IWtsCalendarEventStore store,
        WtsCalendarEventValidator validator,
        IOptions<WtsCalendarEndpointOptions> configuredOptions,
        CancellationToken cancellationToken)
    {
        if (!IsValidRouteId(id))
        {
            return ValidationProblem("id", "Id must be 1-200 printable characters.");
        }

        var options = GetOptions(configuredOptions);
        if (!TryReadIfMatch(context.Request, out var expectedVersion, out var headerProblem))
        {
            return headerProblem!;
        }

        if (options.RequireIfMatchForUpdate && expectedVersion is null)
        {
            return PreconditionRequiredProblem();
        }

        var (calendarEvent, bodyProblem) = await ReadEventAsync(
            context.Request, options, cancellationToken).ConfigureAwait(false);
        if (bodyProblem is not null)
        {
            return bodyProblem;
        }

        calendarEvent = calendarEvent! with { Id = id };
        var errors = validator.Validate(calendarEvent, options);
        if (errors.Count != 0)
        {
            return Results.ValidationProblem(
                errors,
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Calendar event validation failed.",
                type: ProblemBase + "validation");
        }

        var result = await store.UpdateAsync(
            id, calendarEvent, expectedVersion, cancellationToken).ConfigureAwait(false);
        if (result.Status != WtsCalendarMutationStatus.Updated || result.StoredEvent is null)
        {
            return MutationProblem(result);
        }

        SetEntityTag(context.Response, result.StoredEvent.Version);
        return Results.Json(result.StoredEvent.Event, WtsCalendarJson.Default);
    }

    private static async Task<IResult> DeleteAsync(
        string id,
        HttpContext context,
        IWtsCalendarEventStore store,
        IOptions<WtsCalendarEndpointOptions> configuredOptions,
        CancellationToken cancellationToken)
    {
        if (!IsValidRouteId(id))
        {
            return ValidationProblem("id", "Id must be 1-200 printable characters.");
        }

        var options = GetOptions(configuredOptions);
        if (!TryReadIfMatch(context.Request, out var expectedVersion, out var headerProblem))
        {
            return headerProblem!;
        }

        if (options.RequireIfMatchForDelete && expectedVersion is null)
        {
            return PreconditionRequiredProblem();
        }

        var result = await store.DeleteAsync(
            id, expectedVersion, cancellationToken).ConfigureAwait(false);
        return result.Status == WtsCalendarMutationStatus.Deleted
            ? Results.NoContent()
            : MutationProblem(result);
    }

    private static WtsCalendarEndpointOptions GetOptions(
        IOptions<WtsCalendarEndpointOptions> configured)
    {
        var options = configured.Value;
        options.Validate();
        return options;
    }

    private static bool TryCreateQuery(
        HttpRequest request,
        WtsCalendarEndpointOptions options,
        out WtsCalendarEventQuery? query,
        out IResult? problem)
    {
        query = null;
        problem = null;

        if (!TryParseOffset(request.Query["start"], out var start) ||
            !TryParseOffset(request.Query["end"], out var end))
        {
            problem = ValidationProblem(
                "range",
                "Start and end are required ISO 8601 date-times with explicit UTC offsets.");
            return false;
        }

        if (end <= start)
        {
            problem = ValidationProblem("end", "End must be later than start.");
            return false;
        }

        if (end - start > options.MaxQueryWindow)
        {
            problem = ValidationProblem(
                "range",
                $"The requested range must not exceed {options.MaxQueryWindow.TotalDays:0} days.");
            return false;
        }

        var timeZone = request.Query["timeZone"].ToString();
        if (timeZone.Length > 100 || !TryResolveTimeZone(timeZone))
        {
            problem = ValidationProblem("timeZone", "TimeZone must be a supported IANA or system time-zone id.");
            return false;
        }

        var cursor = request.Query["cursor"].ToString();
        if (cursor.Length > 200 ||
            (cursor.Length != 0 && (!int.TryParse(cursor, out var cursorValue) || cursorValue < 0)))
        {
            problem = ValidationProblem("cursor", "Cursor is invalid.");
            return false;
        }

        var limit = options.DefaultPageSize;
        var rawLimit = request.Query["limit"].ToString();
        if (rawLimit.Length != 0 &&
            (!int.TryParse(rawLimit, NumberStyles.None, CultureInfo.InvariantCulture, out limit) ||
             limit <= 0 || limit > options.MaxPageSize))
        {
            problem = ValidationProblem(
                "limit",
                $"Limit must be between 1 and {options.MaxPageSize}.");
            return false;
        }

        query = new WtsCalendarEventQuery(
            start,
            end,
            string.IsNullOrWhiteSpace(timeZone) ? null : timeZone,
            string.IsNullOrWhiteSpace(cursor) ? null : cursor,
            limit);
        return true;
    }

    private static async Task<(WtsCalendarEvent? Event, IResult? Problem)> ReadEventAsync(
        HttpRequest request,
        WtsCalendarEndpointOptions options,
        CancellationToken cancellationToken)
    {
        if (request.ContentLength > options.MaxPayloadBytes)
        {
            return (null, Results.Problem(
                statusCode: StatusCodes.Status413PayloadTooLarge,
                title: "Calendar event payload is too large.",
                type: ProblemBase + "payload-size"));
        }

        if (request.ContentType is null ||
            !request.ContentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
        {
            return (null, Results.Problem(
                statusCode: StatusCodes.Status415UnsupportedMediaType,
                title: "Content-Type must be application/json.",
                type: ProblemBase + "content-type"));
        }

        try
        {
            var calendarEvent = await JsonSerializer.DeserializeAsync<WtsCalendarEvent>(
                request.Body,
                WtsCalendarJson.Default,
                cancellationToken).ConfigureAwait(false);
            return calendarEvent is null
                ? (null, ValidationProblem("body", "A calendar event JSON object is required."))
                : (calendarEvent, null);
        }
        catch (JsonException)
        {
            return (null, ValidationProblem("body", "The calendar event JSON is invalid."));
        }
    }

    private static bool TryReadIfMatch(
        HttpRequest request,
        out string? version,
        out IResult? problem)
    {
        version = null;
        problem = null;
        var value = request.Headers.IfMatch.ToString().Trim();
        if (value.Length == 0)
        {
            return true;
        }

        if (value == "*")
        {
            version = value;
            return true;
        }

        if (value.Contains(',', StringComparison.Ordinal) ||
            value.StartsWith("W/", StringComparison.OrdinalIgnoreCase) ||
            value.Length < 3 || value[0] != '"' || value[^1] != '"')
        {
            problem = ValidationProblem("If-Match", "If-Match must contain one strong entity tag or *.");
            return false;
        }

        version = value[1..^1];
        if (version.Length == 0 || version.Any(character => character is '"' or '\r' or '\n'))
        {
            problem = ValidationProblem("If-Match", "If-Match contains an invalid entity tag.");
            version = null;
            return false;
        }

        return true;
    }

    private static bool TryParseOffset(string? value, out DateTimeOffset parsed)
    {
        parsed = default;
        if (!WtsCalendarDateTime.TryParse(value, out var calendarValue) ||
            calendarValue.IsDateOnly)
        {
            return false;
        }

        parsed = calendarValue.AsDateTimeOffset();
        return true;
    }

    private static bool TryResolveTimeZone(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(value);
            return true;
        }
        catch (TimeZoneNotFoundException)
        {
            return false;
        }
        catch (InvalidTimeZoneException)
        {
            return false;
        }
    }

    private static bool IsValidRouteId(string value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 200 &&
        !value.Any(character => char.IsControl(character));

    private static void SetEntityTag(HttpResponse response, string version)
    {
        var safe = version.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
        response.Headers.ETag = $"\"{safe}\"";
    }

    private static IResult MutationProblem(WtsCalendarMutationResult result) =>
        result.Status switch
        {
            WtsCalendarMutationStatus.NotFound => NotFoundProblem(),
            WtsCalendarMutationStatus.Conflict => Results.Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Calendar event conflict.",
                detail: result.Message ?? "The event was modified by another request.",
                type: ProblemBase + "conflict"),
            _ => Results.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Calendar event mutation was rejected.",
                detail: result.Message,
                type: ProblemBase + "mutation"),
        };

    private static IResult NotFoundProblem() => Results.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Calendar event not found.",
        type: ProblemBase + "not-found");

    private static IResult PreconditionRequiredProblem() => Results.Problem(
        statusCode: StatusCodes.Status428PreconditionRequired,
        title: "If-Match is required for this mutation.",
        type: ProblemBase + "precondition");

    private static IResult ValidationProblem(string field, string message) =>
        Results.ValidationProblem(
            new Dictionary<string, string[]>(StringComparer.Ordinal) { [field] = [message] },
            statusCode: StatusCodes.Status400BadRequest,
            title: "Calendar request validation failed.",
            type: ProblemBase + "validation");
}
