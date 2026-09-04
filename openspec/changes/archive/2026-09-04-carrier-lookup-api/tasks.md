## 1. Project Scaffold

- [x] 1.1 Create the `Genlogs.Api` ASP.NET Core Web API project (`dotnet new webapi -minimal`) and a
      `Genlogs.Api.Tests` xUnit test project (`dotnet new xunit`); add both to a solution file
      (`dotnet new sln`, `dotnet sln add`). Verify with `dotnet build` succeeding for the solution.
- [x] 1.2 Add NuGet packages: `Microsoft.EntityFrameworkCore.Sqlite`, `Microsoft.EntityFrameworkCore.Design`,
      `Google.Apis.Auth`, `System.IdentityModel.Tokens.Jwt`,
      `Microsoft.AspNetCore.Authentication.JwtBearer`, `FluentValidation.AspNetCore`,
      `NetEscapades.AspNetCore.SecurityHeaders` to `Genlogs.Api`; `Microsoft.AspNetCore.Mvc.Testing`,
      `Moq` to `Genlogs.Api.Tests`. Verify with `dotnet restore` completing cleanly.
- [x] 1.3 Create `Endpoints/`, `Services/`, `Models/`, `Data/`, `Middleware/` folders per `design.md`
      layout. Verify the app boots (`dotnet run --project Genlogs.Api`) and responds on a basic health
      path without errors.
- [x] 1.4 Add configuration for `GOOGLE_CLIENT_ID`, `JWT_SECRET`, `ConnectionStrings__Default`,
      `ALLOWED_ORIGIN` via `appsettings.json` + environment-variable overrides (and document local dev via
      `dotnet user-secrets`). Verify the app throws a clear startup error when `GOOGLE_CLIENT_ID` or
      `JWT_SECRET` is missing.

## 2. Database Schema & Seeding

- [x] 2.1 Define EF Core entities `Carrier`, `Lane`, `Vehicle`, `DetectionEvent` (per `design.md`'s
      schema) and a `GenlogsDbContext`. Verify with `dotnet ef migrations add InitialCreate` generating a
      migration without errors.
- [x] 2.2 Wire `Database.Migrate()` at startup against the SQLite connection string. Verify the SQLite
      file is created on first run and the expected tables exist (inspect via `dotnet ef dbcontext info`
      or a quick `sqlite3 .tables` check).
- [x] 2.3 Implement a `DbInitializer` seeder that inserts: the NYC→DC lane's 3 carriers, the SF→LA lane's
      3 carriers, and the default-fallback lane's 2 carriers (UPS/FedEx), each with a synthetic vehicle
      fleet and 7 days of `DetectionEvent` rows following the deterministic ±1 pattern from `design.md`
      calibrated to the exact `requirements.md` figures. Guard it to run only when tables are empty.
      Verify with a unit test asserting each carrier's seeded daily counts average to its documented
      trucks-per-day figure exactly.
- [x] 2.4 Seed the default-fallback `Lane` row (`IsDefaultFallback = true`) alongside the two named
      lanes. Verify with a test querying for a lane with no seeded exact match and confirming the
      default lane is returned.

## 3. Carrier Lookup Service

- [x] 3.1 Implement a lane-resolution service that normalizes incoming origin/destination (case,
      whitespace, common aliases like "NYC") and queries for a matching `Lane`, falling back to the
      default-fallback lane otherwise. Verify with xUnit tests covering all scenarios in
      `specs/carrier-lookup/spec.md` (both known lanes, unmatched pair, alias/whitespace variants).
- [x] 3.2 Implement the trucks-per-day aggregation query (EF Core LINQ: group `DetectionEvent` by
      carrier and day, count distinct vehicles, then average across the lookback window) against the
      resolved `LaneId`, returning carriers ordered by the computed average descending. Verify with a
      test seeding known detection rows and asserting the computed average matches a hand-calculated
      expected value.
- [x] 3.3 Add a FluentValidation validator for `{ origin: string, destination: string }` (both required,
      non-empty). Verify with unit tests for missing fields, empty strings, and wrong types.
- [x] 3.4 Wire `POST /api/carriers/lookup` (minimal-API endpoint → lane resolution → aggregation query),
      returning the ranked carrier list as JSON on success and a `ProblemDetails`-shaped validation error
      (4xx) on invalid input. Verify with `WebApplicationFactory`-based tests covering success (all 3
      cases, using the seeded database) and validation-error paths.

## 4. Google Sign-In Trust (Direct ID Token Verification)

Supersedes the original verify-and-mint approach: no exchange endpoint, no signing secret, no session
token — the API verifies a Google ID token directly on every protected request (see `design.md`'s
"Auth: trust Google's ID token directly" decision).

- [x] 4.1 Remove the session-minting layer entirely: `Endpoints/AuthEndpoints.cs`,
      `Services/SessionTokenService.cs` + `ISessionTokenService.cs`, `Services/GoogleIdTokenValidator.cs`
      + `IGoogleIdTokenValidator.cs`, `Services/GoogleJsonWebSignatureVerifier.cs` +
      `IGoogleJsonWebSignatureVerifier.cs`, `Services/GoogleUserInfo.cs`,
      `Services/AuthenticationFailedException.cs`, `Models/Dtos/GoogleAuthRequest.cs` +
      `GoogleAuthResponse.cs`, `Validation/GoogleAuthRequestValidator.cs`, and their corresponding tests.
      Remove the `Google.Apis.Auth` and `System.IdentityModel.Tokens.Jwt` package references and the
      `Auth` rate-limiter policy. Verify with `dotnet build` succeeding with no dangling references.
- [x] 4.2 Configure JWT bearer authentication in `Program.cs` with `Authority =
      "https://accounts.google.com"` and `Audience = GOOGLE_CLIENT_ID` for all non-`Testing` environments.
      Verify by confirming the app boots and the discovery document is fetched (check startup logs) with
      a real `GOOGLE_CLIENT_ID` configured.
- [x] 4.3 Add a `Testing`-environment branch (`builder.Environment.EnvironmentName == "Testing"`) that
      configures the JWT bearer handler with a static, test-only symmetric signing key instead of
      `Authority`, and a `TestSupport` helper that mints tokens signed with that same key. Verify with a
      test confirming a token signed by the test helper is accepted and one signed with a different key
      is rejected.
- [x] 4.4 Update `Startup/ConfigurationGuard.cs` to only require `GOOGLE_CLIENT_ID` (drop the
      `JWT_SECRET` check). Verify with a test confirming startup still fails fast when
      `GOOGLE_CLIENT_ID` is missing.
- [x] 4.5 Apply `.RequireAuthorization()` to `POST /api/carriers/lookup` against the reconfigured
      authentication scheme. Verify with `WebApplicationFactory` tests (using the `Testing`-environment
      signing key) confirming the endpoint 401s with no token, 401s with a token signed by a different
      key or expired, and succeeds with a validly-signed test token.
- [x] 4.6 Remove `JWT_SECRET` from `appsettings.json`/`appsettings.Development.json` and update
      `backend/README.md`'s local-dev instructions to drop the `dotnet user-secrets set "JWT_SECRET"`
      step.

## 5. Security Hardening

- [x] 5.1 Add `NetEscapades.AspNetCore.SecurityHeaders` and a CORS policy restricted to `ALLOWED_ORIGIN`
      (no wildcard). Verify by asserting security headers are present and a disallowed origin is rejected
      in an integration test.
- [x] 5.2 Add ASP.NET Core rate-limiting middleware on `POST /api/carriers/lookup` (drop the now-unused
      `Auth` limiter policy along with the removed endpoint). Verify with a test that exceeds the
      configured limit and asserts a 429 response.
- [x] 5.3 Add centralized exception-handling middleware (`UseExceptionHandler`/`IExceptionHandler`) that
      returns consistent `ProblemDetails` JSON and never leaks stack traces or secrets. Verify with a
      test triggering an unexpected error and asserting the response body contains no stack trace.

## 6. Verification & Wrap-up

- [x] 6.1 Run the full xUnit suite (`dotnet test`) and confirm all specs in
      `specs/carrier-lookup/spec.md` and `specs/auth/spec.md` are covered and passing, including the
      seeded-average-matches-documented-figure assertions and the direct-Google-token auth paths.
- [x] 6.2 Manually smoke-test the lookup endpoint end-to-end with curl/Postman (a real Google ID token
      obtained via the OAuth playground or a browser sign-in, since there's no longer an exchange step to
      fake) for all 3 lookup cases plus the missing/invalid-token reject paths; capture the
      commands/results in the PR or commit description.
