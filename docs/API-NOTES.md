## API Overview

- Headers: `X-Api-Key` (optional), `X-Correlation-Id` (optional)
- Errors: RFC7807 `ProblemDetails`
  - `instance` contains the correlation id for log correlation
  - Validation errors return 400; 404 for missing; 409 for concurrency/duplicate

### Sanitization
POST `/api/v1/sanitize`

- Rate limit: default `30 req / 60s per IP` (429 on exceed)
- Configure via `RateLimiting:Sanitize:*` in settings or env vars (see README)
- Regex engine: NonBacktracking + 2s timeout (ReDoS protection)
- Degraded mode: Aho–Corasick fallback if regex/cache unavailable

### Words
CRUD under `/api/v1/words`

- Rate limits (per IP):
  - Reads (GET list/get): default `120 req / 60s`
  - Writes (POST/PUT/DELETE): default `30 req / 60s`
- Configure via `RateLimiting:WordsRead:*` and `RateLimiting:WordsWrite:*`
 - Optimistic concurrency via `RowVersion`; 409 on conflict


