# Deploying Genlogs to Render

Two Render services, defined in [`render.yaml`](./render.yaml) at the repo root:

| Service            | Type                    | Source                                  |
|---------------------|-------------------------|------------------------------------------|
| `genlogs-api`       | Web Service (Docker)    | `backend/Genlogs.Api/Dockerfile`         |
| `genlogs-frontend`  | Static Site             | `frontend/` (no build step, plain files) |

Deployment plan, cost tiers, and architecture diagrams: see the published plan —
`https://claude.ai/code/artifact/7bd9ab8b-35ec-40dd-9d85-37ac8bf1c58c`. **Starter tier ($0/month)**
is what this repo is configured for below; the same `render.yaml` upgrades to Growth by changing one
line (see "Upgrading" at the bottom).

---

## Known compatibility note — read before first deploy

Render's edge terminates TLS and forwards **plain HTTP** to the container over Render's internal
network. `Program.cs` currently calls `app.UseHttpsRedirection()` unconditionally, with no
`ForwardedHeaders` middleware configured. Kestrel will see every request as HTTP and redirect to
HTTPS; the browser repeats the same HTTPS request, which arrives at the container as HTTP again —
an infinite redirect loop that breaks every API call.

This is an application-code fix (out of scope for this deployment pass — hand to the
backend-developer agent), one of:

- Skip `UseHttpsRedirection()` in `Production` (Render already enforces HTTPS at its edge for every
  `*.onrender.com` and custom domain, so the app doesn't need to redirect a request it never actually
  receives over plain HTTP from a real client), **or**
- Add `app.UseForwardedHeaders(new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedProto })`
  before `UseHttpsRedirection()` so Kestrel trusts Render's `X-Forwarded-Proto` header.

Confirm this is fixed (or fix it) before deploying, or `genlogs-api` will be unreachable.

---

## One-time setup

### 1. Push this repo to GitHub (if not already)
Render's GitHub auto-deploy and the `checksPass` deploy trigger both require the repo to be on
GitHub with Actions enabled (already true here — `.github/workflows/ci.yml` runs on push/PR to `main`).

### 2. Google Cloud Console — before you have Render URLs you'll need to revisit this
The app uses Google Identity Services (Sign in with Google) and the Maps JavaScript API, both tied to
a GCP project:
- **OAuth client (Web)**: add `https://genlogs-frontend.onrender.com` (or your actual assigned URL,
  see step 5) to **Authorized JavaScript origins**. No redirect URI needed — this app uses the GSI
  token-callback flow, not a redirect flow.
- **Maps API key**: restrict it by **HTTP referrer** to `https://genlogs-frontend.onrender.com/*`
  (and `http://localhost:5500/*` etc. for local dev if you keep using the same key). The key is
  visible in shipped JS regardless — referrer restriction is the actual control, not secrecy.

### 3. Create the Blueprint in Render
Render dashboard → **New** → **Blueprint** → select this GitHub repo → Render reads `render.yaml` and
proposes both services → **Apply**.

### 4. Set the secret env vars (not stored in `render.yaml`, `sync: false`)
In the Render dashboard, for each service → **Environment**:

**`genlogs-api`**
| Key | Value |
|---|---|
| `GOOGLE_CLIENT_ID` | Your Google OAuth Web client ID (same value as `appsettings.json`'s `GOOGLE_CLIENT_ID`, or a prod-specific one — must match the frontend's copy below) |

**`genlogs-frontend`**
| Key | Value |
|---|---|
| `GOOGLE_CLIENT_ID` | Same value as `genlogs-api`'s `GOOGLE_CLIENT_ID` — must match exactly |
| `GOOGLE_MAPS_API_KEY` | Your Maps JavaScript API key (restricted per step 2) |

Tip: create a Render **Environment Group** (dashboard → Env Groups) holding `GOOGLE_CLIENT_ID` once
and link it to both services, instead of pasting the same value twice — reduces drift when it rotates.

### 5. Verify the assigned URLs, then fix cross-references if needed
`render.yaml` assumes Render assigns exactly `genlogs-api.onrender.com` and
`genlogs-frontend.onrender.com` (derived from each service's `name`). If either name collided with an
existing Render service elsewhere, Render appends a random suffix instead. After the first deploy:

1. Check both services' actual URLs in the dashboard.
2. If either differs from the assumption, update:
   - `genlogs-api`'s `ALLOWED_ORIGIN` env var → the real frontend URL
   - `genlogs-frontend`'s `API_BASE_URL` env var → the real backend URL
   - Google Cloud Console's Authorized JavaScript origin / Maps referrer restriction (step 2)
3. Trigger a manual redeploy of both services (env var changes alone don't rebuild `config.js`,
   which is generated at build time — a plain redeploy re-runs the build command).

### 6. Confirm CI is gating deploys
Both services use `autoDeployTrigger: checksPass` — a push to `main` only auto-deploys once
`.github/workflows/ci.yml`'s `backend-tests` and `frontend-tests` jobs report green on that commit.
Push to `main` (or merge a passing PR) and watch both services deploy in the Render dashboard.

---

## What ships where

- **`genlogs-api`** (Docker): `backend/Genlogs.Api/Dockerfile` — multi-stage `dotnet publish` build,
  runs on `mcr.microsoft.com/dotnet/aspnet:8.0`. Binds to Render's injected `$PORT` at container
  start (defaults to `10000` if unset — see the Dockerfile's `ENTRYPOINT`). Health-checked by Render
  at `GET /health`.
- **`genlogs-frontend`** (Static Site): served as-is from `frontend/` — no bundler, no `npm install`
  needed to serve it. The one build step is generating `frontend/src/config.js` (gitignored, normally
  hand-written locally per `frontend/src/config.example.js`) from the three env vars above, via the
  `buildCommand` in `render.yaml`.

## SQLite persistence decision

`backend/Genlogs.Api/Data/DbInitializer.cs` seeds the database from static in-code data on every
startup (`if (db.Carriers.Any()) return;` — otherwise reseeds from scratch) and `Program.cs` runs
`db.Database.Migrate()` + `DbInitializer.Seed(db)` on every boot. There are no write endpoints —
`CarrierEndpoints.cs` is read-only. **Consequence: the container's ephemeral filesystem (wiped on
every deploy, and on every free-tier spin-down) is not a problem to work around — it's the correct,
$0 choice.** No Render Disk is attached at any tier in this plan. If the app ever gains a real write
path (e.g. actual detection ingestion instead of static mock data), the right move is migrating to
Render managed Postgres (Scale tier), not attaching a disk to SQLite — Render Disks aren't shared
across multiple instances of a horizontally-scaled service, so SQLite-on-disk still can't outlive a
single instance.

## CORS

`Program.cs` reads `ALLOWED_ORIGIN` (comma-separated) and applies it via the `"Default"` CORS policy
(`WithOrigins(...).AllowAnyHeader().AllowAnyMethod()`). Set it to the frontend's exact deployed
origin (scheme + host, no trailing slash) — see step 5 above if the assumed URL doesn't match.

## Upgrading to Growth ($7/month)

Change one line in `render.yaml`:

```diff
   - type: web
     name: genlogs-api
     runtime: docker
-    plan: free
+    plan: 0.5c-512mb   # Render "Starter" compute plan — 0.5 CPU / 512MB, always-on, no spin-down
```

Commit and push to `main` (through the normal CI gate) — Render redeploys the service on the new
plan. Nothing else in the architecture changes at this tier (see the published plan for the full
Growth/Scale breakdown, including when SQLite needs to become Postgres).
