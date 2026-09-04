## Why

`requirements.md` point 4 needs a backend API for the portal simulation: given an origin/destination
city pair, return the carriers moving the most trucks on that lane. `requirements.md` says this data
"does not need to be stored in a database" — but a hardcoded lookup table doesn't demonstrate anything
about how the real Genlogs platform would answer this query, and the platform's own warehouse design
(`docs/architecture/platform-architecture.md`, point 3) already specifies the `CARRIER`/`LANE`/`VEHICLE`
schema and the aggregation query this feature needs. This change builds a small SQLite-backed instance of
that same schema, seeded with synthetic detection data, so the lookup endpoint runs a genuine query
instead of returning stored numbers — plus the session-auth boundary in front of it, so the frontend (a
separate, later change) has something real to call.

- Add an ASP.NET Core (.NET) Web API service backed by a SQLite database (via EF Core) modeling
  `Carrier`, `Lane`, `Vehicle`, and `DetectionEvent` — a trimmed instance of the warehouse schema from
  `platform-architecture.md`, seeded at startup with synthetic sightings.
- `POST /api/carriers/lookup` accepts `{ origin, destination }`, validates them, resolves the matching
  `Lane` (NYC↔DC, SF↔LA, or a catch-all default lane for any other pair — matching `requirements.md`
  4.2.1–4.2.3), and returns carriers ranked by a computed 7-day rolling average of distinct vehicles
  detected per day on that lane.
- Add a stateless Google Sign-In boundary: the carrier-lookup endpoint requires a valid Google-issued ID
  token as its bearer credential, verified directly against Google's public keys via ASP.NET Core's
  built-in JWT bearer authentication (`Authority = https://accounts.google.com`, `Audience` = our OAuth
  client ID) — no separate token-exchange endpoint, no credential minted or stored by this API. No user
  records are persisted, and nothing here touches the SQLite database.
- Add baseline API hardening: input validation (FluentValidation), security-headers middleware, a
  locked-down CORS policy, and rate limiting (ASP.NET Core's built-in rate-limiting middleware) on both
  endpoints.
- Add xUnit + `WebApplicationFactory` tests covering both endpoints' success, validation, and
  auth-rejection paths, plus the lane-resolution and aggregation-query logic.

## Capabilities

### New Capabilities
- `carrier-lookup`: accepts an origin/destination city pair, resolves it to a seeded lane, and returns
  carriers ranked by a computed trucks-per-day average for that lane (NYC↔DC, SF↔LA, or the default
  fallback), per `requirements.md` 4.2.
- `auth`: verifies a Google-issued ID token directly (no exchange, no minted credential) and gates the
  carrier-lookup endpoint on a valid one.

### Modified Capabilities
_None — both capabilities are new; no existing spec exists yet in `openspec/specs/`._

## Impact

- **New code**: a `Genlogs.Api` ASP.NET Core Web API project (`.csproj`, `Program.cs`,
  endpoint/service/middleware/EF Core `DbContext` classes, migrations, a `DbInitializer` seeder) plus a
  `Genlogs.Api.Tests` xUnit test project — none of this exists in the repo yet.
- **`data/carriers.mock.json`**: no longer read at runtime; kept in the repo only as the documented
  source of truth for the seed values (the exact carriers/trucks-per-day figures the seeder must
  reproduce via its averaging).
- **New dependencies** (NuGet): `Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.EntityFrameworkCore.Design`,
  `Microsoft.AspNetCore.Authentication.JwtBearer`, `FluentValidation.AspNetCore`,
  `NetEscapades.AspNetCore.SecurityHeaders`; `xunit`, `Microsoft.AspNetCore.Mvc.Testing`, `Moq` as test
  dependencies. (No `Google.Apis.Auth` or `System.IdentityModel.Tokens.Jwt` — Google-token verification
  and JWT parsing are both handled by the framework's own JWT bearer handler now.)
- **New config/env vars**: `GOOGLE_CLIENT_ID` (used as the JWT bearer `Audience` to verify ID tokens),
  `ConnectionStrings__Default` (SQLite file path), plus standard
  `ASPNETCORE_URLS`/`ASPNETCORE_ENVIRONMENT`. No `JWT_SECRET` — there's no credential for this API to
  sign, so there's nothing to key. Sourced from environment variables / user-secrets locally; no secrets
  are hardcoded or committed.
- **New storage**: a SQLite database file, created and seeded on startup — not committed to git, not
  persisted across deploys (regenerating it from the seeder on each deploy is acceptable since all of its
  data is synthetic).
- **Downstream**: the not-yet-built frontend change will call these two endpoints; no other system
  depends on this API today.
