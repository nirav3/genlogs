## 1. Project setup

- [x] 1.1 Create `frontend/` with `index.html`, `src/`, and a `package.json` (Jest + `jest-environment-jsdom` as dev dependencies, `test` script), and verify `npm install` succeeds from `frontend/`
- [x] 1.2 Add `frontend/src/config.example.js` (committed template: `GOOGLE_CLIENT_ID`, `GOOGLE_MAPS_API_KEY`, `API_BASE_URL` placeholders) and add `frontend/src/config.js` to `.gitignore`
- [x] 1.3 Add `frontend/README.md` documenting local setup: copy `config.example.js` → `config.js` and fill in real values, serve `frontend/` from `http://127.0.0.1:5500` (e.g. VS Code Live Server or `npx serve -l 5500`), and the Google Cloud prerequisites (OAuth client's Authorized JavaScript origin includes `http://127.0.0.1:5500`; a Maps API key with Maps JavaScript API + Places API + Directions API enabled, HTTP-referrer restricted)

## 2. API client

- [x] 2.1 Implement `src/apiClient.js`: a `lookupCarriers(origin, destination, idToken)` function that POSTs to `{API_BASE_URL}/api/carriers/lookup` with the JSON body and `Authorization: Bearer` header, and returns the parsed `{ carriers }` on success
- [x] 2.2 Classify failures into `validation | auth | rate_limited | server` per response status (400/401/429/other) with a safe generic message per class, never the raw response body, and verify with Jest tests covering each status code plus a network-failure (`fetch` rejects) case
- [x] 2.3 Verify a success-path Jest test asserts the request URL, method, headers (including the bearer token), and body shape, and that the resolved carrier list is returned unmodified/unsorted

## 3. Auth (Google Identity Services)

- [x] 3.1 Implement `src/auth.js`: initialize Google Identity Services with `GOOGLE_CLIENT_ID`, render the sign-in control, and expose `getIdToken()`, `isSignedIn()`, `onAuthChange(callback)`, and `signOut()` (clears the in-memory token and notifies listeners)
- [x] 3.2 Store the obtained ID token only in a module-level variable (no `localStorage`/cookies), and verify a Jest test confirms `getIdToken()` returns `null` after `signOut()`
- [x] 3.3 Verify Jest tests cover: the sign-in callback populating the token and firing `onAuthChange(true)`, and `signOut()` firing `onAuthChange(false)`, using a mocked `window.google.accounts.id`

## 4. Places autocomplete

- [x] 4.1 Implement `src/places.js`: attach a `google.maps.places.PlaceAutocompleteElement` to the origin and destination inputs, and expose `getOrigin()`/`getDestination()` returning the resolved place (or `null` if unresolved) plus `onSelectionChange(callback)`
- [x] 4.2 Verify a Jest test (with a stub custom element / mocked `gmp-select` event) confirms `getOrigin()`/`getDestination()` return `null` before a place is selected and the resolved place after selection

## 5. Map and routes

- [x] 5.1 Implement `src/mapRoutes.js`: `renderRoutes(originPlace, destinationPlace)` that calls `DirectionsService.route(...)` with `provideRouteAlternatives: true`, sums each route's leg durations, sorts ascending, and renders the first 3 via `DirectionsRenderer`
- [x] 5.2 Handle the zero-routes case by showing a "no route found" state instead of rendering an empty/stale map, and verify with a Jest test using a mocked Directions response
- [x] 5.3 Verify a Jest test confirms that given 4+ mock alternative routes with varying durations, exactly 3 are rendered and in ascending-duration order

## 6. Carrier list rendering

- [x] 6.1 Implement `src/carrierList.js`: `renderCarriers(carriers)` that renders each carrier's name and trucks-per-day in the given array order, and a distinct empty-state render when the array is empty
- [x] 6.2 Verify Jest tests cover: non-empty list renders all entries in input order without re-sorting, and an empty array renders the empty state (not a blank list, not an error)

## 7. App wiring and UI states

- [x] 7.1 Implement `src/app.js`: load `config.js` and fail fast with a visible on-page setup error if any required value is missing/placeholder (verify with a Jest test that a missing config key produces the error state and skips SDK initialization)
- [x] 7.2 Wire sign-in state and resolved-place state so the Search control is enabled only when both are true, and disabled otherwise (verify with a Jest test toggling each precondition independently)
- [x] 7.3 Wire Search submission to call `apiClient.lookupCarriers(...)` and `mapRoutes.renderRoutes(...)` together, showing a loading state until both resolve (verify with a Jest test using mocked `apiClient`/`mapRoutes`)
- [x] 7.4 Wire the `auth` error classification from `apiClient` to call `auth.signOut()` and show a re-sign-in prompt, and wire `validation`/`rate_limited`/`server` classifications to distinct visible messages (verify with Jest tests, one per classification, asserting the resulting UI state and that `auth.signOut()` is called only for the `auth` case)
- [x] 7.5 Inject the Google Maps JavaScript API `<script>` tag at runtime using `config.js`'s `GOOGLE_MAPS_API_KEY` (not hardcoded in `index.html`), and verify by inspecting the injected tag's `src` in a Jest test (mocking `document.createElement`/`appendChild`)

## 8. Markup and manual verification

- [x] 8.1 Build `index.html`: origin/destination inputs, Search button, sign-in control container, map container, carrier-list container, and the static Google Identity Services `<script>` tag; load `src/app.js` as `type="module"`
- [x] 8.2 Run the full Jest suite from `frontend/` and verify it passes (`npm test`)
- [ ] 8.3 Serve `frontend/` on `http://127.0.0.1:5500` against a locally running backend, and manually verify: sign-in gates search, all 3 documented lane cases (NYC↔DC, SF↔LA, an unmatched pair) return the expected carriers, up to 3 routes render on the map, and an expired/absent token produces the re-sign-in prompt rather than a silent failure — record the result in the PR/change notes since this is a manual step, not an automated test
