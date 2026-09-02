# Contributing

Thank you for helping improve the WTS Calendar ASP.NET Core integration.

1. Open an issue before making a large behavioral or public-API change.
2. Keep the package framework-native and free of unnecessary runtime dependencies.
3. Run the release build and conformance executable before submitting a change.
4. Add coverage for endpoint, validation, serialization, or concurrency changes.
5. Do not commit credentials, customer data, package signing material, or license tokens.

```bash
dotnet build Wts.Calendar.AspNetCore.sln --configuration Release
dotnet run --project tests/Wts.Calendar.AspNetCore.Conformance --configuration Release
```

Contributions are licensed under the repository's MIT License.
