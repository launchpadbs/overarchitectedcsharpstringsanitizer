## Schema

- SensitiveWord: stores words with rowversion for optimistic concurrency
- SanitizationRule: per-word rules
- Idempotency: store key and payload

## Indexes

- Unique on `SensitiveWord(NormalizedWord)` filtered by `IsActive=1`
- Nonclustered on `SensitiveWord(IsActive)` and `SanitizationRule(WordId, IsActive)`

## Connection & Resilience

- Connections created via factory per operation; pooling handled by SqlClient
- Polly CircuitBreaker with metrics and logs (`db.circuit.*`)
- Retries for transient `SqlException`/timeouts


