## Context

See `proposal.md` - Why for motivation. Constraints that shape this design:

- No framework, no bundler (`.claude/agents/frontend-developer.md`) — the page ships as plain ES modules
  loaded by the browser directly.
- The API contract is fixed and already live: `POST /api/carriers/lookup`, JSON body
  `{ origin, destination }`, requires `Authorization: Bearer <google-id-token>`, returns
  `{ carriers: [{ name, trucksPerDay }] }` already ranked descending, or a `400`/`401`/`429`/`5xx` on
  failure (`backend/Genlogs.Api/Endpoints/CarrierEndpoints.cs`,
  `openspec/specs/carrier-lookup/spec.md`, `openspec/specs/auth/spec.md`).
- The `auth` capability verifies a Google ID token directly — there is no session-exchange endpoint, so
  the frontend's only credential is the ID token Google Identity Services hands back.
- The backend's CORS allowlist is a fixed value (`ALLOWED_ORIGIN=http://127.0.0.1:5500`); local dev must
  be served from that exact origin or the browser will reject the API calls before this design's code
  even runs.
- Google Identity Services requires a real HTTP(S) origin (not `file://`) for both sign-in and the
  "Authorized JavaScript origin" check on the OAuth client.

## Goals / Non-Goals

**Goals:**
- A build-free static page that runs by opening `index.html` through a static file server on
  `127.0.0.1:5500`.
- Keep the three external config values (`GOOGLE_CLIENT_ID`, `GOOGLE_MAPS_API_KEY`, `API_BASE_URL`) in one
  place, gitignored, without inventing a build step to inject them.
- Every module independently unit-testable under Jest + jsdom without a real network or Google script
  load.

**Non-Goals:**
- Token refresh / long-lived sessions. A Google ID token is short-lived (~1 hour); this design re-prompts
  sign-in on expiry/401 rather than silently refreshing. Acceptable for a portal-simulation exercise.
- Cross-browser support beyond current evergreen browsers (native ES modules, no transpilation, no
  polyfills).
- Styling system/design polish — out of scope per the proposal.

## Decisions

**Module layout** (`frontend/`):
```
index.html
src/
  config.js            (gitignored — real values)
  config.example.js     (committed template)
  auth.js               (Google Identity Services wrapper)
  places.js             (From/To autocomplete wiring)
  apiClient.js          (fetch wrapper + error classification)
  mapRoutes.js           (embedded map + up to 3 routes)
  carrierList.js         (renders the carrier list)
  app.js                 (wiring/init, loaded as the page's entry module)
```
One concern per module, mirroring how the backend splits into `Services/`/`Endpoints/` rather than one
script — matches `frontend-developer`'s "small focused modules" convention and keeps each piece
independently testable.

**Config without a build step**: `src/config.js` is gitignored and exports the three values as a plain
object; `src/config.example.js` (committed) documents the shape with placeholder values — the same
pattern the backend already uses for secrets it won't commit (`docs/secrets/`, `GOOGLE_CLIENT_ID` via
`appsettings.json`/user-secrets). `app.js` imports `config.js` first and fails fast with a visible
on-page setup error if a value is missing or still a placeholder, rather than letting Google's SDKs fail
with an opaque console error later — same "fail fast on bad config" spirit as the backend's
`ConfigurationGuard`.
- *Alternative considered*: a `<script>` tag with inline constants in `index.html`. Rejected — it's the
  one file most likely to get committed by accident with a real key pasted in during local testing; a
  dedicated gitignored module is harder to commit by mistake.

**Loading the Maps JS API without hardcoding the key in HTML**: the Google Identity Services script has no
key (the client ID is passed to `google.accounts.id.initialize()` at runtime), so it's a static `<script>`
tag in `index.html`. The Maps JavaScript API script *does* embed the key in its `src` URL, so `app.js`
injects that `<script>` tag at runtime (`document.createElement('script')`, `src` built from
`config.js`'s `GOOGLE_MAPS_API_KEY`) instead of writing it into committed HTML.

**Places input: `PlaceAutocompleteElement`, not the legacy `Autocomplete` class**: Google deprecated
`google.maps.places.Autocomplete` for new customers (March 2025) in favor of the
`places.PlaceAutocompleteElement` web component. Since this is new code, building on the deprecated widget
would mean starting the exercise already on a removal path.
- *Trade-off*: `PlaceAutocompleteElement` is a custom element with a different selection event
  (`gmp-select`) than the legacy widget's `place_changed`, so `places.js` owns that event-wiring detail
  entirely — nothing outside it needs to know which Places API is in use.

**Route alternatives are sorted client-side before display**: the Directions API's
`provideRouteAlternatives: true` returns alternative routes but does not guarantee they're ordered by
travel time. `mapRoutes.js` requests alternatives, sums each returned route's leg durations, sorts
ascending, and renders the first three via `DirectionsRenderer` — this is what makes the spec's "ordered
fastest first" scenario actually hold, rather than trusting response order.

**Credential storage: in-memory only, not `localStorage`/cookies**: `auth.js` holds the ID token in a
module-level variable, not persisted storage. It doesn't survive a page reload (the user re-signs-in),
but it also can't be exfiltrated via a stored-XSS payload reading `localStorage` after the fact, and
matches the backend's own "nothing persisted" stance on identity (`openspec/specs/auth/spec.md`).

**API error classification lives in `apiClient.js`**: it maps HTTP status → one of
`validation | auth | rate_limited | server` (per the `portal-ui` spec's distinct-error-states
requirement) and returns that classification plus a safe, generic message — never the raw response body
— to callers. `app.js` maps each classification to the corresponding UI state; on `auth`, it also calls
`auth.js`'s sign-out/reset so the UI returns to signed-out rather than showing a stale carrier list.

**Local dev serving is standardized on `127.0.0.1:5500`**: chosen to match the backend's existing
`ALLOWED_ORIGIN` value exactly rather than changing backend config for the frontend's sake. `frontend/README.md`
(added in tasks) documents serving via VS Code's Live Server extension (or any static server bound to that
exact host/port, e.g. `npx serve -l 5500`) — the requirement is the origin string, not the specific tool.

## Risks / Trade-offs

- **No token refresh** → a user mid-session after ~1 hour gets a `401` on their next search. Mitigated:
  `apiClient.js`'s `auth` classification triggers an explicit "please sign in again" prompt instead of a
  generic error, so the failure mode is recoverable and clear, not confusing.
- **Client-exposed Maps API key** is inherent to any browser Maps integration, not specific to this
  design. Mitigated by the HTTP-referrer restriction called out as a setup prerequisite in `proposal.md`
  — the key alone isn't sufficient to abuse the quota from another origin.
- **`PlaceAutocompleteElement` is newer/less battle-tested** than the legacy widget it replaces. Mitigated
  by isolating all Places-specific wiring inside `places.js`, so a future swap (if Google's API surface
  changes again) touches one module.
- **No bundler** means no dependency de-duplication or minification; acceptable at this exercise's scale
  (a handful of small modules, two external SDK scripts).

## Migration Plan

Net-new code; no existing behavior changes. Build order (also drives `tasks.md`): `config.example.js` →
`apiClient.js` → `auth.js` → `places.js` → `mapRoutes.js` → `carrierList.js` → `app.js` wiring →
`index.html`, writing each module's Jest+jsdom tests immediately after that module, per
`frontend-developer`'s conventions. Rollback is simply not linking/serving the `frontend/` directory —
nothing else in the repo depends on it existing.
