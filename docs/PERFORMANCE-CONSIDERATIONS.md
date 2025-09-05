# Performance Considerations

This document explains practical ways to make this service faster, more efficient, and more resilient under load. It focuses on low‑risk, high‑impact improvements first, and then stretches into advanced tuning you can apply as traffic grows.

## What is already done well
- Cache the compiled sensitive‑word pattern in memory and rebuild it only when the list changes.
- Use async database calls and pagination to avoid large, blocking operations.
- Keep SQL lean with Dapper (no heavy ORM), parameterized queries, and indexing.
- Protect against slow regex patterns with a non‑backtracking engine and strict timeouts.
- Fall back to a safe multi‑word scanner (Aho‑Corasick style) if the primary path can’t run.
- Rate limit hot endpoints to protect the service.
- Expose metrics so we can see performance and capacity signals.

## What to enhance next
1) Warm the cache on startup
   - Pre‑build the compiled word pattern during readiness so the first user doesn’t pay the cost.
   - Benefit: Faster first requests after deploy or restart.

2) Trim allocations on the hot path
   - Prefer spans and in‑place edits where safe; reuse buffers for masking.
   - Pool temporary buffers via `ArrayPool<char>` for the FixedLength/Hash strategies.
   - Benefit: Lower GC pressure under concurrency.

3) De‑overlap matcher results earlier
   - Drop overlapping matches as soon as we find them (greedy, longest‑match wins) to cut work later.
   - Benefit: Less post‑processing and fewer writes to the output buffer.

4) Precompute boundary rules per word
   - Merge “whole‑word only”, case‑sensitivity, and compound‑word rules into the compiled structure up front.
   - Benefit: Fewer conditional checks per match; simpler inner loop.

5) Response shaping for large payloads
   - Optionally omit the `matched` array above a size threshold or page matches in the response for very long texts.
   - Benefit: Smaller responses and less serialization time for edge‑case inputs.

6) Prepare and reuse SQL commands
   - Use prepared statements for high‑frequency queries, and tune command timeouts by operation type.
   - Benefit: Lower overhead on repeated calls.

7) Distributed cache for multi‑instance scale
   - Keep the active‑words “version” in Redis; only one instance rebuilds, others observe the version bump and pull.
   - Benefit: Consistent performance during updates with many replicas.

8) Targeted indexes and query plans
   - Validate actual query patterns in production (e.g., search on `Word` and `Category`) and add/adjust non‑clustered indexes.
   - Benefit: Stable low‑latency queries as data grows.

## Database efficiency
- Use covering indexes for common filters (`IsActive`, `NormalizedWord`, `Category`).
- Keep transactions small; do bulk work in batches with explicit timeouts.
- Avoid chatty round‑trips: prefer a single `OFFSET/FETCH` over client‑side filtering.
- Consider snapshot isolation at the DB level to reduce lock contention for reads.

## Sanitization engine efficiency
- Main path
  - Keep a single compiled pattern instead of per‑word regexes.
  - Cache by configuration “shape” (word list + flags) and invalidate via version stamps.
- Fallback path
  - Build the automaton once per version; reuse across requests.
  - Apply early boundary checks to skip impossible matches quickly.
- Masking
  - For `FullMask` and `FirstLastOnly`, mutate a char buffer in place.
  - For `FixedLength` and `Hash`, use pooled temporary buffers and reuse `StringBuilder` instances where possible.

## API & runtime efficiency
- Use gzip/brotli response compression for larger JSON payloads.
- Return `204 No Content` where appropriate to save bytes and work (e.g., successful updates/deletes).
- Tune Kestrel request body size and keep‑alive settings for your expected client behavior.
- Enable server GC and size containers with enough headroom to reduce GC frequency.
- Validate and cap `pageSize` to a safe maximum (already done); consider small defaults.

## Caching patterns
- Memory cache for the compiled pattern (already done) with a TTL “safety net”.
- Optional Redis for: pattern version, hot read‑through caches for frequent GETs, and cross‑instance coordination.
- Always include explicit invalidation hooks after CRUD writes.

## Observability‑driven tuning
- Use existing metrics to locate bottlenecks: sanitization duration, match counts, cache hit/miss, SQL latency, circuit breaker events, memory.
- Add simple logs around long‑running sanitizations (e.g., > p95) with correlation ids to see input size and strategy used.
- Run load tests that reflect your real mix (text size distribution, endpoint ratios) before and after changes.

## Capacity planning levers
- Scale up: more CPU for regex/AC matching; more memory to reduce GC frequency.
- Scale out: multiple API replicas; put the pattern version in Redis to keep them in sync.
- Database: increase DTUs/cores, tune tempdb, revisit indexes as data grows.
- Rate limits: adjust per‑IP/window caps to protect the service while being fair to clients.