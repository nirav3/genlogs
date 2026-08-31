# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project overview

Genlogs is a platform concept for tracking commercial trucks nationwide from highway camera images (license
plates, truck IDs, company logos), cross-referenced against the SAFER FMCSA API by USDOT number. This repo
currently holds a technical exercise (see `requirements.md`) whose deliverable is a small **portal simulation**:
a single-page front end where a user picks an origin/destination city, sees the fastest 3 routes on an embedded
Google Map, and gets back a list of carriers moving trucks on that lane. The carrier data is intentionally static
for this exercise — no database — and lives in `data/carriers.mock.json`:

- NYC → Washington, DC: Knight-Swift, J.B. Hunt, YRC Worldwide
- San Francisco → Los Angeles: XPO Logistics, Schneider, Landstar Systems
- Any other origin/destination pair: UPS Inc., FedEx Corp (default fallback)

**Current state:** no application code exists yet — the repo is at the planning/scaffolding stage (requirements,
mock data, OpenSpec config, and agent definitions only). There are no build, lint, or test commands to run until
front-end/back-end code is added.

## Spec-driven workflow (OpenSpec)

This repo uses OpenSpec (`openspec/config.yaml`, schema: `spec-driven`) to plan work before it's implemented.
Changes go through `proposal.md` → `specs/<capability>/spec.md` (delta spec) → `design.md` → `tasks.md`, then get
applied and archived. Drive this via the `/opsx` slash commands or the matching skills — do not hand-roll planning
docs outside this flow:

- `opsx:propose` / `openspec-propose` — new change, all artifacts in one step (planning only, does not touch code)
- `opsx:explore` / `openspec-explore` — think through a problem before/instead of proposing
- `opsx:update` / `openspec-update-change` — revise an existing change's artifacts
- `opsx:apply` / `openspec-apply-change` — implement a change's `tasks.md`
- `opsx:sync` / `openspec-sync-specs` — fold a change's delta spec into the main specs
- `opsx:archive` / `openspec-archive-change` — finalize and archive a completed change

No changes have been proposed yet (`openspec/` has no `changes/` or `specs/` directories yet).

## Subagent pipeline

`.claude/agents/` defines five specialized subagents meant to be used in this order:

1. **system-architect** — first stop for the architecture/component/data-flow design called for in
   `requirements.md` points 2–3. Produces Mermaid diagrams via Artifact; writes no code.
2. **backend-developer** — implements the API. Default stack is **.NET / C# (ASP.NET Core)** unless the repo
   already shows otherwise. Security-first (validated DTOs, auth on every endpoint, parameterized queries), writes
   xUnit/NUnit/MSTest tests for every feature, verifies with `dotnet build` / `dotnet test` before reporting done.
3. **frontend-developer** — implements the UI. Default stack is **plain/vanilla JavaScript** (no framework) —
   standard DOM APIs, small focused modules, Jest+jsdom tests written immediately after each feature.
4. **qa-integration-tester** — runs only after front end + back end are both done; exercises the full stack live
   (via the Claude Browser tools), reports bugs and a performance scorecard, does not fix code.
5. **delivery-manager** — proposes deployment (Starter/Growth/Scale tiers, AWS by default: S3+CloudFront for the
   front end, Lambda/App Runner/ECS Fargate for the .NET API, RDS for a DB if one is introduced later), and writes
   the actual IaC/CI-CD only once a tier is chosen.

Each agent's own file is the source of truth for its conventions (security rules, test expectations, tiering
approach, etc.) — read the relevant one before doing that kind of work rather than duplicating its rules here.

Once front-end and back-end code exist, use each agent's own verification commands (`dotnet build` / `dotnet test`
for the API, the project's `npm test` once a `package.json` is added for the UI) — there is nothing to build/lint/
test in the repo yet.
