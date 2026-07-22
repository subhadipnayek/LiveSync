# PostgreSQL setup and migration

The API now uses PostgreSQL through Npgsql. The committed EF migration is a new
PostgreSQL baseline; the removed migrations contained SQL Server-specific types
and cannot be executed against PostgreSQL.

## Local setup

1. Start your shared PostgreSQL service (`postgres-dev`) with:
   - `POSTGRES_USER=devuser`
   - `POSTGRES_PASSWORD=devpassword`
   - `POSTGRES_DB=postgres`
2. Restore the pinned EF tool: `dotnet tool restore --tool-manifest backend/dotnet-tools.json`
3. Start `LiveSync.Api`.

The development connection targets database `livesync`, user `devuser`, and
password `devpassword`. On startup, the API connects to the shared maintenance
database `postgres`, creates `livesync` if it does not already exist, and then
applies all EF migrations to `livesync`.

If the API runs inside Docker instead of directly on the host, override the host
name in the connection string from `localhost` to the compose service name:
`Host=postgres-dev;Port=5432;Database=livesync;Username=devuser;Password=devpassword`.

## Production configuration

Set these environment variables for both backend services:

- `ConnectionStrings__DefaultConnection` (API only), for example
  `Host=db;Port=5432;Database=livesync;Username=livesync;Password=...`
- `Database__MaintenanceDatabase` (API only), usually `postgres`
- `Jwt__Secret` with the same strong secret on the API and SignalR service
- `Cors__AllowedOrigins__0` to the public frontend origin
- `Services__ApiBaseUrl` on SignalR to the internally reachable API URL

Run `dotnet ef database update` once as a release step. Startup migrations are
enabled only in Development to avoid races between production instances.

## Existing SQL Server data

The schema migration does not copy existing rows. Export and import the Identity,
Documents, and SharedDocuments data before switching production traffic, then
reset the PostgreSQL migration history to the committed `InitialPostgreSql`
baseline. Keep string IDs unchanged so ownership and sharing foreign keys remain
valid. Take and verify a SQL Server backup before the cutover.

The frontend reads optional deployment-time endpoints from `public/config.js`.
Leave them empty when `/api` and `/hubs` are reverse-proxied on the same origin,
or set `apiBaseUrl` and `signalRBaseUrl` before serving the built assets.
