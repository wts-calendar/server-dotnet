using Wts.Calendar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddWtsCalendarAspNetCore(options =>
{
    options.MaxPageSize = 500;
    options.RequireIfMatchForUpdate = true;
    options.RequireIfMatchForDelete = true;
});

// Demonstration only. Register your durable IWtsCalendarEventStore in production.
builder.Services.AddWtsCalendarInMemoryStore();

var app = builder.Build();

var calendarApi = app.MapWtsCalendarEvents("/api/calendar/events");
// In a real application, apply your normal policy:
// calendarApi.RequireAuthorization("calendar-api");

app.MapGet("/", () => Results.Ok(new
{
    package = "Wts.Calendar.AspNetCore",
    events = "/api/calendar/events?start=2026-09-01T00:00:00Z&end=2026-10-01T00:00:00Z",
}));

var store = app.Services.GetRequiredService<IWtsCalendarEventStore>();
await store.CreateAsync(new WtsCalendarEvent
{
    Id = "welcome",
    Title = "WTS Calendar API is connected",
    Start = WtsCalendarDateTime.Parse("2026-09-07T10:00:00Z"),
    End = WtsCalendarDateTime.Parse("2026-09-07T11:00:00Z"),
    Color = "#147d70",
}, CancellationToken.None);

await app.RunAsync();
