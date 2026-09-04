# Genlogs Portal Simulation - QA Test Report

Date: 2026-09-04
Tester: qa-integration-tester subagent
Scope: backend/Genlogs.Api (.NET 8 / ASP.NET Core, SQLite) + frontend/ (vanilla JS SPA), endpoint POST /api/carriers/lookup.

## Overall summary

The backend is solid: all 72 xUnit tests pass, and live testing of the running API (auth rejection paths, all
three carrier-data lanes, bidirectional lane matching, city-name normalization including Google Places'
"City, ST, USA" format, input validation, CORS allow/deny, rate limiting, malformed JSON, oversized input,
non-POST methods, and the global exception handler) all behaved exactly as documented in smoke-test.html,
requirements.md, and the code's own comments -- no backend bugs were found. The frontend's Jest suite also
passes in full (42/42), and static/code review of frontend/src/*.js found no wiring defects. However, true
end-to-end browser testing of the live UI was not possible in this session because no browser-automation tool
was available to this agent -- only filesystem and shell tools were provided, despite the task instructions
describing a browser-driven workflow. All UI claims below are therefore based on static code review and the
passing Jest suite, not on an actually-rendered, clicked-through page. Real interactive Google OAuth sign-in
was separately expected to be infeasible per the task brief and was not attempted for the same reason plus the
missing browser tool.

## Automated test suites

| Suite | Command | Result |
|---|---|---|
| Backend unit/integration (xUnit) | dotnet test Genlogs.sln (from backend/) | Pass - 72/72, 0 failed, 0 skipped, ~3s |
| Frontend unit (Jest + jsdom) | npm test (from frontend/) | Pass - 42/42 across 6 suites, ~13s |

Backend test files: CarrierEndpointsTests.cs, SecurityHardeningTests.cs, CarrierLookupServiceTests.cs,
CityNormalizerTests.cs, LaneResolutionServiceTests.cs, ConfigurationGuardTests.cs,
LookupRequestValidatorTests.cs, DbInitializerTests.cs.
Frontend test files: app.test.js, auth.test.js, apiClient.test.js, carrierList.test.js, mapRoutes.test.js,
places.test.js.

Note: dotnet test initially failed with MSB3027/MSB3026 (file lock on Genlogs.Api.exe) because a stale
Genlogs.Api.exe process (PID 20500) was already running from a previous session and holding the binary open.
Killing that process resolved it. Not a code bug, but worth flagging: nothing in the repo currently guards
against or documents this failure mode for local dev.

## Live API testing

Two backend instances were run directly (not through the UI, since no browser tool was available):
- Port 5136, ASPNETCORE_ENVIRONMENT=Development, GOOGLE_CLIENT_ID/ALLOWED_ORIGIN from appsettings.json
  (real Google-Authority JWT validation) - used for auth-rejection and CORS tests.
- Port 5137, ASPNETCORE_ENVIRONMENT=Testing (the app's own test-only symmetric-key JWT validation path,
  TestAuthConstants/TestJwtTokenFactory, mirrored with a small local Node script to mint HS256 tokens signed
  with the same static test key) - used to reach the authenticated code path and exercise all three carrier
  lanes plus validation/edge cases without needing a real interactive Google sign-in.

### Test matrix

| # | Category | Test | Result |
|---|---|---|---|
| 1 | Unit | Backend xUnit suite (dotnet test) | Pass (72/72) |
| 2 | Unit | Frontend Jest suite (npm test) | Pass (42/42) |
| 3 | Unit (static) | node --check on all frontend/src/*.js (syntax validity) | Pass - no syntax errors |
| 4 | API/Integration | POST /api/carriers/lookup, NYC to Washington DC returns Knight-Swift/J.B. Hunt/YRC Worldwide with correct truck counts | Pass |
| 5 | API/Integration | POST /api/carriers/lookup, San Francisco to Los Angeles returns XPO/Schneider/Landstar with correct truck counts | Pass |
| 6 | API/Integration | POST /api/carriers/lookup, unrelated pair (Chicago to Miami) returns UPS Inc./FedEx Corp fallback | Pass |
| 7 | API/Integration | Reversed direction (Washington DC to New York City) still resolves the NYC/DC lane | Pass (bidirectional match is intentional per code comment in LaneResolutionService.cs) |
| 8 | API/Integration | Mismatched pair from the two named lanes (New York City to Los Angeles) correctly falls back to UPS/FedEx rather than partial-matching either named lane | Pass |
| 9 | API/Integration | Same city in both fields (New York City to New York City) falls back to UPS/FedEx (not treated as the NYC lane) | Pass |
| 10 | API/Integration | City-name normalization: mixed case + extra whitespace + comma resolves NYC lane | Pass |
| 11 | API/Integration | City-name normalization: Google Places formattedAddress shape ("New York, NY, USA", "Washington, DC, USA") resolves correctly | Pass |
| 12 | Auth and Security | Request with no Authorization header returns 401, WWW-Authenticate: Bearer | Pass |
| 13 | Auth and Security | Request with a garbage/non-JWT bearer token returns 401 | Pass |
| 14 | Auth and Security | Request with a well-formed but expired JWT (test signing key) returns 401 with error_description indicating expiry | Pass |
| 15 | Auth and Security | Request with a JWT signed with the wrong key returns 401, error=invalid_token, error_description=The signature key was not found | Pass |
| 16 | Auth and Security | Auth is checked before body validation - an unauthenticated request with an empty body still returns 401, not 400 | Pass (correct precedence) |
| 17 | Auth and Security | CORS preflight (OPTIONS) from the allowed origin (http://127.0.0.1:5500) returns Access-Control-Allow-Origin/-Methods/-Headers | Pass |
| 18 | Auth and Security | CORS preflight from a disallowed origin (http://evil.example.com) returns 204 with no Access-Control-Allow-* headers | Pass |
| 19 | Auth and Security | Actual (non-preflight) request from a disallowed origin still gets no CORS headers back (browser would block reading it) | Pass |
| 20 | Auth and Security | Security response headers present on every response (X-Frame-Options: DENY, X-Content-Type-Options: nosniff, Referrer-Policy, Content-Security-Policy, Cross-Origin-*) | Pass |
| 21 | Auth and Security | Swagger UI reachable at /swagger only when ASPNETCORE_ENVIRONMENT=Development (200), and returns 404 under Testing env | Pass |
| 22 | Auth and Security | Rate limiting: fixed window of 30 requests/minute on the lookup endpoint eventually returns 429 once the window's budget is exhausted | Pass - 429s began appearing once the running total of endpoint-reaching requests in the 1-minute window hit 30 (confirmed by counting prior requests in the same window); 429 response carries no Retry-After header (minor, see Bugs) |
| 23 | Functional/Validation | Empty-string origin/destination (with valid auth) returns 400 ProblemDetails, Origin/Destination "is required" | Pass |
| 24 | Functional/Validation | Missing origin/destination fields entirely (with valid auth) returns same 400 ProblemDetails | Pass |
| 25 | Functional/Validation | Whitespace-only origin/destination returns same 400 "is required" (not silently treated as empty then fallback) | Pass |
| 26 | Functional/Reliability | Malformed (non-JSON) request body returns 400, empty body, no stack trace or internal detail leaked | Pass |
| 27 | Functional/Reliability | GET instead of POST on /api/carriers/lookup returns 405 Method Not Allowed | Pass |
| 28 | Functional/Reliability | Very large origin string (10,000 chars) handled without error, falls back to default lane, no crash | Pass |
| 29 | Functional/Reliability | Non-ASCII / CJK city names handled without error, falls back to default lane, no crash | Pass |
| 30 | Performance | Median latency for POST /api/carriers/lookup (authenticated, in-process SQLite) | ~2.1-2.7ms per request across 5 samples - well under any reasonable threshold |
| 31 | Performance | GET /health latency | ~1.3ms |
| 32 | Reliability | Backend server logs during entire test session reviewed for unhandled exceptions | Pass - only a benign "Failed to determine the https port for redirect" warning from UseHttpsRedirection() in plain-HTTP local dev, no exceptions or errors |
| 33 | UI/Functional | Frontend static asset syntax/structure review (index.html data-role wiring vs. app.js querySelector calls) | Pass (by inspection) - every data-role selector referenced in app.js/carrierList.js/mapRoutes.js has a matching element in index.html |
| 34 | UI/Functional | carrierList.js renders carrier name/trucks-per-day via textContent (not innerHTML) | Pass (by inspection) - no XSS/HTML-injection risk from carrier data |
| 35 | End-to-End (browser) | Load the UI, exercise origin/destination picker, click Search, verify map and carrier list render, check console/network | Blocked - no browser-automation tool was available in this session (see below) |
| 36 | End-to-End (browser) | Real interactive Google OAuth sign-in via the rendered Google button | Blocked - same tooling gap, and separately expected to be infeasible for a headless/scripted agent per the task brief |

## Bugs found

None in application/backend logic. Two minor observations, not blocking:

1. Minor - No Retry-After header on 429 responses. POST /api/carriers/lookup returns 429 Too Many
   Requests with an empty body and no Retry-After header once the 30-req/min window is exhausted. Not wrong,
   but a well-behaved rate limiter typically tells the client when it may retry so the frontend could implement
   a backoff/countdown instead of a generic "please wait a moment" message.
   Repro: send 30+ authenticated POST /api/carriers/lookup requests within 60 seconds; inspect headers on the
   response once it starts returning 429.
   Location: backend/Genlogs.Api/Program.cs (AddRateLimiter/AddFixedWindowLimiter config).

2. Minor/DX - Stale process can silently break local dotnet test/dotnet run. If a previous
   Genlogs.Api.exe is left running (e.g., from a prior dotnet run that wasn't stopped), the next
   dotnet test/dotnet build fails with MSB3027: Could not copy ... Genlogs.Api.exe rather than a clear
   "stop the running server first" message. Encountered directly in this session (PID 20500 had to be killed
   manually before dotnet test would run). Not a functional bug, but worth a one-line note in
   backend/README.md.

## Not tested / blocked

- Full browser-driven end-to-end UI testing (page load, console/network inspection, clicking through
  origin/destination autocomplete, Search button, verifying the embedded Google Map and route list render, and
  the resulting carrier list appears) - blocked: this agent's toolset in this session was limited to
  Read/Glob/Grep/Bash; no preview_start/navigate/computer/read_console_messages/
  read_network_requests/javascript_tool/resize_window-style browser tools were exposed, even though the
  task brief described driving the UI live via "Claude Browser tools." As a substitute, the frontend's own Jest
  suite (42/42 passing, covering app.js init/search-flow logic, auth.js, apiClient.js, carrierList.js,
  mapRoutes.js, places.js with mocked fetch/Google globals) was run, and every frontend source file was
  read and cross-checked against index.html's DOM structure and against the backend contract it calls. This is
  a reasonable substitute for logic correctness but is not a substitute for confirming the page actually
  renders, that Google Maps/Places/Identity Services scripts load correctly in a real browser, that the CSS
  produces a usable layout, or that there are no runtime console errors on initial load - none of that was
  observed directly.
- Real interactive Google OAuth sign-in - not attempted, for two reasons: (1) it requires picking a real
  Google account through a consent UI, which per the task brief is very unlikely to be drivable by an agent even
  with browser tools, and (2) no browser tool was available at all in this session. As a substitute, the API's
  auth-rejection paths (missing token, garbage token, expired token, wrong-signing-key token) were fully
  exercised directly against the running backend, and the three successful-lookup lanes were exercised using a
  locally-minted JWT signed with the app's own Testing-environment signing key (TestAuthConstants) - the same
  mechanism the backend's own xUnit tests use (TestJwtTokenFactory) - against a second backend instance
  started with ASPNETCORE_ENVIRONMENT=Testing. This validates the entire request/response contract and all
  three carrier-data cases end-to-end at the API layer; it does not validate the frontend's actual Google
  Identity Services button rendering or token handoff in a live page.
- Visual/responsive/accessibility checks (resize_window across breakpoints, actual keyboard-navigation
  walkthrough in a live DOM, screen-reader semantics beyond static ARIA-attribute inspection) - not performed,
  same tooling gap. index.html was reviewed statically and has aria-live, role="status"/role="button",
  and labeled form controls throughout, which is a good sign, but this was not confirmed interactively.
- Deployed/production environment - everything above was tested against local dev instances
  (localhost:5136/:5137 for the API; a frontend origin matching the documented http://127.0.0.1:5500 setup
  could not be established in this session, even setting the browser-tool gap aside, because port 5500 was
  already bound by another local process - a VS Code Live Server instance serving the whole repo root, not just
  frontend/). No deployment exists yet per CLAUDE.md.
