# Genlogs portal — front end

Single-page, build-free front end for the portal simulation described in `requirements.md` point 4.1.
Plain ES modules loaded directly by the browser — no framework, no bundler.

## Local setup

1. **Config**: copy `src/config.example.js` to `src/config.js` (gitignored) and fill in real values:
   - `GOOGLE_CLIENT_ID` — must match the backend's configured audience.
   - `GOOGLE_MAPS_API_KEY` — a Google Maps API key with **Maps JavaScript API**, **Places API**, and
     **Directions API** enabled on the same Google Cloud project, HTTP-referrer restricted.
   - `API_BASE_URL` — the running backend's base URL (defaults to `http://localhost:5136` for local dev).
2. **Google Cloud prerequisites**:
   - The OAuth client's **Authorized JavaScript origins** must include `http://127.0.0.1:5500`.
   - The Maps API key must have Maps JavaScript API, Places API, and Directions API enabled, and should be
     HTTP-referrer restricted before use outside local dev.
3. **Serve** `frontend/` from `http://127.0.0.1:5500` — this exact origin is required because:
   - Google Identity Services requires a real HTTP(S) origin (not `file://`).
   - The backend's CORS allowlist (`ALLOWED_ORIGIN`) is fixed to `http://127.0.0.1:5500`.

   Any static server bound to that exact host/port works, e.g.:
   - VS Code's [Live Server](https://marketplace.visualstudio.com/items?itemName=ritwickdey.LiveServer)
     extension, configured to serve on host `127.0.0.1` port `5500`.
   - `npx serve -l 5500` (then browse to `http://127.0.0.1:5500`, not `localhost:5500`).
4. **Run the backend** locally (see `backend/`) so `API_BASE_URL` has something to talk to.

## Tests

```
cd frontend
npm install
npm test
```

Jest + `jest-environment-jsdom`, mocking `fetch` and the Google Identity Services / Maps JS globals — no
real network or Google script load required to run the suite.
