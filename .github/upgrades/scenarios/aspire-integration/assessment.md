# Aspire Integration Assessment

## Compatible Projects (net8.0+)
- `backend/LiveSync.Api/LiveSync.Api.csproj` — `net10.0`
- `backend/LiveSync.SignalR/LiveSync.SignalR.csproj` — `net10.0`
- `backend/LiveSync.SignalR.Tests/LiveSync.SignalR.Tests.csproj` — `net10.0`

## Incompatible Projects
- None

## Inter-service communication graph

Direct calls:
- `backend/LiveSync.SignalR/LiveSync.SignalR.csproj` --HTTP--> `backend/LiveSync.Api/LiveSync.Api.csproj` via `Services:ApiBaseUrl` (`https://localhost:7001`) used by `AddHttpClient<DocumentAccessClient>`.

Shared resources:
- PostgreSQL (`ConnectionStrings:DefaultConnection`) used by `LiveSync.Api`.
- Redis (`Redis:ConnectionString`) used by `LiveSync.SignalR`.

## Notes for aspireify delegation
- Prioritize service reference wiring from SignalR to API (`WithReference` / startup ordering).
- Keep `LiveSync.SignalR.Tests` out of AppHost runnable resources unless explicitly requested as a test utility component.
