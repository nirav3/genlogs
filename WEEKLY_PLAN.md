# Genlogs Portal — Build Plan (Aug 31 – Sep 4, 2026)

2 hours/day, .NET/ASP.NET Core backend first, vanilla-JS frontend second, deploy on the last day.
Maps to `requirements.md` item 4. This table also answers item 1.2/1.3 (time estimate + delivery date)
if you want to forward it to the hiring manager.

| Day | Date | Hours | Focus | Tasks | Agent(s) |
|---|---|---|---|---|---|
| 1 | Mon Aug 31 | 2h | Backend kickoff | `/opsx:propose carrier-lookup-api` (proposal + delta spec + tasks). Scaffold an ASP.NET Core Web API project. Implement the lookup endpoint against `data/carriers.mock.json` for the 3 cases (NYC↔DC, SF↔LA, default). Manual smoke test via curl/Postman. | backend-developer |
| 2 | Tue Sep 1 | 2h | Backend security + auth + tests | Input validation (FluentValidation) on from/to. Security-headers middleware, locked-down CORS, ASP.NET Core rate limiting. **Google Sign-In (OAuth):** `POST /api/auth/google` verifies the ID token from the frontend via `Google.Apis.Auth`, then issues a short-lived signed session JWT (no DB — stateless). Auth middleware requires a valid session JWT on the carrier-lookup endpoint. xUnit + `WebApplicationFactory` tests for auth verify/reject paths + carrier success/error paths. `dotnet test` green. `/opsx:archive` the backend change. | backend-developer |
| 3 | Wed Sep 2 | 2h | Frontend kickoff + login | `/opsx:propose frontend-portal`. Scaffold single HTML page + small JS modules (auth, form, api client, rendering). Add "Sign in with Google" button (Google Identity Services JS lib) that gets an ID token and exchanges it with the backend for a session JWT; gate the search form on being signed in. From/To fields wired to Google Places Autocomplete. Search button → calls the backend API with the JWT attached, loading/error states. Jest+jsdom tests for auth + form logic. | frontend-developer |
| 4 | Thu Sep 3 | 2h | Map + carrier list + polish | Embed Google Map; use Directions Service (`alternatives: true`) to render up to 3 routes. Render carrier list from the API response. Handle empty/error/edge-case/signed-out UI states. Finish Jest+jsdom tests. `/opsx:apply` + `/opsx:archive` the frontend change. | frontend-developer |
| 5 | Fri Sep 4 | 2h | QA + Deploy | Full-stack smoke test: sign-in flow, all 3 route cases, edge cases, signed-out/rejected-token cases (~40 min). Fix anything blocking (~20 min budget). Deploy to **Render** (Docker-based .NET web service for the API — Render has no native .NET runtime, so this needs a `Dockerfile` — + static site for the frontend). Wire env vars (Google Maps key, Google OAuth client ID/secret, JWT signing secret). Verify the live URL end-to-end, update README with it. | qa-integration-tester → delivery-manager |

**Scope note:** real user auth (Google Sign-In) isn't in `requirements.md` item 4.1 — it's an addition on top of
the exercise spec. Implementing it as Google Identity Services (frontend gets an ID token via popup, backend
verifies it and mints its own short-lived JWT) keeps it DB-free and reuses the same Google Cloud project you
already need for the Maps API key — no second OAuth provider account required.

**Risk:** adding OAuth makes Day 2 and Day 3 the tightest windows now (on top of Day 1/Day 4, which were already
tight). If a day runs long, the safest place to absorb it is Day 5's QA pass (30 min instead of 40) — don't cut
corners on the deploy step or on Day 2's token-verification tests, since that's the actual security boundary.
