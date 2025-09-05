This is a .NET 8 microservice that sanitizes input strings by masking sensitive words, and provides a CRUD API to manage those words in SQL Server. The service is production-ready with validation, observability, caching, resilience, and containerization.

## Architecture & Design

- Technology choices: .NET 8 Web API (C# 12), SQL Server for storage, Dapper for data access (no Entity Framework), and Swagger for interactive API docs.
- Project structure (keep things simple and testable):
  - `src/Api`: Web endpoints, Swagger, middleware (logging, errors, request checks), health checks, metrics
  - `src/Application`: Core logic (services), request/response shapes, input validation, sanitization engine, configuration options
  - `src/Domain`: Core data types and domain-specific errors
  - `src/Infrastructure`: Data access (Dapper), SQL connection factory, resiliency policies
  - `tests`: Unit tests and integration tests

### Design choices in plain language

- Database access: We talk to SQL Server using Dapper with safe, named parameters. Updates use a "row version" so we don’t overwrite someone else’s changes by accident.
- Resilience to hiccups: Brief database glitches are retried automatically. If failures keep happening, a “circuit breaker” pauses calls for a short time. We publish metrics so you can see when this occurs.
- How sanitization works:
  - Main path: one compiled pattern of all active words. It’s fast and has safety guards (non‑backtracking engine and a strict 2‑second timeout) to prevent regex attacks.
  - Fallback path: if the main pattern can’t be built or loaded, we switch to a fast multi‑word scanner that avoids backtracking entirely. We still honor whole‑word rules and don’t double‑mask overlapping matches.
- Caching: We keep the compiled pattern in memory and rebuild it only when the word list changes.
- Input safety & security: All incoming requests are validated. Only JSON is accepted. Oversized requests are rejected early (before heavy work). Errors use a consistent, simple shape. Rate limits protect the hot endpoints. Logs are sanitized so sensitive words don’t leak.
- What you can see: Logs include a correlation id so you can follow a request. Health endpoints report liveness/readiness. Metrics cover performance, cache, SQL connections, and circuit breaker state.

## Project layout

```
FlashAssessment/
  src/
    Api/
    Application/
    Domain/
    Infrastructure/
  tests/
    UnitTests/
    IntegrationTests/
  db/init/        # SQL schema + seed
  docker/         # Dockerfiles
  docker-compose.yml
```

## Run locally with Docker (step by step)

Prerequisites: Docker Desktop (Windows/macOS) or Docker Engine (Linux).

1) Copy environment file

```
cp .env.example .env
```

2) Start the full stack

```
docker compose up --build
```

What you’ll get:
- A SQL Server container (named `db`) plus a helper that creates the database and sample data (`db-init`)
- The API listening at `http://localhost:8080`
- A test runner container you can invoke on demand

3) Open Swagger

```
http://localhost:8080/swagger
```

4) Run tests in the compose network

```
docker compose run --rm tests
```

5) Tear down

```
docker compose down -v
```

For a friendly, step‑by‑step walkthrough with copy‑paste examples, see `docs/API-HOWTO.md`.

## Configuration (what the settings mean)

- `ConnectionStrings__Sql`: how the API connects to SQL Server (Docker Compose sets this automatically).
- `AUTH_ENABLED` / `API_KEY`: turn on a simple API key check (off by default for local).
- Rate limiting (per IP):
  - `RateLimiting:Sanitize:PermitLimit` and `WindowSeconds`: how many sanitize calls are allowed per time window.
  - `RateLimiting:WordsRead:*`: limits for GET endpoints.
  - `RateLimiting:WordsWrite:*`: limits for POST/PUT/DELETE endpoints.
- `Caching:ActiveWordsMinutes`: how long we keep the compiled word pattern in memory before refreshing.
- `RequestLimits:MaxJsonBytes`: maximum request size (early rejection before parsing the body).
- `Health:*`: timeouts for health checks.
- `Resilience:*`: how many retries to do, how long to wait, and when the circuit should “open”.

Environment variable examples (Docker):

- `RateLimiting__Sanitize__PermitLimit=50`
- `RateLimiting__WordsRead__PermitLimit=300`
- `RateLimiting__WordsWrite__PermitLimit=60`

## Try the API quickly

Sanitize text:

```
curl -X POST http://localhost:8080/api/v1/sanitize \
  -H "Content-Type: application/json" \
  -d '{
    "text": "You need to create a string",
    "options": { "strategy": "FullMask", "wholeWordOnly": true }
  }'
```

Add a sensitive word:

```
curl -X POST http://localhost:8080/api/v1/words \
  -H "Content-Type: application/json" \
  -d '{ "word": "create", "category": "General", "severity": 2 }'
```

## Error responses (what to expect)

- Errors use a simple, standard shape (ProblemDetails). The `instance` field contains the correlation id you’ll also see in the `X‑Correlation‑Id` header. That lets you match the response to the server logs.

## Observability & Metrics

- Sanitization
  - `sanitize.duration.ms`: end-to-end sanitization duration (milliseconds)
  - `sanitize.matches`: number of sensitive-word matches per request
  - `sanitize.cache.hits`: active-words regex cache hits
  - `sanitize.cache.misses`: active-words regex cache misses (rebuilds)
  - `sanitize.cache.evictions`: cache invalidations due to CRUD changes
  - `sanitize.errors`: unexpected errors during sanitization path
  - `sanitize.ratelimit.rejects`: rate limit sanitization rejections (HTTP 429)
- Process
  - `process.working_set.bytes`: process working set (resident memory) in bytes
- SQL connections
  - `sql.connection.opened`: cumulative connections opened by the factory
  - `sql.connection.active`: current active (checked-out) connections
- Circuit breaker
  - `db.circuit.state`: current circuit state (0=Closed, 1=Half-Open, 2=Open)
  - `db.circuit.breaks`: transitions to Open (break events)
  - `db.circuit.resets`: transitions back to Closed (reset events)
  - `db.circuit.halfopen`: transitions to Half-Open (trial state)

See `docs/PERFORMANCE-CONSIDERATIONS.md` for a plain‑language guide on further performance improvements, and `docs/ADDITIONAL-ENHANCEMENTS.md` for ideas to make the product more complete.

## Health checks

- Liveness: `/health`
- Readiness: `/health/ready` (SQL reachable + regex cache usable)

## Testing (what we cover)

- Unit tests check input validation and each masking strategy (Full‑mask, First/Last‑only, Fixed‑length, Hash), including edge cases.
- Integration tests run the full system: create/read/update/delete words (with paging and conflict checks), rate limiting behavior, cache invalidation, and end‑to‑end sanitization.

## Security (how we keep things safe)

- The main regex path has a strict 2‑second timeout and a non‑backtracking engine to prevent regex attacks.
- The fallback path uses a scanner that doesn’t backtrack and still respects word boundaries.
- Inputs are validated and must be JSON. Large requests are rejected early.
- You can enable an API key and we apply rate limits to busy endpoints.
- Logs are sanitized so sensitive words don’t leak.
