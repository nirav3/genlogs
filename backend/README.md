# Genlogs.Api — local development

## Required configuration

The app fails fast at startup if this is missing:

- `GOOGLE_CLIENT_ID` — OAuth client ID used as the JWT bearer `Audience` when verifying Google ID tokens.

Optional:

- `ConnectionStrings__Default` — SQLite file path (defaults to `Data Source=genlogs.db`).
- `ALLOWED_ORIGIN` — comma-separated list of allowed CORS origins (no wildcard; defaults to none, i.e.
  cross-origin requests are rejected until set).

None of these are committed to `appsettings.json` — set them locally via `dotnet user-secrets` (never
commit real secrets to `appsettings.Development.json`):

```
cd Genlogs.Api
dotnet user-secrets init
dotnet user-secrets set "GOOGLE_CLIENT_ID" "<your-oauth-client-id>"
dotnet user-secrets set "ALLOWED_ORIGIN" "http://localhost:5173"
```

In deployment, set the same keys as real environment variables instead.

## Auth model

There is no token-exchange endpoint and no credential minted by this API. The frontend obtains a Google
ID token via Google Identity Services and sends that same token as the `Authorization: Bearer <token>`
header on every request to a protected endpoint (currently `POST /api/carriers/lookup`). The API verifies
the token directly against Google's public signing keys (`Authority = https://accounts.google.com`,
`Audience = GOOGLE_CLIENT_ID`) — it never issues, signs, or stores a credential of its own.

## Running

```
dotnet run --project Genlogs.Api
```

The SQLite database is created and seeded fresh on every startup (see `Data/DbInitializer.cs`) — it is
never committed to git.

## Testing

```
dotnet test
```
