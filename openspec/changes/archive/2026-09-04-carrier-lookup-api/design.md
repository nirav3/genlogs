## Context

See `proposal.md` - Why/What Changes. Constraints that shape this design: `requirements.md` 4 says the
data doesn't *need* a database, but the user chose to model one anyway so the lookup endpoint runs a
genuine query instead of returning stored numbers — deliberately going beyond the exercise's minimum, not
a misreading of it. `docs/architecture/platform-architecture.md` (point 3) already designed the target
warehouse schema (`CARRIER`, `LANE`, `VEHICLE`, `DETECTION_EVENT`, plus ingestion-specific tables this
exercise doesn't need); this change implements a trimmed instance of that same schema in SQLite. The
backend stack itself is **.NET / ASP.NET Core (C#)** per the user's earlier request (see `CLAUDE.md`,
`.claude/agents/backend-developer.md`) instead of the `backend-developer` agent's Node/Express default.
This is the first application code in the repo — there is no existing layering convention to match yet,
so this design establishes one. One deployment note carried from that conversation: Render has no native
.NET runtime, so shipping this means a `Dockerfile` — tracked as a `delivery-manager` concern for Day 5,
not built here.

## Goals / Non-Goals

**Goals:**
- A single small ASP.NET Core Web API exposing `POST /api/carriers/lookup`, protected by a Google-issued
  ID token used directly as the bearer credential.
- A SQLite database (via EF Core) modeling enough of the real warehouse schema (`Carrier`, `Lane`,
  `Vehicle`, `DetectionEvent`) that the lookup endpoint's answer comes from a real aggregation query over
  seeded rows, not a hardcoded table.
- Zero credential-management surface on the auth side: no minted token, no signing secret, no session
  store — the API only ever verifies a credential someone else (Google) issued.
- A layering convention (endpoints → services → EF Core) that the later frontend change and any future
  backend work can extend without restructuring.

**Non-Goals:**
- Persisting users or sessions — auth stays fully stateless and untouched by the database.
- Modeling the full nationwide pipeline (camera sites, ML extraction confidence, logo labels) —
  those tables exist in `platform-architecture.md` for the real platform but add nothing to this
  exercise's query.
- A live-updating warehouse — the database is seeded once at startup with synthetic historical data;
  there's no ingestion pipeline behind it.
- General-purpose auth (password login, refresh tokens, multi-provider, app-issued credentials) — Google
  ID-token verification only.

## Decisions

**Runtime: .NET 8 (LTS).**
Current LTS release, avoids a mid-exercise upgrade, matches what Render's Docker-based .NET hosting
expects. Alternative considered: .NET 9 (current STS) — no material upside for a short exercise, and .NET
8's tooling is more settled.

**API style: Minimal APIs, not MVC controllers.**
Two endpoints don't justify controller-class ceremony. Endpoints are grouped by feature
(`MapAuthEndpoints`, `MapCarrierEndpoints` extension methods on `WebApplication`) and delegate immediately
to a service class for the actual logic. Alternative considered: full MVC controllers — more ceremony
(attribute routing, `ControllerBase`, model binding conventions) than this surface area needs.

**Database: SQLite via EF Core, seeded at startup, not committed to git.**
SQLite needs no separate server process — it's a single file the API owns — which keeps the Starter-tier
deployment story intact (no managed-DB service, no connection-string secret beyond a file path) while
still being a real relational database with real SQL execution via EF Core's LINQ provider. The file is
created and seeded fresh every time the app starts (`Database.Migrate()` + a `DbInitializer` that inserts
rows only if the tables are empty), which is safe specifically *because* all the data is synthetic —
there's no real state to lose on a redeploy. Alternatives considered: PostgreSQL (Render-managed) —
rejected for this scope, since it adds a paid managed service and connection secrets for data that isn't
real; an EF Core in-memory provider instead of SQLite — rejected because it doesn't exercise real SQL
(defeats the purpose of moving off the static JSON lookup), and it can't be inspected with normal SQLite
tooling for debugging/demo purposes.

**Schema: `Carrier`, `Lane`, `Vehicle`, `DetectionEvent` — a trimmed instance of the
`platform-architecture.md` warehouse design.**

```
Carrier                 Lane                        Vehicle                    DetectionEvent
-----------------       ------------------------    -----------------------    -------------------
CarrierId (PK)          LaneId (PK)                 VehicleId (PK)             DetectionId (PK)
Name                    OriginCity                  PlateNumber                LaneId (FK)
UsdotNumber             DestinationCity             CarrierId (FK)             VehicleId (FK)
                        IsDefaultFallback (bool)                               CapturedAt (date)
```

- `Lane` has exactly one row with `IsDefaultFallback = true` (`OriginCity`/`DestinationCity` = `"*"`),
  which backs `requirements.md` 4.2.3 ("any other pair") as a real seeded lane rather than an in-code
  fallback branch — lane resolution is always "find the matching lane, or fall back to the default lane,"
  never a separate code path for the default case.
- `DetectionEvent` is the mock stand-in for what Stage 3 (identity resolution) would have written to the
  real warehouse — one row per truck sighting on a lane on a given day. `CAMERA_SITE`,
  `CAMERA_LANE_MAP`, `DETECTION_EXTRACTION`, and `CARRIER_LOGO_LABEL` from the full platform schema are
  intentionally omitted: they're ingestion/ML-specific and this exercise has no camera or ML step to mock.
  `UsdotNumber` on `Carrier` is carried over from the platform schema as a realism touch (echoes point 3
  without requiring a real SAFER call).

**Query: 7-day rolling average of distinct vehicles per carrier per day, per lane.**
```sql
SELECT c.Name, AVG(daily.TruckCount) AS TrucksPerDay
FROM (
    SELECT v.CarrierId, date(d.CapturedAt) AS Day, COUNT(DISTINCT d.VehicleId) AS TruckCount
    FROM DetectionEvent d
    JOIN Vehicle v ON v.VehicleId = d.VehicleId
    WHERE d.LaneId = @matchedLaneId
    GROUP BY v.CarrierId, date(d.CapturedAt)
) daily
JOIN Carrier c ON c.CarrierId = daily.CarrierId
GROUP BY c.Name
ORDER BY TrucksPerDay DESC;
```
Expressed via EF Core LINQ (`GroupBy` twice — by day then by carrier), not raw SQL, so it stays
provider-agnostic and testable against the SQLite in-memory/relational test double. Alternative
considered: a single-day snapshot (`COUNT(DISTINCT VehicleId)` for "today") — rejected per the user's
choice of a rolling average, which better matches "trucks/day" as a rate rather than a one-off count and
gives the seeded data a reason to vary day-to-day.

**Seeding calibration: deterministic ±1 daily variation around each target, over a 7-day window.**
Each carrier's daily distinct-vehicle count alternates one below and one above its target across the 7
seeded days (e.g. Knight-Swift, target 10: 9, 11, 9, 11, 9, 11, 10 → sums to 70, averages to exactly 10).
This makes the seed data look like real fluctuating activity instead of the same number repeated 7 times,
while still landing exactly on the figures `requirements.md` lists — so the demo's visible output matches
the spec precisely even though the mechanism underneath is now a genuine computed aggregate. `VehicleId`s
are reused across a carrier's own days (a fixed synthetic fleet), which is also why "distinct vehicle
count" is the right measure — a truck that runs the lane daily should still count as one truck, not one
new sighting-day each time.

**Auth: trust Google's ID token directly — no exchange endpoint, no minted credential.**
`/api/carriers/lookup` is protected by ASP.NET Core's standard JWT bearer authentication configured with
`Authority = "https://accounts.google.com"` and `Audience = GOOGLE_CLIENT_ID`. The handler fetches
Google's OIDC discovery document and public signing keys (JWKS) once, caches and periodically refreshes
them, and validates every incoming token's signature/issuer/audience/expiry locally against that cache —
there is no live network call to Google per request. The frontend sends the same Google ID token it gets
from Google Identity Services straight through as the bearer credential; this API never issues, signs, or
stores a credential of its own.

Alternative considered and reversed from an earlier version of this design: minting our own session JWT
(verify the Google token once via `Google.Apis.Auth`, then sign a short-lived `HS256` token with a
`JWT_SECRET` this API manages). That added a token-exchange endpoint, a signing secret to generate and
rotate, and a service whose only job was translating one already-valid, already-signed token into
another. Its original justification — "re-verifying against Google on every request adds latency/an
external dependency" — doesn't actually hold: both approaches validate locally against a cached JWKS, not
via a live per-request call to Google. The only genuine benefit of a custom session token is a seam for
future app-specific claims or a second login provider; neither is a real requirement here, so the extra
component isn't justified. Trusting Google's token directly removes an endpoint, a secret, and roughly a
dozen files with no loss of the behavior this exercise actually needs.

**Testing implication:** unlike verifying against a mockable interface, `Authority`-based validation has
no DI seam to substitute in a unit test, and integration tests can't depend on live network access to
Google's real JWKS endpoint (slow, flaky, offline-hostile, and a real Google-signed token can't be
fabricated without Google's private key anyway). `Program.cs` branches on
`builder.Environment.EnvironmentName == "Testing"`: in that environment only, the JWT bearer handler is
configured with a static, test-only symmetric signing key instead of `Authority`, so
`WebApplicationFactory`-based tests can mint their own validly-signed tokens locally. Production and
local development always use the real `Authority`/`Audience` path — the test-only branch never runs
outside `WebApplicationFactory("Testing")`.

**Validation: FluentValidation at the endpoint boundary.**
Declarative, unit-testable in isolation from the web host and the database.

**Security middleware stack: `NetEscapades.AspNetCore.SecurityHeaders`, scoped CORS policy, built-in rate
limiting.**
CORS is locked to an allowed-origins config value (not a wildcard), since auth relies on a bearer
credential path, not cookies. ASP.NET Core's built-in `Microsoft.AspNetCore.RateLimiting` middleware
(no extra NuGet dependency) applies to the lookup endpoint.

## Risks / Trade-offs

- **SQLite file isn't persisted across Render redeploys** → acceptable: the seeder regenerates identical
  synthetic data every startup, so there's no real data to lose. Flagged explicitly for
  `delivery-manager` rather than silently assumed.
- **Deterministic ±1 seeding pattern is still synthetic, not "real" variance** → acceptable for a demo;
  called out in code comments so it reads as an intentional simplification, not an oversight, if reviewed.
- **No token revocation** (a Google ID token is valid until its own ~1h expiry even if the user's Google
  session ends elsewhere) → same exposure window either way, and out of this API's control since it never
  issues the credential; acceptable for this exercise's scope.
- **`GOOGLE_CLIENT_ID` misconfiguration in deployment** → covered by a test asserting the app fails fast
  at startup if it's missing, rather than silently accepting a JWT bearer handler with no audience check.
- **Google's discovery/JWKS endpoint is a runtime dependency** (first token validation after startup
  needs `accounts.google.com` reachable to fetch signing keys) → no different in kind from the exchange-
  endpoint design this replaced, which had the same dependency inside `Google.Apis.Auth`; the framework
  handler caches keys afterward, so this is a one-time-per-restart concern, not a per-request one.
- **Render has no native .NET buildpack** → this change doesn't build the Dockerfile (that's
  `delivery-manager`'s Day-5 task), but the project layout (a single `Genlogs.Api.csproj`, SQLite with no
  external server dependency) is kept simple specifically so a standard
  `mcr.microsoft.com/dotnet/aspnet` multi-stage Dockerfile drops in without restructuring.

## Migration Plan

Net-new service; no existing deployment to migrate. Rollout is: implement → `dotnet test` green → manual
smoke test (curl/Postman) → hand off to `delivery-manager` for the actual deploy (tracked outside this
change, per `WEEKLY_PLAN.md` Day 5). No rollback concerns beyond redeploying the previous commit, since
the database is fully synthetic and regenerated on every startup.
