# Wts.Calendar.AspNetCore

[![NuGet](https://img.shields.io/nuget/v/Wts.Calendar.AspNetCore?label=stable)](https://www.nuget.org/packages/Wts.Calendar.AspNetCore)
[![Downloads](https://img.shields.io/nuget/dt/Wts.Calendar.AspNetCore)](https://www.nuget.org/packages/Wts.Calendar.AspNetCore)
[![License](https://img.shields.io/github/license/wts-calendar/server-dotnet)](LICENSE)

Official ASP.NET Core server integration for `@wts-calendar/core`. The package
provides a typed event wire contract, Minimal API endpoints, validation,
optimistic concurrency, and a replaceable storage boundary. It does not require
a WTS-hosted backend and it does not control your authentication or database.

## Install

```bash
dotnet add package Wts.Calendar.AspNetCore --version 1.0.0
```

The package targets `net8.0` and `net10.0` and has no third-party runtime
dependencies. It uses the ASP.NET Core shared framework.

| Component | Supported |
| --- | --- |
| .NET | 8 and 10 |
| ASP.NET Core | Minimal APIs and standard endpoint middleware |
| Browser clients | Angular, React, Vue, Web Components |
| Native client | React Native through the same JSON contract |

## Configure the server

```csharp
using Wts.Calendar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddWtsCalendarAspNetCore(options =>
{
    options.MaxPageSize = 500;
    options.RequireIfMatchForUpdate = true;
    options.RequireIfMatchForDelete = true;
});

builder.Services.AddSingleton<IWtsCalendarEventStore, SqlCalendarEventStore>();

var app = builder.Build();

var calendarApi = app.MapWtsCalendarEvents("/api/calendar/events");
calendarApi.RequireAuthorization("calendar-api");

app.Run();
```

`SqlCalendarEventStore` represents your implementation of
`IWtsCalendarEventStore`. It can use EF Core, Dapper, MongoDB, a remote service,
or the persistence layer your application already has.

For a local demo or a test, explicitly opt into process-local storage:

```csharp
builder.Services.AddWtsCalendarInMemoryStore();
```

The in-memory store is thread-safe, but it is not durable and does not coordinate
multiple server instances. Do not use it as production persistence.

## Connect `@wts-calendar/core`

```ts
import {
  CalendarDataClient,
  createRestCalendarDataAdapter,
} from '@wts-calendar/core/data-adapter-sdk';

const endpoint = 'https://api.example.com/api/calendar/events';

const adapter = createRestCalendarDataAdapter({
  url: endpoint,
  mutationUrl: ({ type, id }) =>
    type === 'create' ? endpoint : `${endpoint}/${encodeURIComponent(id ?? '')}`,
  headers: async () => ({
    authorization: `Bearer ${await accessToken()}`,
  }),
});

const events = new CalendarDataClient(adapter);
```

The adapter sends visible ranges as offset-aware ISO 8601 values. All-day event
fields use `yyyy-MM-dd`; timed events require `Z` or an explicit UTC offset.
Mutation versions are sent through `If-Match` when supplied.

## HTTP contract

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/api/calendar/events?start=...&end=...&timeZone=...` | Load a bounded visible range |
| `GET` | `/api/calendar/events/{id}` | Load one event and its `ETag` |
| `POST` | `/api/calendar/events` | Create an event; returns `201`, `Location`, and `ETag` |
| `PATCH` or `PUT` | `/api/calendar/events/{id}` | Replace the current event representation |
| `DELETE` | `/api/calendar/events/{id}` | Delete an event |

Validation failures use RFC 7807-compatible validation problems. Stale
versions return `409 Conflict`, which maps directly to the WTS data adapter's
`conflict` mutation result. Set `RequireIfMatchForUpdate` and
`RequireIfMatchForDelete` when every editing client retains entity tags.

## Production responsibilities

The package deliberately leaves these application-level concerns to the host:

- authentication and per-calendar/per-event authorization;
- durable persistence, tenant isolation, and database migrations;
- CORS origins, rate limits, request-body limits, and observability;
- provider secrets and Google/Microsoft/CalDAV token storage;
- conflict policy beyond the supplied entity-tag precondition.

Apply authorization to the returned `RouteGroupBuilder`. Configure CORS with an
explicit origin list; do not combine credentialed requests with wildcard origins.
For chunked requests, also configure the server's normal maximum request body
size—the package's `MaxPayloadBytes` check applies when `Content-Length` is known.

## Build and verify this repository

```bash
dotnet build Wts.Calendar.AspNetCore.sln --configuration Release
dotnet run --project tests/Wts.Calendar.AspNetCore.Conformance \
  --configuration Release
dotnet pack src/Wts.Calendar.AspNetCore --configuration Release \
  --output artifacts
```

The conformance executable starts a real loopback ASP.NET Core server and checks
create, range loading, strict preconditions, conflict mapping, update, validation,
delete, ETags, and all-day serialization without adding a test-framework runtime
dependency.

## Project links

- [Homepage and documentation](https://wts-calendar.github.io/docs)
- [NuGet package](https://www.nuget.org/packages/Wts.Calendar.AspNetCore)
- [Source repository](https://github.com/wts-calendar/server-dotnet)
- [Browser calendar package](https://www.npmjs.com/package/@wts-calendar/core)
- [Issue tracker](https://github.com/wts-calendar/server-dotnet/issues)
- [Security policy](SECURITY.md)
- [Changelog](CHANGELOG.md)

## License

MIT © Suman Mandal.
