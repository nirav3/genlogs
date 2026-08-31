# Genlogs AWS Service Justification & Cost Analysis

Deep-dive on every AWS service in the ingestion → ML extraction → identity resolution → warehouse
pipeline (the service-level diagram at `docs/architecture/aws-architecture-diagram.html`). For each
service: why it was chosen, pros/cons specific to this use case, scalability behavior, availability
story, and cost at a stress-test load of **10,000 images/second sustained**.

This is a companion to `docs/architecture/platform-architecture.md` (the logical component/data-flow
and database design for points 2–3) — that document is unchanged; this one only adds service-level
justification and cost. It does not redesign the point-4 portal simulation.

## How to read this document

- Every service section ends with **"what we drew" vs "what we'd actually deploy at this load"** where
  those differ. The diagram shows the right pipeline *shape*; at 10,000 img/sec several of the literal
  service choices need swapping for cost or hard-limit reasons, called out explicitly.
- All pricing is **on-demand, us-east-1, no Savings Plans/Reserved capacity/Enterprise discounts**,
  pulled via web search against AWS's public pricing pages/blog aggregators in **August 2026**. Each
  section states the rate used and flags where sources disagreed so the number can be challenged.
- Every cost figure is arithmetic you can recompute — assumptions are stated before the math, not buried
  in it.

## Global assumptions (stated once, used throughout)

| Assumption | Value | Why |
|---|---|---|
| Sustained load | 10,000 images/sec | Given stress-test figure |
| → per day | 864,000,000 images/day | 10,000 × 86,400 s |
| → per month (30-day) | **25,920,000,000 images/month** (25.92B) | 864M × 30 |
| Avg raw image size | 2 MB | Typical HD JPEG frame from a fixed highway ALPR camera |
| Camera fleet size | ~5,000 physical camera sites | Only used to size the ingestion *control plane* (heartbeats, credential refresh), not per-image costs |
| Raw image retention (S3 raw zone) | 7 days rolling | Per the architecture doc's lifecycle policy — raw frames aren't needed once extraction succeeds |
| Extractions written per image | 2.5 avg (of 3 possible: plate / unit-#/ logo) | Not every frame yields all three legibly |
| Curated fact record size | ~2 KB (pre-batch JSON/Parquet) | Resolved detection + carrier merge, much smaller than the source image |
| Curated zone retention (S3) | 90 days rolling | Source-of-truth staging behind the warehouse, in case of reload |
| Warehouse fact retention | 24 months, columnar-compressed | Analytics window; compression ~4× vs. raw JSON |
| SAFER cache hit rate | 99% | Long TTL (30 days) against a bounded universe of ~2M active USDOT numbers nationwide |
| Pricing baseline | On-demand, us-east-1, Aug 2026, no free tier (immediately exhausted at this volume) | |
| Explicitly excluded from totals | Data transfer/NAT gateway, CloudWatch logs/metrics, VPC endpoints, AWS Support plan, human QA review tooling | Real costs, but not part of "the services in the diagram"; flagged, not zero |

One honest caveat up front: **25.92 billion images/month is roughly 2–3 orders of magnitude beyond any
plausible real nationwide highway-camera deployment** (it implies each of ~5,000 camera sites capturing
2 frames/sec continuously, 24/7, not "on vehicle presence"). The point of running the numbers at this
level, per the brief, is to find where the architecture breaks — several of the "as drawn" choices below
fail outright at this volume, which is the useful signal. Treat the dollar figures as directionally
correct, order-of-magnitude arithmetic, not an invoice.

---

## 1. API Gateway / IoT Core — camera ingestion endpoint

| | |
|---|---|
| **Why chosen** | Cameras/edge units need an authenticated, managed entry point into AWS without Genlogs running its own edge-facing servers. API Gateway gives per-camera auth (API keys/IAM/Lambda authorizer) and request validation; IoT Core is the alternative if cameras are managed as a persistent-connection device fleet (MQTT, device shadow, OTA firmware). |
| **Pros** | Fully managed, scales without capacity planning; built-in auth/throttling per client; IoT Core adds device lifecycle management (shadow state, fleet provisioning) cameras will eventually need anyway (firmware, health). |
| **Cons/trade-offs** | Both are **per-request billed** — fine for a light control-plane call, expensive if every image's bytes are proxied through them. API Gateway also has a hard **10 MB payload limit** and a **29-second integration timeout**, both of which a 2 MB image individually fits under, but neither leaves headroom for larger frames or retries at the edge. |
| **Scalability** | API Gateway scales to very high RPS with no explicit knob (regional default throttle ~10,000 RPS/5,000 burst, raisable via support ticket). IoT Core scales per-account message-rate quotas (also raisable) and connection counts. |
| **Availability** | Both are regional, multi-AZ managed services with no user-managed failover. A regional API Gateway/IoT Core outage stops *new* uploads; cameras with local buffering (edge storage) queue and retry — this is the main reason to spec edge units with local disk buffer, not a purely fire-and-forget uplink. |
| **Cost at 10K img/sec — as drawn** | If every image were proxied through **REST API Gateway** (1 call/image): 25.92B requests/month against tiered pricing ($3.50/M first 333M, $2.80/M next 667M, $2.38/M beyond) ≈ **$62,343/month** — and this ignores that pushing 2 MB payloads through a REST proxy integration is itself an anti-pattern this volume would surface immediately (Lambda-proxy integration overhead, timeout risk under retry storms). |
| **Cost at 10K img/sec — recommended** | Don't proxy image bytes through API Gateway at all. Cameras hold rotating, short-lived S3-signed upload credentials (refreshed in a low-frequency batch call, e.g. hourly) and **PUT directly to S3**; camera_id/timestamp/GPS ride in the S3 object key/metadata instead of a separate API call, so the raw-arrival trigger for the next stage comes from the S3 write itself (see §2/§3), not from this endpoint. IoT Core is kept only for fleet device-management traffic (heartbeat every 60s/camera): 5,000 cameras × 1/min = 7.2M msgs/month × $1.00/M = **$7.20/month**. Credential-refresh API Gateway calls (5,000 cameras × 24/day × 30 = 3.6M/month) at HTTP API rate ($1.00/M) ≈ **$3.60/month**. **Total: ~$11/month**, i.e., three orders of magnitude cheaper than proxying bytes — the fix is architectural, not a pricing-tier trick. |

## 2. Amazon S3 — raw image data lake

| | |
|---|---|
| **Why chosen** | Durable (11 nines), cheap, and the natural landing zone for immutable binary blobs that many independent downstream consumers (extraction branches, audit/replay, retraining datasets) need to read without a shared server in the path. |
| **Pros** | No provisioning; scales to arbitrary object count/throughput; native event notifications remove the need for downstream polling; lifecycle rules automate the 7-day raw-image retention without custom code. |
| **Cons/trade-offs** | Cost is **linear in request count and bytes stored** — at extreme image volume, S3 becomes one of the two largest line items in the whole bill (see below), and there's no way to make raw image storage cheaper without either shrinking the image, shortening retention, or reducing frame-capture rate — this is a genuine cost floor, not a service-selection mistake. |
| **Scalability** | Effectively unlimited; S3 auto-partitions prefixes under sustained request load (older "hot prefix" guidance is now largely obsolete, but very high single-prefix PUT rates still benefit from key-name entropy — using `camera_id/date/hour/uuid.jpg` already spreads keys well). |
| **Availability** | 99.99% design availability, multi-AZ by default for Standard storage. Failure mode: an S3 outage in-region blocks new uploads (cameras buffer locally) and blocks the ML pipeline from reading existing objects already-durable data is not at risk, only new writes/reads stall. |
| **Cost at 10K img/sec** | **PUT**: 25.92B images/month × ($0.005/1,000 = $5/M) → 25,920 (millions) × $5 = **$129,600/month**. **Storage** (7-day rolling retention): 864M images/day × 2 MB = 1.728 PB/day generated; steady-state stored = 7 × 1.728 PB = 12.096 PB ≈ 12,096,000 GB × $0.023/GB-month = **$278,208/month**. **GET** (ML extraction reads the raw bytes once per image, not once per branch — see §5 fix): 25.92B × ($0.0004/1,000 = $0.40/M) → 25,920 × $0.40 = **$10,368/month**. **Total: $418,176/month.** (If each of the 3 extraction branches independently re-fetched the image instead of one shared read, GET alone would triple to ~$31,104/month — a concrete reason to fetch once and pass bytes downstream, see §5.) |

## 3. Amazon EventBridge — raw-arrival trigger

| | |
|---|---|
| **Why chosen (as drawn)** | Decouples "an image arrived" from "process this image," and is the natural default when you might later want multiple independent consumers of the same S3 event (e.g., a separate audit/compliance pipeline) without touching the producer. |
| **Pros** | Content-based filtering, multi-consumer fan-out, schema registry — valuable if the number of downstream consumers of "new image" grows. |
| **Cons/trade-offs** | At this volume, EventBridge is a **paid hop between two things S3 can already talk to directly for free**: S3 → Lambda/SQS native event notifications cost nothing on the S3 side. EventBridge's value (routing/filtering/multi-consumer) isn't being used yet — there's exactly one consumer (the extraction pipeline) — so the fee is being paid for optionality not currently exercised. |
| **Scalability** | Very high published throughput per account/region, but scales linearly in *cost*, which is the actual constraint at this volume, not throughput. |
| **Availability** | Regional, multi-AZ, no user-managed component. |
| **Cost at 10K img/sec — as drawn** | 25.92B events/month × $1.00/M = **$25,920/month**. |
| **Cost at 10K img/sec — recommended** | Use **native S3 Event Notifications straight to an SQS queue** (no EventBridge hop) as the raw-arrival trigger. S3→SQS delivery itself is free; the only charge is the SQS request cost: 25.92B messages × $0.40/M = **$10,368/month** (same maths as §8's queue). Revisit EventBridge if/when a second independent consumer of "new image" actually appears — that's the point at which its fan-out earns its cost. |

## 4. AWS Step Functions — per-image ML orchestration

| | |
|---|---|
| **Why chosen (as drawn)** | The extraction stage has three branches (plate OCR, unit-# OCR, logo detection) each needing independent retry/backoff and visual, per-execution observability — a legitimate reason to reach for Step Functions in general. |
| **Pros** | Built-in retry/catch per state, execution history for debugging a specific frame's pipeline, visual audit trail. |
| **Cons/trade-offs** | Standard Workflows bill **per state transition**, which is fatal at this call frequency (below). Standard Workflows also have a **default `StartExecution` throttle far below 10,000/sec** (a low-hundreds/sec soft limit, raisable but not by 1–2 orders of magnitude without AWS engagement) — this design would be rate-limited by the orchestrator itself before it ever got to a cost conversation. |
| **Scalability** | Poor fit at this per-item granularity; Step Functions is built for workflow *complexity* (branching, waits, human approval, long duration), not for being invoked once per event at extreme fan-in rates. |
| **Availability** | Regional, multi-AZ, managed; not the concern here — cost and throughput are. |
| **Cost at 10K img/sec — as drawn** | ≈6 state transitions/image (start → parallel → 3 branch tasks → aggregate). 25.92B × 6 = 155.52B transitions/month × ($0.025/1,000 = $25/M) → 155,520 × $25 = **$3,888,000/month** — and, as noted, likely undeployable at all without a major default-quota increase. This is the clearest "does not hold up" finding in the whole pipeline. |
| **Cost at 10K img/sec — recommended** | The workflow itself is fixed and simple (3 parallel calls, no branching logic, no waits) — that's exactly the case where a **plain Lambda dispatcher** (reads the queue message, invokes the combined inference endpoint from §5, writes the result) beats orchestration entirely; Step Functions Express Workflows is the fallback if branching complexity grows later (billed per-request + GB-second like Lambda, not per-transition). Dispatcher sizing: 25.92B invocations/month, ~200 ms @ 512 MB each. Requests: 25.92B × $0.20/M = **$5,184**. Duration: 25.92B × 0.5 GB × 0.2 s = 2.592B GB-s × $0.0000166667/GB-s = **$43,200**. **Total: ~$48,384/month** — 1.3% of the Standard-Workflow cost, with none of the quota risk. |

## 5. Amazon SageMaker — ALPR / OCR inference

| | |
|---|---|
| **Why chosen** | Generic vision APIs aren't reliable for license-plate character recognition (small, angled, motion-blurred text against variable backgrounds) — this needs a purpose-trained model, which means a real-time SageMaker endpoint, not an off-the-shelf managed API. |
| **Pros** | Full control over model architecture/training data (can be tuned specifically on highway-camera plate imagery); can be extended to a **multi-task model** (see cost fix) that does plate OCR, unit-number OCR, and logo classification in one forward pass off a shared backbone — cutting GPU fleet size roughly 2–3× versus three separate single-task fleets. |
| **Cons/trade-offs** | Requires Genlogs to own model training/retraining/versioning — real ongoing ML-ops cost not captured in the AWS bill. Real-time endpoints bill for **reserved instance-hours whether or not traffic is flowing**, unlike the purely request-billed services elsewhere in this pipeline — utilization matters a lot to the effective per-image cost. |
| **Scalability** | Scaling knob is **instance count behind the endpoint** (SageMaker endpoint auto scaling on an invocations-per-instance target) or moving to **asynchronous/batch inference** if near-real-time isn't required. Throughput per instance depends entirely on model size/batching — this is a modeled assumption here, not a published constant. |
| **Availability** | Endpoints support multi-AZ, multi-instance deployment; a single unhealthy instance is drained and replaced automatically. A full endpoint outage stalls the extraction stage only — raw images are already durably in S3, so nothing is lost, just delayed (and SQS in §8 buffers the backlog). |
| **Cost at 10K img/sec** | Assumption: one **combined multi-task model** (plate + unit-# OCR + logo) on `ml.g4dn.xlarge` ($0.736/hr), sustaining **~15 images/sec/instance** with batching (a stated modeling assumption — real throughput depends on the trained model and needs a load test to confirm before committing budget). Instances needed: 10,000 ÷ 15 ≈ **667 instances**, run continuously. Cost: 667 × $0.736/hr × 730 hr/month = **$358,368/month**. This single fleet replaces both the SageMaker ALPR endpoint *and* the Rekognition-based OCR/logo calls in §6 below — see that section for why running them as separate per-call managed APIs at this volume is far worse. |

## 6. Amazon Rekognition — logo detection + text/OCR (as-drawn API calls)

Treated as one line item since both use cases (generic text detection, Custom Labels logo
classification) hit the same wall at this volume for the same underlying reason: **per-image API-call
billing doesn't scale to billions of calls/month**, regardless of which Rekognition feature is used.

| | |
|---|---|
| **Why it was on the diagram** | Rekognition is the fastest way to get a *working* logo classifier (Custom Labels needs comparatively little training data) and general text detection without operating any inference infrastructure — a reasonable default at low-to-moderate volume. |
| **Pros** | Zero infrastructure to manage, fast to stand up a first version, Custom Labels training itself is cheap ($1/training-hour). |
| **Cons/trade-offs** | Two failure modes at this scale simultaneously: (1) the per-image **Group 2 API price** is a straight per-call multiplier with no ceiling — it does not get cheaper by adding capacity, it gets more expensive by adding volume; (2) **Custom Labels inference bills per inference-hour per unit**, and each unit has a real (and, per AWS docs, model-dependent but bounded) images/sec ceiling — sustaining 10,000 img/sec would require provisioning a very large number of inference units, and Custom Labels inference-unit counts are subject to **account-level service quotas** that are nowhere near this scale by default. |
| **Scalability** | Does not scale cost-effectively to this volume by design — it's priced and quota-limited for "add Rekognition to an app," not "replace a GPU inference fleet." |
| **Availability** | Fully managed, multi-AZ, no user action needed — not the limiting factor here. |
| **Cost at 10K img/sec — as drawn** | At this volume the account is deep into Rekognition's top pricing tier (>35M images/month): Group 2 APIs at $0.00025/image. Running **two** Group 2 calls per image (text detection for the unit number, plus a nominal logo-classification-via-labels pass): 25.92B × 2 × $0.00025 = **$12,960,000/month**, *before* Custom Labels' separate per-inference-hour charge is even added, and before accounting for the near-certainty of hitting inference-unit and TPS service quotas that would block this deployment outright. |
| **Cost at 10K img/sec — recommended** | Retire Rekognition from this pipeline's hot path entirely at this volume; fold logo + OCR into the combined SageMaker fleet in §5 (already costed there — **$0 incremental** here). Rekognition remains a reasonable *low-volume* choice (a pilot region, a long-tail fallback for frames the primary model rejects) — the finding is about volume, not about the service being bad. |

## 7. Amazon DynamoDB — structured extraction landing table

| | |
|---|---|
| **Why chosen** | Extraction output is high-volume, per-field, and variable-shape (0–3 extraction rows per image, each with its own confidence/model-version) — a fast key-value append fits better than forcing every write through a relational schema before it's even curated. No joins are needed at this stage. |
| **Pros** | No connection limits (HTTP-based), on-demand mode needs no capacity planning for typical traffic, scales writes horizontally via partition key with no manual sharding. |
| **Cons/trade-offs** | On-demand write pricing is **5× read pricing per request**, and this table is write-heavy by construction (every extraction is a write, few are ever re-read individually) — that asymmetry is exactly why write volume dominates this line item. |
| **Scalability** | On-demand mode auto-scales, but **only up to ~2× the previous 30-minute peak** before throttling — a true instantaneous jump to 10,000 img/sec from cold would throttle; sustained/ramped load (which "sustained 10,000/sec" implies) is fine. Partition key must be high-cardinality (e.g., `detection_id`) to avoid a hot-partition ceiling regardless of table-level throughput numbers. |
| **Availability** | Multi-AZ by default, no user action; a regional DynamoDB event backs up the upstream SQS queue (§8) rather than dropping data, since consumers simply stop making progress until it recovers. |
| **Cost at 10K img/sec** | **Writes**: 25.92B images × 2.5 extraction rows/image = 64.8B writes/month × ($1.25/M) → 64,800 × $1.25 = **$81,000/month**. **Reads**: ~1 aggregate read per image by the resolver = 25.92B × $0.25/M → 25,920 × $0.25 = **$6,480/month**. **Storage** (3-day TTL — this is a transient landing buffer, not the system of record): steady-state items ≈ 64.8B × (3/30) = 6.48B × 1 KB = 6.48 TB ≈ 6,480 GB × $0.25/GB = **$1,620/month**. **Total: $89,100/month.** |

## 8. Amazon SQS — `detections-ready` queue + DLQ

| | |
|---|---|
| **Why chosen** | The identity-resolution stage's real bottleneck is an **external dependency it doesn't control** (SAFER, §14). A queue is what lets Genlogs throttle outbound calls independently of inbound extraction volume, retry with backoff, and dead-letter permanently-failing lookups instead of losing them or blocking the pipeline behind a slow/rate-limited external API. |
| **Pros** | Fully decouples producer (extraction) rate from consumer (resolution) rate; DLQ gives a durable record of what failed and why, for reprocessing once the root cause (e.g., a SAFER outage) clears. |
| **Cons/trade-offs** | Adds end-to-end latency (seconds-to-minutes) between "extracted" and "resolved" — acceptable for a historical lane-volume portal query, not for anything claiming real-time tracking. Standard queues (used here, not FIFO) don't guarantee ordering — fine, since detections don't need to resolve in capture order. |
| **Scalability** | Effectively unlimited throughput; no partitioning/sharding concept for the caller to manage (unlike Kinesis). |
| **Availability** | Multi-AZ, redundant by default; messages persist until explicitly deleted by a consumer, so a downstream outage (identity resolver down) just grows queue depth rather than losing data, up to the retention window (default 4 days, configurable to 14). |
| **Cost at 10K img/sec** | One message per resolved detection event ≈ 25.92B/month × $0.40/M → 25,920 × $0.40 = **$10,368/month**. DLQ at an assumed <1% terminal-failure rate: 259.2M × $0.40/M = **$104/month**. **Total: $10,472/month.** |

## 9. AWS Lambda / Fargate — identity-resolution workers

| | |
|---|---|
| **Why chosen (Fargate over Lambda here)** | Each resolution call needs a warm connection/cache client and does mostly I/O-bound work (cache lookup, occasional external HTTP call) — a small, long-running Fargate task pool amortizes connection setup and cache-client overhead across many messages, whereas Lambda would pay cold-start and connection-setup cost per invocation at this call frequency. |
| **Pros** | No server patching; scales by adjusting task count/CPU-mem, not by re-architecting; easier to keep a persistent SAFER-cache client and outbound connection pool warm than in Lambda's per-invocation model. |
| **Cons/trade-offs** | Requires managing a task-count scaling policy (target-tracking on queue depth) — more operational surface than Lambda's built-in concurrency scaling; Lambda would in fact be perfectly adequate here too if using an SDK HTTP keep-alive client and provisioned concurrency — this is a legitimate "either works" choice, not a hard requirement. |
| **Scalability** | Scale by Fargate task count against `ApproximateNumberOfMessagesVisible` on the SQS queue (§8); ultimate throughput ceiling is the **cache/SAFER dependency**, not Fargate itself. |
| **Availability** | Tasks run across multiple AZs behind the service's task placement; an AZ failure is absorbed by rescheduling tasks in healthy AZs, with in-flight messages simply becoming visible again in SQS for another worker to pick up (SQS visibility timeout handles this natively — no custom failover logic needed). |
| **Cost at 10K img/sec** | Assumption: 1 vCPU / 2 GB tasks, ~50 msgs/sec/task sustained (mostly cache-hit fast path per the 99% hit-rate assumption). Tasks needed: 10,000 ÷ 50 = **200**, running continuously. Cost: 200 × (1 × $0.04048 + 2 × $0.004445)/hr × 730 hr = 200 × $0.04937 × 730 = **$7,208/month.** |

## 10. DynamoDB vs. Aurora — USDOT cache (recommendation: **DynamoDB**)

| | |
|---|---|
| **Why DynamoDB, not Aurora, is the primary recommendation** | This is a pure key→record lookup by `usdot_number` with no joins — exactly DynamoDB's sweet spot. More importantly at this concurrency: DynamoDB has **no connection concept** (plain HTTPS requests), while Aurora Postgres has a hard `max_connections` ceiling per instance size that 200 concurrent Fargate tasks (each pooling multiple connections) would need an RDS Proxy layer to manage safely — that's an extra moving part and failure mode DynamoDB avoids entirely. |
| **Pros (DynamoDB)** | No connection-pool exhaustion risk under high worker concurrency; scales read throughput horizontally with no admin action; pay-per-request matches the workload's spiky, cache-hit-dominated access pattern. |
| **Cons/trade-offs** | No SQL/joins if the cache record ever needs to be queried by anything other than USDOT number (acceptable — it's a cache, not a system of record; the warehouse in §13 is where relational querying happens). |
| **Scalability** | Same on-demand ramp behavior/limits as §7; a bounded, slowly-changing key space (~2M USDOT numbers) means traffic is naturally spread across many keys, which is the ideal on-demand access pattern. |
| **Availability** | Multi-AZ by default; a regional outage degrades to "every lookup falls through to a cache miss," which just means every identity resolution calls SAFER directly — exactly the scenario §14's rate-limit risk describes, so cache availability matters more than its raw cost. |
| **Cost at 10K img/sec (DynamoDB)** | Reads: 25.92B lookups/month, eventually-consistent (0.5 RRU per ≤4 KB item) = 12.96B RRU-equivalent → 12,960 (millions) × $0.25 = **$3,240/month**. Writes (1% cache misses/refreshes): 259.2M × $1.25/M = **$324/month**. Storage: ~2M carriers × 2 KB ≈ 4 GB, negligible (**~$1/month**). **Total: ~$3,565/month.** |
| **For comparison — Aurora Serverless v2 path** | Sized for the same 25.92B lookups/month (~10,000 QPS): assume ~1,000 simple indexed-PK reads/sec/ACU → ~16 ACUs sustained (with headroom for connection overhead) × $0.12/ACU-hr × 730 hr = **$1,402/month** compute + I/O at $0.20/M requests → 25,920 × $0.20 = **$5,184/month** + storage (~$0.40/month) ≈ **$6,586/month total** — nearly 2× the DynamoDB cost *before* counting the RDS Proxy this concurrency level would need operationally. Aurora remains the right call if the cache record ever needs relational querying; it isn't needed here. |

## 11. Amazon S3 — curated zone (resolved facts, pre-warehouse)

| | |
|---|---|
| **Why chosen** | Same rationale as the raw zone (§2): durable, cheap staging that the warehouse loader (§12) reads in batches, and a source of truth for reloading the warehouse if it ever needs to be rebuilt. |
| **Pros** | Decouples "identity resolution wrote a fact" from "the warehouse has ingested it" — a Redshift maintenance window or load failure doesn't block resolution workers. |
| **Cons/trade-offs** | If every resolved record is written as its own object (1 PUT/record), request cost scales identically to the raw zone despite the payload being ~1,000× smaller — a pure waste that batching fixes for free. |
| **Scalability** | Same as §2 — effectively unlimited; the only real lever is request count via batching. |
| **Availability** | Same as §2 — 99.99% design availability, multi-AZ. |
| **Cost at 10K img/sec — as drawn (1 PUT/record)** | 25.92B records/month × $5/M = **$129,600/month** — needlessly identical to the raw zone's PUT cost despite ~1,000× smaller payloads. |
| **Cost at 10K img/sec — recommended (batched, 10,000 records/file)** | Files/month: 25.92B ÷ 10,000 = 2.592M × $5/M = **$12.96/month** for PUTs. **Storage** (90-day rolling, 2 KB/record pre-compression): 25.92B × 2 KB = 51.84 TB/month generated → steady state ≈ 3× that = 155.52 TB ≈ 155,520 GB × $0.023 = **$3,577/month**. GETs (batched reads by Glue) are negligible (~$1/month). **Total: ~$3,591/month** — batching writes is the single highest-leverage cost fix in this entire pipeline (36× reduction on this line alone). |

## 12. AWS Glue — micro-batch loader into Redshift

| | |
|---|---|
| **Why chosen** | Redshift is optimized for bulk columnar loads (`COPY`), not row-by-row upserts; Glue is the managed way to run a scheduled/continuous batch job that reads curated S3 files and issues bulk loads on a short cycle, keeping the warehouse workload in the pattern it's built for. |
| **Pros** | Serverless (no cluster to manage), scales by adding DPUs, integrates natively with S3 and Redshift `COPY`/JDBC. |
| **Cons/trade-offs** | Billed per-DPU-hour whether or not there's a full batch waiting — a continuously-running job at low utilization wastes DPU-hours; a purely scheduled (e.g., every 5 min) job trades latency for lower idle cost. At this volume, continuous running is justified by throughput, not by minimizing idle time. |
| **Scalability** | Scale by DPU count; this is a manual/config lever (or Glue Auto Scaling within a job run), not fully automatic across job runs. |
| **Availability** | Managed, retries failed job runs; a Glue outage delays warehouse freshness (curated S3 data is untouched and safe) rather than losing data. |
| **Cost at 10K img/sec** | Assumption: a continuously-running job sized at **20 DPUs** to sustain ~10,000 records/sec load throughput (stated assumption — needs a load test to confirm real per-DPU throughput for this record shape). 20 × $0.44/DPU-hr × 730 hr = **$6,424/month.** |

## 13. Amazon Redshift — analytics warehouse

| | |
|---|---|
| **Why chosen** | The point-4 "top carriers on lane X→Y" query and any future analytics need SQL joins across `detection_event`, `vehicle`, `carrier`, `lane` — a relational/columnar warehouse is the right shape, and Redshift is the AWS-native option (Snowflake is an equally valid substitute, called out in the platform-architecture doc). |
| **Pros** | Columnar storage compresses the fact table well (assumed ~4× here), MPP query execution suits the "group by carrier, filter by lane and date" access pattern well, Serverless removes cluster-sizing guesswork for a workload whose query concurrency is still unknown. |
| **Cons/trade-offs** | Sizing for "continuous high-throughput load + concurrent ad hoc analytics" simultaneously is inherently rougher than the request-based services above — real capacity needs a load test, not just arithmetic. Provisioned RA3 nodes with Reserved Instance pricing would very likely beat Serverless on cost once the load pattern is well understood and stable — Serverless is the right *starting* choice for an unproven access pattern, not necessarily the final one. |
| **Scalability** | Redshift Serverless scales base/max RPU (4–1,024 RPU range); provisioned clusters scale by adding RA3 nodes (also supports elastic resize). |
| **Availability** | Multi-AZ within a region for Serverless; provisioned clusters support multi-AZ with automated failover. A Redshift outage delays analytics freshness only — curated S3 data (§11) is untouched, so no data is lost, only queryable-latency increases. |
| **Cost at 10K img/sec** | Storage: ~200 bytes/row compressed × 25.92B rows/month = 5.184 TB/month raw → ÷4 compression ≈ 1.3 TB/month compressed; over 24-month retention ≈ 31.2 TB steady state ≈ 31,200 GB × $0.024/GB (RMS) = **$749/month**. Compute: sized at an assumed **64 RPU sustained** (stated assumption, to keep up with continuous micro-batch loads plus concurrent portal queries — needs load-testing to confirm) × $0.375/RPU-hr × 730 hr = **$17,520/month**. **Total: ~$18,269/month.** For comparison, 2× `ra3.4xlarge` provisioned nodes would run ~$4,760/month on-demand — cheaper, but a fixed 2-node cluster is unlikely to sustain this ingest+query load; right-sizing (Serverless RPU vs. provisioned node count) should be settled with a load test before committing to either number. |

## 14. SAFER FMCSA API — external identity source (non-AWS)

| | |
|---|---|
| **Why used** | It's the only authoritative source tying a USDOT number to a carrier's legal identity/operating status — there's no substitute if the goal is verified carrier identity, not just a self-reported label. |
| **AWS cost** | **$0** — it's an external government API, not billed through AWS. |
| **The real "cost": rate-limit risk** | At a 99% cache hit rate (per assumption), the resolver still needs **259.2M live SAFER calls/month ≈ 100 requests/sec sustained average** (and bursty above that during cache-cold periods). SAFER's public Web Services API requires developer registration (a "webkey") and has no published high-throughput SLA — it is built for occasional per-company lookups, not a sustained 3-digit-per-second production load. This is very likely to get Genlogs' key throttled or blocked well before the AWS side of the pipeline becomes the bottleneck, **independent of how well the DynamoDB cache in §10 is tuned**. |
| **Fix — don't call it live at this volume** | FMCSA publishes a **bulk census/snapshot data file** of registered carriers. At real production scale, mirror that file into the carrier reference table on a scheduled basis (e.g., nightly) instead of resolving each new detection against the live API; reserve the live single-record API for the rare truly-new USDOT number not yet in the nightly mirror (a handful of calls/day, not 100/sec). This is the single most important non-AWS finding in this analysis: **caching harder doesn't fix this — the identity-resolution design itself needs to shift from "call SAFER per detection" to "mirror SAFER, call it rarely."** |

---

## Cost summary (recommended architecture at 10,000 img/sec)

| # | Component | As-drawn cost/mo | Recommended cost/mo | Why they differ |
|---|---|---:|---:|---|
| 1 | Ingestion (API GW/IoT Core) | $62,343 (and likely undeployable — payload/timeout limits) | $11 | Direct-to-S3 presigned PUT; API GW/IoT Core reduced to control-plane only |
| 2 | S3 raw zone | $418,176 | $418,176 | Unchanged — this is a real cost floor of storing every frame |
| 3 | EventBridge | $25,920 | $10,368 | Replaced by native S3→SQS event delivery |
| 4 | Step Functions | $3,888,000 (and likely undeployable — StartExecution quota) | $48,384 | Replaced by a plain Lambda dispatcher |
| 5 | SageMaker (combined OCR+logo fleet) | — | $358,368 | New: absorbs §6's workload |
| 6 | Rekognition (OCR + logo, as-drawn) | $12,960,000+ (and likely blocked — Custom Labels unit quotas) | $0 | Folded into §5's self-hosted fleet |
| 7 | DynamoDB (extraction landing) | $89,100 | $89,100 | Unchanged |
| 8 | SQS (detections-ready + DLQ) | $10,472 | $10,472 | Unchanged |
| 9 | Lambda/Fargate (identity resolution) | $7,208 | $7,208 | Unchanged |
| 10 | DynamoDB (USDOT cache) | $3,565 | $3,565 | Unchanged (chosen over Aurora's ~$6,586 equivalent) |
| 11 | S3 curated zone | $129,600 (1 PUT/record) | $3,591 | Batched writes (10,000 records/file) |
| 12 | Glue (micro-batch loader) | $6,424 | $6,424 | Unchanged |
| 13 | Redshift | $18,269 | $18,269 | Unchanged (needs load-test validation either way) |
| 14 | SAFER FMCSA API | $0 (but ~100 req/s sustained live, high real-world block risk) | $0 (nightly bulk mirror; live calls only for true cache misses) | Rate-limit risk, not AWS cost |
| | **Total** | **~$17.6M/month (largely undeployable as drawn)** | **≈ $973,945/month** | |

**Top cost drivers (recommended architecture):** **S3 raw zone** ($418,176, 43%) and the **SageMaker ML
extraction fleet** ($358,368 + $48,384 dispatcher = $406,752, 42%) together account for **~85%** of the
total; add **DynamoDB extraction landing** ($89,100, 9%) and three line items explain **~94%** of the
bill. Everything else — queues, warehouse, cache, orchestration glue — is comparatively small once the
Step Functions/Rekognition/EventBridge/API-Gateway fixes are applied. This is the expected shape for a
pipeline whose fundamental cost driver is "store and run ML inference on every single frame" — no
service substitution below §2/§5 changes that; only capturing fewer, better-triggered frames would.

**Not included above (flagged, not zero):** NAT Gateway/data-transfer costs for the Fargate/Lambda
workers' outbound calls to SAFER and to each other's VPC endpoints; CloudWatch Logs/Metrics at this
event volume (verbose logging on 25.92B events/month would itself be a non-trivial bill); AWS Support
plan tier appropriate for a workload this size; the ML-ops cost of training/maintaining the SageMaker
model outside AWS infrastructure billing. These should be estimated separately once the deployment tier
is chosen (delivery-manager's remit), not folded into this service-selection analysis.

## Sources

Pricing pulled via web search against AWS pricing pages and pricing-tracking sites, August 2026,
us-east-1: [API Gateway](https://aws.amazon.com/api-gateway/pricing/) ·
[IoT Core](https://aws.amazon.com/iot-core/pricing/) ·
[S3](https://aws.amazon.com/s3/pricing/) ·
[EventBridge](https://aws.amazon.com/eventbridge/pricing/) ·
[Step Functions](https://aws.amazon.com/step-functions/pricing) ·
[SageMaker](https://aws.amazon.com/sagemaker/ai/pricing/) ·
[Rekognition](https://aws.amazon.com/rekognition/pricing/) ·
[DynamoDB](https://aws.amazon.com/dynamodb/pricing/on-demand/) ·
[SQS](https://www.amazonaws.cn/en/sqs/pricing/) ·
[Lambda](https://aws.amazon.com/lambda/pricing/) ·
[Fargate](https://aws.amazon.com/fargate/pricing/) ·
[Aurora](https://aws.amazon.com/rds/aurora/pricing/) ·
[Glue](https://www.amazonaws.cn/en/glue/pricing/) ·
[Redshift](https://aws.amazon.com/redshift/pricing/) ·
[FMCSA Developer Portal](https://mobile.fmcsa.dot.gov/QCDevsite/docs/apiAccess) (SAFER API access/rate-limit
context). Where third-party aggregator sites and official AWS pages showed slightly different numbers
(e.g., DynamoDB on-demand rates), the figure corroborated by multiple independent sources was used and
is noted inline.
