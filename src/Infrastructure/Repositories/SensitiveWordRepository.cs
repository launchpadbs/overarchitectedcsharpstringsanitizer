using System.Data;
using Dapper;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using FlashAssessment.Application.Words;
using FlashAssessment.Domain.Common;
using FlashAssessment.Domain.Words;
using FlashAssessment.Infrastructure.Database;
using Microsoft.Data.SqlClient;
using Polly;
using FlashAssessment.Application.Common;

namespace FlashAssessment.Infrastructure.Repositories;

public sealed class SensitiveWordRepository : ISensitiveWordRepository
{
    private readonly SqlConnectionFactory _connectionFactory;
    private readonly Microsoft.Extensions.Logging.ILogger<SensitiveWordRepository> _logger;
    private readonly ResilienceOptions _resilience;
    private readonly IAsyncPolicy _readRetryPolicy;
    private readonly IAsyncPolicy _writeRetryPolicy;
    private readonly IAsyncPolicy _circuitBreakerPolicy;
    private const int CommandTimeoutSeconds = 30;
    private static readonly ActivitySource Activity = new("FlashAssessment");
    private static readonly Meter Meter = new("FlashAssessment.Infrastructure", "1.0.0");
    private static readonly Counter<long> CircuitBreaks = Meter.CreateCounter<long>("db.circuit.breaks");
    private static readonly Counter<long> CircuitResets = Meter.CreateCounter<long>("db.circuit.resets");
    private static readonly Counter<long> CircuitHalfOpens = Meter.CreateCounter<long>("db.circuit.halfopen");
    private static int _circuitState; // 0=Closed,1=HalfOpen,2=Open
    private static readonly ObservableGauge<int> CircuitStateGauge = Meter.CreateObservableGauge("db.circuit.state", () => _circuitState);

    public SensitiveWordRepository(SqlConnectionFactory connectionFactory, Microsoft.Extensions.Options.IOptions<ResilienceOptions> resilience, Microsoft.Extensions.Logging.ILogger<SensitiveWordRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _resilience = resilience.Value;
        _logger = logger;

        _readRetryPolicy = Policy
            .Handle<SqlException>()
            .Or<TimeoutException>()
            .WaitAndRetryAsync(_resilience.ReadRetryCount, attempt => TimeSpan.FromMilliseconds(_resilience.BaseDelayMs * attempt));

        _writeRetryPolicy = Policy
            .Handle<SqlException>()
            .Or<TimeoutException>()
            .WaitAndRetryAsync(_resilience.WriteRetryCount, attempt => TimeSpan.FromMilliseconds(_resilience.BaseDelayMs * attempt));

        _circuitBreakerPolicy = Policy
            .Handle<SqlException>()
            .Or<TimeoutException>()
            .CircuitBreakerAsync(
                exceptionsAllowedBeforeBreaking: _resilience.CircuitFailures,
                durationOfBreak: TimeSpan.FromSeconds(_resilience.BreakSeconds),
                onBreak: (ex, ts) =>
                {
                    System.Threading.Volatile.Write(ref _circuitState, 2);
                    CircuitBreaks.Add(1);
                    _logger.LogWarning(ex, "DB circuit OPEN for {BreakSeconds}s due to {Error}", ts.TotalSeconds, ex.Message);
                },
                onReset: () =>
                {
                    System.Threading.Volatile.Write(ref _circuitState, 0);
                    CircuitResets.Add(1);
                    _logger.LogInformation("DB circuit RESET (Closed)");
                },
                onHalfOpen: () =>
                {
                    System.Threading.Volatile.Write(ref _circuitState, 1);
                    CircuitHalfOpens.Add(1);
                    _logger.LogInformation("DB circuit HALF-OPEN (trial)");
                }
            );
    }

    public async Task<long> CreateAsync(SensitiveWord word, IDbTransaction? tx = null, CancellationToken cancellationToken = default)
    {
        using var activity = Activity.StartActivity("SensitiveWordRepository.CreateAsync", ActivityKind.Client);
        activity?.SetTag("db.system", "mssql");
        activity?.SetTag("db.operation", "INSERT");
        activity?.SetTag("db.table", "dbo.SensitiveWord");
        activity?.SetTag("app.hasTransaction", tx is not null);

        const string sql = @"-- Insert sensitive word
INSERT INTO dbo.SensitiveWord (Word, NormalizedWord, Category, Severity, IsActive)
OUTPUT INSERTED.SensitiveWordId
VALUES (@Word, @NormalizedWord, @Category, @Severity, @IsActive);";

        if (tx is not null)
        {
            var connection = tx.Connection!;
            var id = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                sql,
                new
                {
                    word.Word,
                    word.NormalizedWord,
                    word.Category,
                    word.Severity,
                    word.IsActive
                },
                transaction: tx,
                commandTimeout: CommandTimeoutSeconds,
                cancellationToken: cancellationToken));
            activity?.SetTag("db.rows", 1);
            return id;
        }
        else
        {
            // Apply Polly for transient faults
            using var connection = _connectionFactory.CreateOpenConnection();
            try
            {
                long id = 0;
                await _circuitBreakerPolicy.ExecuteAsync(async () =>
                {
                    await _writeRetryPolicy.ExecuteAsync(async () =>
                    {
                        id = await connection.ExecuteScalarAsync<long>(new CommandDefinition(
                            sql,
                            new
                            {
                                word.Word,
                                word.NormalizedWord,
                                word.Category,
                                word.Severity,
                                word.IsActive
                            },
                            transaction: null,
                            commandTimeout: CommandTimeoutSeconds,
                            cancellationToken: cancellationToken));
                    });
                });
                activity?.SetTag("db.rows", 1);
                return id;
            }
            catch (SqlException ex) when (ex.Number is 2601 or 2627)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw new DuplicateException("A word with the same NormalizedWord already exists.");
            }
        }
    }

    public async Task<SensitiveWord?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        using var activity = Activity.StartActivity("SensitiveWordRepository.GetByIdAsync", ActivityKind.Client);
        activity?.SetTag("db.system", "mssql");
        activity?.SetTag("db.operation", "SELECT");
        activity?.SetTag("db.table", "dbo.SensitiveWord");
        activity?.SetTag("app.id", id);

        const string sql = @"SELECT SensitiveWordId, Word, NormalizedWord, Category, Severity, IsActive, CreatedUtc, RowVersion
FROM dbo.SensitiveWord WITH (READCOMMITTEDLOCK) WHERE SensitiveWordId = @Id;";

        using var connection = _connectionFactory.CreateOpenConnection();
        SensitiveWord? result = null;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await _circuitBreakerPolicy.ExecuteAsync(async () =>
        {
            await _readRetryPolicy.ExecuteAsync(async () =>
            {
                result = await connection.QuerySingleOrDefaultAsync<SensitiveWord>(new CommandDefinition(
                    sql, new { Id = id }, commandTimeout: CommandTimeoutSeconds, cancellationToken: cancellationToken));
            });
        });
        sw.Stop();
        if (sw.ElapsedMilliseconds > _resilience.SlowQueryMs)
        {
            _logger.LogWarning("Slow query {Method} took {ElapsedMs}ms for Id={Id}", nameof(GetByIdAsync), sw.ElapsedMilliseconds, id);
        }
        activity?.SetTag("db.query.elapsed_ms", sw.ElapsedMilliseconds);
        return result;
    }

    public async Task<(IReadOnlyList<SensitiveWord> Items, int TotalCount)> ListAsync(string? search, bool? isActive, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        using var activity = Activity.StartActivity("SensitiveWordRepository.ListAsync", ActivityKind.Client);
        activity?.SetTag("db.system", "mssql");
        activity?.SetTag("db.operation", "SELECT");
        activity?.SetTag("db.table", "dbo.SensitiveWord");
        activity?.SetTag("app.search", string.IsNullOrWhiteSpace(search) ? null : search);
        activity?.SetTag("app.isActive", isActive);
        activity?.SetTag("app.page", page);
        activity?.SetTag("app.pageSize", pageSize);

        const string sql = @";WITH Filtered AS (
    SELECT SensitiveWordId, Word, NormalizedWord, Category, Severity, IsActive, CreatedUtc, RowVersion
    FROM dbo.SensitiveWord WITH (READCOMMITTEDLOCK)
    WHERE (@SearchLike IS NULL OR Word LIKE @SearchLike OR Category LIKE @SearchLike)
      AND (@IsActive IS NULL OR IsActive = @IsActive)
)
SELECT COUNT(1) FROM Filtered;

;WITH Filtered AS (
    SELECT SensitiveWordId, Word, NormalizedWord, Category, Severity, IsActive, CreatedUtc, RowVersion
    FROM dbo.SensitiveWord WITH (READCOMMITTEDLOCK)
    WHERE (@SearchLike IS NULL OR Word LIKE @SearchLike OR Category LIKE @SearchLike)
      AND (@IsActive IS NULL OR IsActive = @IsActive)
)
SELECT SensitiveWordId, Word, NormalizedWord, Category, Severity, IsActive, CreatedUtc, RowVersion
FROM Filtered
ORDER BY SensitiveWordId
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

        using var connection = _connectionFactory.CreateOpenConnection();
        int total = 0;
        List<SensitiveWord> items = new();
        await _circuitBreakerPolicy.ExecuteAsync(async () =>
        {
            await _readRetryPolicy.ExecuteAsync(async () =>
            {
                using var multi = await connection.QueryMultipleAsync(new CommandDefinition(sql, new
                {
                    SearchLike = string.IsNullOrWhiteSpace(search) ? null : $"%{search}%",
                    IsActive = isActive,
                    Offset = (page - 1) * pageSize,
                    PageSize = pageSize
                }, commandTimeout: CommandTimeoutSeconds, cancellationToken: cancellationToken));

                total = await multi.ReadSingleAsync<int>();
                items = (await multi.ReadAsync<SensitiveWord>()).ToList();
            });
        });
        activity?.SetTag("db.rows", items.Count);
        activity?.SetTag("db.total", total);
        return (items, total);
    }

    public async Task UpdateAsync(SensitiveWord word, byte[] rowVersion, IDbTransaction? tx = null, CancellationToken cancellationToken = default)
    {
        using var activity = Activity.StartActivity("SensitiveWordRepository.UpdateAsync", ActivityKind.Client);
        activity?.SetTag("db.system", "mssql");
        activity?.SetTag("db.operation", "UPDATE");
        activity?.SetTag("db.table", "dbo.SensitiveWord");
        activity?.SetTag("app.id", word.SensitiveWordId);
        activity?.SetTag("app.hasTransaction", tx is not null);

        const string sql = @"UPDATE dbo.SensitiveWord SET Word = @Word, NormalizedWord = @NormalizedWord, Category = @Category, Severity = @Severity, IsActive = @IsActive
WHERE SensitiveWordId = @SensitiveWordId AND RowVersion = @RowVersion;
SELECT @@ROWCOUNT;";

        if (tx is not null)
        {
            var connection = tx.Connection!;
            var affected = await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql, new
            {
                word.Word,
                word.NormalizedWord,
                word.Category,
                word.Severity,
                word.IsActive,
                word.SensitiveWordId,
                RowVersion = rowVersion
            }, transaction: tx, commandTimeout: CommandTimeoutSeconds, cancellationToken: cancellationToken));
            if (affected == 0)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "Concurrency conflict");
                throw new ConcurrencyException("Update conflict for SensitiveWord.");
            }
        }
        else
        {
            using var connection = _connectionFactory.CreateOpenConnection();
            int affected = 0;
            await _circuitBreakerPolicy.ExecuteAsync(async () =>
            {
                await _writeRetryPolicy.ExecuteAsync(async () =>
                {
                    affected = await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql, new
                    {
                        word.Word,
                        word.NormalizedWord,
                        word.Category,
                        word.Severity,
                        word.IsActive,
                        word.SensitiveWordId,
                        RowVersion = rowVersion
                    }, transaction: null, commandTimeout: CommandTimeoutSeconds, cancellationToken: cancellationToken));
                });
            });
            if (affected == 0)
            {
                activity?.SetStatus(ActivityStatusCode.Error, "Concurrency conflict");
                throw new ConcurrencyException("Update conflict for SensitiveWord.");
            }
        }
    }

    public async Task<bool> DeleteAsync(long id, IDbTransaction? tx = null, CancellationToken cancellationToken = default)
    {
        using var activity = Activity.StartActivity("SensitiveWordRepository.DeleteAsync", ActivityKind.Client);
        activity?.SetTag("db.system", "mssql");
        activity?.SetTag("db.operation", "DELETE");
        activity?.SetTag("db.table", "dbo.SensitiveWord");
        activity?.SetTag("app.id", id);
        activity?.SetTag("app.hasTransaction", tx is not null);

        const string sql = @"DELETE FROM dbo.SensitiveWord WHERE SensitiveWordId = @Id; SELECT @@ROWCOUNT;";
        if (tx is not null)
        {
            var connection = tx.Connection!;
            var affected = await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql, new { Id = id }, transaction: tx, commandTimeout: CommandTimeoutSeconds, cancellationToken: cancellationToken));
            var existed = affected > 0;
            activity?.SetTag("db.rows", affected);
            return existed;
        }
        else
        {
            using var connection = _connectionFactory.CreateOpenConnection();
            int affected = 0;
            await _circuitBreakerPolicy.ExecuteAsync(async () =>
            {
                await _writeRetryPolicy.ExecuteAsync(async () =>
                {
                    affected = await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql, new { Id = id }, transaction: null, commandTimeout: CommandTimeoutSeconds, cancellationToken: cancellationToken));
                });
            });
            activity?.SetTag("db.rows", affected);
            return affected > 0;
        }
    }
}


