# LiveSync — FAANG-Readiness Implementation Checklist

Goal: evolve LiveSync from a working real-time collaborative editor demo into a project that
demonstrates distributed-systems judgment, correctness under failure, and production hygiene —
the signals FAANG-level interviews/reviewers actually look for.

Each section is ordered by interview signal impact (highest first). Check items off as completed.

---

## 1. Horizontal Scalability (highest impact)

Current gap: `EditorHub` state (`_documentUsers`, `_documentContent`, `_userColors`, `_documentAccess`)
is stored in `static ConcurrentDictionary` fields inside the process. This only works for a single
instance and is lost on restart.

- [x] Add `Microsoft.AspNetCore.SignalR.StackExchangeRedis` package to `LiveSync.SignalR`
- [x] Configure Redis backplane via `AddStackExchangeRedis(connectionString)` in `Program.cs`
- [x] Move `_documentUsers` (presence) into Redis (e.g., Redis Sets keyed by `doc:{id}:users`)
- [x] Move `_documentContent` (latest snapshot) into Redis (e.g., Redis String/Hash keyed by `doc:{id}:content`)
- [x] Move `_documentAccess` (per-connection access level cache) into Redis or re-validate per call
- [x] Add a local `docker-compose.yml` service for Redis so the backplane can be run and tested locally
- [ ] Verify: run two instances of `LiveSync.SignalR` on different ports, confirm edits from a client
	  connected to instance A are visible to a client connected to instance B

## 2. Real Conflict Resolution (core algorithmic signal)

Current gap: `SendContentUpdate` does last-write-wins — the whole document content is overwritten
and broadcast, so concurrent edits from different users can silently clobber each other.

- [ ] Decide on approach: Operational Transform (OT) vs CRDT (e.g., RGA/Logoot-style sequence CRDT)
- [ ] Define an `Operation` model (insert/delete with position, client revision, timestamp/vector clock)
- [ ] Implement transform/merge logic as a standalone, unit-testable class (not embedded in the Hub)
- [ ] Change `SendContentUpdate` to accept operations instead of full content, apply + rebroadcast
- [ ] Handle out-of-order delivery / reconnect resync (client requests full state + missed ops since revision N)
- [ ] Write a README section explaining the chosen algorithm and its trade-offs (this is your interview talk track)

## 3. Persistence & Durability

Current gap: in-memory document content is never flushed to Postgres; a crash/restart loses all
unsaved edits.

- [ ] Periodically snapshot live document content to `Documents` table (e.g., debounce every N seconds or N edits)
- [ ] Add a `LastSyncedRevision`/`UpdatedAt` column to track persisted state vs live state
- [ ] On `JoinDocument`, fall back to DB content if no in-memory/Redis state exists yet
- [ ] Add graceful shutdown handling (`IHostApplicationLifetime.ApplicationStopping`) to flush state before exit

## 4. Automated Testing

Current gap: no test projects exist in the solution.

- [ ] Create `LiveSync.Api.Tests` (xUnit) — unit tests for `AuthService`, `DocumentService`
- [x] Create `LiveSync.SignalR.Tests` (xUnit) — unit tests for conflict-resolution/transform logic
- [ ] Add integration tests for `EditorHub` using `Microsoft.AspNetCore.SignalR.Client` against `TestServer`/`WebApplicationFactory`
- [ ] Add integration tests for `AuthController`/`DocumentsController` using `WebApplicationFactory`
- [ ] Wire tests into CI (see section 7)

## 5. Observability

Current gap: only basic `ILogger` info-level logging; no metrics, tracing, or health endpoints.

- [ ] Add `/health` and `/health/ready` endpoints (`AddHealthChecks()`, including a Postgres check and Redis check)
- [ ] Add structured metrics (e.g., `System.Diagnostics.Metrics` + OpenTelemetry): active connections,
	  active documents, messages/sec, operation-apply latency
- [ ] Add distributed tracing (OpenTelemetry + OTLP exporter) across `LiveSync.Api` and `LiveSync.SignalR`
- [ ] Replace ad-hoc `_logger.LogInformation` calls with consistent structured log scopes (documentId, userId, connectionId)

## 6. Security & Resilience Hardening

Current gap: no rate limiting on hub methods; connection/access-token validation happens once at join.

- [ ] Add rate limiting on `SendContentUpdate` / `SendCursorPosition` per connection (token bucket)
- [ ] Re-validate access periodically (or on reconnect), not just at `JoinDocument`
- [ ] Add input validation/size limits on incoming content/operations to prevent abuse
- [ ] Add resilience policies (Polly) around `DocumentAccessClient` HTTP calls (timeout, circuit breaker)
- [ ] Confirm secrets (`Jwt:Secret`, connection strings) are never committed; use user-secrets/env vars locally

## 7. CI/CD & Deployment Hygiene

Current gap: no CI pipeline visible; migrations run inline based on config flags (already reasonably designed).

- [ ] Add GitHub Actions workflow: build, run unit + integration tests, on every PR
- [ ] Add a separate CI job/step that runs `dotnet ef migrations bundle` or applies migrations explicitly
	  (not relying on `MigrateOnStartup` for anything beyond local/dev)
- [ ] Ensure `Database:EnsureCreatedOnStartup` / `Database:MigrateOnStartup` default to `false` outside Development
- [ ] Add Dockerfile healthcheck and multi-stage build verification for `LiveSync.SignalR` (already exists for `LiveSync.Api`, confirm parity)
- [ ] Document environment variables/config required for deployment in README

## 8. Documentation & Presentation (interview packaging)

- [ ] Root `README.md`: architecture diagram (Api + SignalR + Postgres + Redis), key design decisions, trade-offs considered
- [ ] Write a short "Design Decisions" doc: why OT/CRDT choice, why Postgres, why separate Api/SignalR services, scaling story
- [ ] Add a "Known Limitations / Future Work" section — reviewers respect honesty about trade-offs over false completeness
- [ ] Record a short demo (GIF/video) showing multi-client concurrent editing and presence indicators

---

## Suggested Execution Order

1. Section 1 (Redis backplane) — unlocks real horizontal scaling story
2. Section 2 (conflict resolution) — core algorithmic depth
3. Section 4 (tests) — do this alongside 1–2, not after, so new logic is covered from the start
4. Section 3 (persistence)
5. Section 5–6 (observability, security/resilience)
6. Section 7–8 (CI/CD, documentation/presentation)
