# FlashAssessment API – How‑To Guide

This guide shows how to use the API

## Before you start
- Make sure the stack is running. If you’re using Docker, follow the steps in the main `README.md` (copy `.env.example` → `.env`, then `docker compose up --build`).
- When the API is up, open Swagger at `http://localhost:8080/swagger`.

## What this service does
- You send a piece of text.
- The service returns the same text with sensitive words masked (for example, replaced with `*`).
- You can also manage the list of sensitive words (add, read, update, delete) that power the sanitizer.

## Using Swagger (easiest)
1) Go to `http://localhost:8080/swagger`.
2) Expand an endpoint, click “Try it out”, fill in the request, and hit “Execute”.
3) You’ll see the request/response and can repeat as needed.

Tips:
- Content type must be `application/json` for POST/PUT.
- If API key auth is enabled, add header `X-Api-Key: your-key`.
- Every response includes a correlation id (also in `X-Correlation-Id`) to help trace logs.

## Common tasks (with examples)

### 1) Sanitize some text
Send a POST to `/api/v1/sanitize` with your text. Options are optional.

Minimal:
```bash
curl -X POST http://localhost:8080/api/v1/sanitize \
  -H "Content-Type: application/json" \
  -d '{
    "text": "You need to create a full string"
  }'
```

With options:
```bash
curl -X POST http://localhost:8080/api/v1/sanitize \
  -H "Content-Type: application/json" \
  -d '{
    "text": "You need to create a full string",
    "options": {
      "maskChar": "*",
      "strategy": "FullMask",
      "wholeWordOnly": true,
      "caseSensitive": false,
      "preserveCasing": false
    }
  }'
```

You’ll get back JSON like:
```json
{
  "sanitizedText": "You need to ****** a **** string",
  "matched": [
    { "word": "create", "start": 12, "length": 6 },
    { "word": "full", "start": 21, "length": 4 }
  ],
  "elapsedMs": 0.42
}
```

Notes:
- `maskChar` must be a single character string (for example "*" or "#").
- `strategy` can be `FullMask`, `FirstLastOnly`, `FixedLength`, or `Hash`.
- `FixedLength` and `Hash` change the length of the text where matches occur; others don’t.

### 2) Add a sensitive word
```bash
curl -X POST http://localhost:8080/api/v1/words \
  -H "Content-Type: application/json" \
  -d '{
    "word": "create",
    "category": "General",
    "severity": 2,
    "isActive": true
  }'
```
Response: `201 Created` with a `Location` header pointing to the new resource.

### 3) Get a word by id
```bash
curl http://localhost:8080/api/v1/words/1
```
Response: `200 OK` with the word details, or `404 Not Found`.

### 4) List words with search and paging
```bash
curl "http://localhost:8080/api/v1/words?search=create&isActive=true&page=1&pageSize=50"
```
Response: `200 OK` with a list, and `X-Total-Count` header for total items.

### 5) Update a word (optimistic concurrency)
Get the item first to read its `rowVersion`, then send PUT including the same value. If it changed, you’ll get `409 Conflict`.
```bash
curl -X PUT http://localhost:8080/api/v1/words/1 \
  -H "Content-Type: application/json" \
  -d '{
    "word": "create",
    "category": "General",
    "severity": 3,
    "isActive": true,
    "rowVersion": "AAAAAAAAB9E="
  }'
```
Response: `204 No Content` on success.

### 6) Delete a word
```bash
curl -X DELETE http://localhost:8080/api/v1/words/1
```
Response: `204 No Content` if it existed, `404 Not Found` otherwise.

### 7) Manage rules (optional)
Rules let you fine‑tune matching (whole‑word only, case sensitivity, etc.). Endpoints follow the pattern:
- `POST /api/v1/words/{id}/rules`
- `GET /api/v1/words/{id}/rules`
- `PUT /api/v1/rules/{ruleId}`
- `DELETE /api/v1/rules/{ruleId}`

## Headers you may need
- `Content-Type: application/json` for POST/PUT.
- `X-Api-Key: your-key` when auth is enabled.
- `X-Correlation-Id: your-id` (optional). If omitted, the server will generate one.

## How errors look
The API uses a standard error shape (ProblemDetails). Example:
```json
{
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "options.maskChar": [ "maskChar must be a single character if provided." ]
  },
  "traceId": "00-aabbcc..."
}
```
- The `traceId` (and `X-Correlation-Id` header) helps you find the matching logs.

## Rate limits
- The sanitize endpoint is rate‑limited per IP (defaults are in configuration).
- If exceeded, you’ll get `429 Too Many Requests`.

## Troubleshooting
- 400 with “dto field is required”: usually means the JSON couldn’t be bound. Check for valid JSON and correct property names.
- 400 about `maskChar`: ensure it’s a single character string ("*" not "**").
- 415 Unsupported Media Type: add `Content-Type: application/json` header.
- 500 errors: check container logs; use the `traceId`/`X-Correlation-Id` to correlate.
- Ready check: `GET /health/ready` returns healthy only after the database is reachable and the cache is usable.

## Where to look next
- Full API surface and examples: Swagger at `http://localhost:8080/swagger`.
- Configuration details and environment variables: see `README.md`.
