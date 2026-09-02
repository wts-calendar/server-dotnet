using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wts.Calendar.AspNetCore;

var checks = 0;

void Check(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }

    checks++;
}

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = [],
    EnvironmentName = Environments.Development,
});
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Services.AddWtsCalendarAspNetCore(options =>
{
    options.RequireIfMatchForUpdate = true;
    options.RequireIfMatchForDelete = true;
});
builder.Services.AddWtsCalendarInMemoryStore();

await using var app = builder.Build();
app.MapWtsCalendarEvents();
await app.StartAsync();

var addresses = app.Services.GetRequiredService<IServer>()
    .Features.Get<IServerAddressesFeature>()?.Addresses;
var address = addresses?.SingleOrDefault()
    ?? throw new InvalidOperationException("The test server did not expose an address.");
using var client = new HttpClient { BaseAddress = new Uri(address) };

var createResponse = await client.PostAsJsonAsync("/api/calendar/events", new
{
    id = "roadmap-review",
    title = "Roadmap review",
    start = "2026-09-07T10:00:00Z",
    end = "2026-09-07T11:00:00Z",
    resourceId = "engineering",
    extendedProps = new { priority = "high" },
});
Check(createResponse.StatusCode == HttpStatusCode.Created, "Create must return 201.");
var firstVersion = createResponse.Headers.ETag?.Tag;
Check(!string.IsNullOrWhiteSpace(firstVersion), "Create must return an ETag.");
var created = await createResponse.Content.ReadFromJsonAsync<WtsCalendarEvent>();
Check(created?.Id == "roadmap-review", "Create must return the stored id.");

var queryResponse = await client.GetAsync(
    "/api/calendar/events?start=2026-09-01T00:00:00Z&end=2026-10-01T00:00:00Z&timeZone=UTC");
Check(queryResponse.StatusCode == HttpStatusCode.OK, "Range query must return 200.");
var page = await queryResponse.Content.ReadFromJsonAsync<WtsCalendarEventPage>();
Check(page?.Records.Count == 1, "Range query must return the created event.");

var missingPrecondition = await client.PatchAsJsonAsync(
    "/api/calendar/events/roadmap-review",
    new
    {
        title = "Updated roadmap review",
        start = "2026-09-07T10:00:00Z",
        end = "2026-09-07T11:30:00Z",
    });
Check((int)missingPrecondition.StatusCode == 428, "Strict updates must require If-Match.");

using var conflictRequest = new HttpRequestMessage(
    HttpMethod.Patch,
    "/api/calendar/events/roadmap-review")
{
    Content = JsonContent.Create(new
    {
        title = "Conflicting update",
        start = "2026-09-07T10:00:00Z",
    }),
};
conflictRequest.Headers.TryAddWithoutValidation("If-Match", "\"999\"");
var conflictResponse = await client.SendAsync(conflictRequest);
Check(conflictResponse.StatusCode == HttpStatusCode.Conflict,
    "A stale entity tag must return 409 for the WTS REST adapter.");

using var updateRequest = new HttpRequestMessage(
    HttpMethod.Patch,
    "/api/calendar/events/roadmap-review")
{
    Content = JsonContent.Create(new
    {
        title = "Updated roadmap review",
        start = "2026-09-07T10:00:00Z",
        end = "2026-09-07T11:30:00Z",
    }),
};
updateRequest.Headers.TryAddWithoutValidation("If-Match", firstVersion);
var updateResponse = await client.SendAsync(updateRequest);
Check(updateResponse.StatusCode == HttpStatusCode.OK, "Matching update must return 200.");
var secondVersion = updateResponse.Headers.ETag?.Tag;
Check(secondVersion != firstVersion, "A successful update must advance the ETag.");

var unsafeUrlResponse = await client.PostAsJsonAsync("/api/calendar/events", new
{
    title = "Unsafe link",
    start = "2026-09-08T10:00:00Z",
    url = "javascript:alert(1)",
});
Check((int)unsafeUrlResponse.StatusCode == 422, "Unsafe absolute URLs must be rejected.");

using var deleteRequest = new HttpRequestMessage(
    HttpMethod.Delete,
    "/api/calendar/events/roadmap-review");
deleteRequest.Headers.TryAddWithoutValidation("If-Match", secondVersion);
var deleteResponse = await client.SendAsync(deleteRequest);
Check(deleteResponse.StatusCode == HttpStatusCode.NoContent, "Matching delete must return 204.");

var allDayJson = JsonSerializer.Serialize(new WtsCalendarEvent
{
    Title = "All day",
    Start = WtsCalendarDateTime.FromDate(new DateOnly(2026, 9, 9)),
    End = WtsCalendarDateTime.FromDate(new DateOnly(2026, 9, 10)),
    IsAllDay = true,
});
Check(allDayJson.Contains("\"2026-09-09\"", StringComparison.Ordinal),
    "All-day values must retain yyyy-MM-dd wire format.");

await app.StopAsync();
Console.WriteLine($"Wts.Calendar.AspNetCore conformance passed ({checks} checks).");
