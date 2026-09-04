# Genlogs.Api container image for Render (type: web, runtime: docker).
#
# Render has no native .NET runtime (Node/Python/Ruby/Go/Rust/Elixir only — see
# https://render.com/docs/language-support), so the API is deployed as a Docker image built by
# Render's own BuildKit builder from this file. Build context is this directory
# (backend/Genlogs.Api) — see render.yaml's rootDir/dockerContext for this service.

# ---- Build stage --------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Restore first, separately, so dependency layers cache across builds that only change source.
COPY Genlogs.Api.csproj ./
RUN dotnet restore Genlogs.Api.csproj

COPY . .
RUN dotnet publish Genlogs.Api.csproj -c Release -o /app/publish --no-restore

# ---- Runtime stage --------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# ASPNETCORE_ENVIRONMENT is also set as a Render env var (render.yaml) — kept here too so
# `docker run` locally behaves the same way without extra flags.
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

# Render assigns a container port at deploy time via $PORT (defaults to 10000 if unset) and expects
# the service to bind 0.0.0.0:$PORT — see https://render.com/docs/web-services#port-binding.
# ASPNETCORE_URLS is set at container start (not build time) so it always reflects Render's actual
# injected port rather than a value baked into the image.
EXPOSE 10000

# Render's edge terminates TLS and forwards plain HTTP to this container. Program.cs currently calls
# UseHttpsRedirection() unconditionally with no ForwardedHeaders middleware configured, which will
# see every request as HTTP and issue a redirect the browser just repeats forever (redirect loop) —
# see RENDER_DEPLOY.md "Known compatibility note before first deploy" for the one-line fix needed in
# application code (out of scope for this container/infra change).
ENTRYPOINT ["sh", "-c", "ASPNETCORE_URLS=http://0.0.0.0:${PORT:-10000} exec dotnet Genlogs.Api.dll"]
