## Why

`requirements.md` point 4.1 needs a browser front end for the portal simulation: a single page where a
user picks an origin/destination city, sees the fastest 3 routes on an embedded Google Map, and gets back
the ranked carrier list from the already-built `carrier-lookup` API. That API is live and requires a
Google ID token as its bearer credential (`auth` capability) — until now nothing produces one or calls the
endpoint outside the manual `backend/smoke-test.html` harness. This change is the "separate, later change"
the archived `carrier-lookup-api` proposal explicitly deferred.

## What Changes

- Add a single static HTML page (`frontend/index.html`) plus small, focused vanilla-JS ES modules — no
  framework, no bundler: auth (Google Identity Services sign-in, ID-token lifecycle), places/autocomplete
  (From/To city fields), an API client (calls `POST /api/carriers/lookup` with the ID token as a bearer
  credential), map/routes rendering (embedded Google Map, up to 3 route alternatives via Directions), and
  carrier-list rendering, wired together by a thin app-init module.
- Add a "Sign in with Google" control that obtains a Google ID token via Google Identity Services and
  holds it in memory for the session. The search form/button is disabled until signed in. **The ID token
  is sent directly as the API's bearer credential — there is no token-exchange or session endpoint**,
  matching the `auth` capability as actually built (the earlier `WEEKLY_PLAN.md` sketch of a session-JWT
  exchange was superseded during backend implementation and is not carried forward here).
- Wire the From/To fields to the current (non-deprecated) Google Places Autocomplete API. Once both
  resolve to a place and the user is signed in, "Search" becomes enabled.
- On "Search": call the carrier-lookup endpoint with `{ origin, destination }` and the bearer token, and
  in parallel render an embedded Google Map showing up to the 3 fastest routes between the two places
  (Directions with route alternatives).
- Render the returned carrier list (`{ carriers: [{ name, trucksPerDay }] }`) in the order the API returns
  it (already ranked descending — no client-side re-sort).
- Handle and visibly distinguish: initial/signed-out, loading, empty-result, and error states (400
  validation, 401 auth — prompt re-sign-in, 429 rate-limited, 5xx/network failure) without leaking raw
  API error bodies into the UI.
- Add Jest + jsdom unit tests for every module, written immediately after that module's implementation,
  mocking `fetch` and the Google Identity Services / Maps JS globals.

## Capabilities

### New Capabilities
- `portal-ui`: the single-page front end — sign-in gating, city input with map-provider matching, search
  submission, embedded map with up to 3 route alternatives, and carrier-list rendering, per
  `requirements.md` 4.1.

### Modified Capabilities
_None — `carrier-lookup` and `auth` are consumed exactly as already specified in
`openspec/specs/carrier-lookup/spec.md` and `openspec/specs/auth/spec.md`; no requirement changes to
either._

## Impact

- **New code**: a `frontend/` directory (`index.html`, `src/` ES modules for auth, autocomplete, api
  client, map/routes, carrier list, and app wiring; a `package.json` for Jest + jsdom dev dependencies;
  `src/**/__tests__` or colocated `*.test.js` files) — none of this exists in the repo yet.
- **New dependencies** (npm, dev-only): `jest`, `jest-environment-jsdom`. Runtime dependencies are the
  externally loaded Google Identity Services script and Google Maps JavaScript API (Places + Directions
  libraries) — no npm runtime deps, no bundler.
- **New config/env**: `GOOGLE_CLIENT_ID` (must equal the backend's configured audience,
  `206746117317-52ft783fisdiua9698s7123ocvqor1u4.apps.googleusercontent.com`), a new
  `GOOGLE_MAPS_API_KEY` (not yet provisioned — needs Maps JavaScript API, Places API, and Directions API
  enabled on the same Google Cloud project, HTTP-referrer restricted before it's usable outside local
  dev), and `API_BASE_URL` (defaults to `http://localhost:5136` for local dev). Design.md covers how a
  build-free static page injects these without committing secrets.
- **Local dev serving constraint**: must be served from a real HTTP origin (e.g. `http://127.0.0.1:5500`,
  already present in the backend's `ALLOWED_ORIGIN` CORS allowlist), not opened as a `file://` URL — both
  Google Identity Services and the backend's CORS policy require a real origin.
- **No backend changes**: `carrier-lookup` and `auth` are used as-is; nothing in `backend/` is modified by
  this change.
- **Downstream**: unblocks `qa-integration-tester` (full-stack live exercise) and `delivery-manager`
  (static-site deployment) once this change is applied and archived.
