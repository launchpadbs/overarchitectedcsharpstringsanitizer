using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using System.Diagnostics.Metrics;
using FlashAssessment.Application.Common;

namespace FlashAssessment.Infrastructure.Database;

public sealed class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly string _connectionString;
    private readonly TimeSpan _commandTimeout;
    private readonly ILogger<SqlConnectionFactory>? _logger;
    private static readonly Meter Meter = new("FlashAssessment.Infrastructure", "1.0.0");
    private static readonly Counter<long> ConnectionsOpened = Meter.CreateCounter<long>("sql.connection.opened");
    private static readonly ObservableGauge<long> ConnectionsActiveGauge = Meter.CreateObservableGauge("sql.connection.active", () => System.Threading.Interlocked.Read(ref _activeConnections));
    private static long _activeConnections;

    public SqlConnectionFactory(string connectionString, TimeSpan? commandTimeout = null, ILogger<SqlConnectionFactory>? logger = null)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        _commandTimeout = commandTimeout ?? TimeSpan.FromSeconds(30);
        _logger = logger;
    }

    public IDbConnection CreateOpenConnection()
    {
        var inner = new SqlConnection(_connectionString)
        {
            ConnectionString = _connectionString
        };
        inner.Open();

        _logger?.LogInformation("Opened SQL connection");
        System.Threading.Interlocked.Increment(ref _activeConnections);
        ConnectionsOpened.Add(1);

        return new TrackedDbConnection(inner, OnConnectionDisposed);
    }

    private static void OnConnectionDisposed()
    {
        System.Threading.Interlocked.Decrement(ref _activeConnections);
    }

    #nullable disable
    private sealed class TrackedDbConnection : IDbConnection
    {
        private readonly SqlConnection _inner;
        private readonly Action _onDispose;

        public TrackedDbConnection(SqlConnection inner, Action onDispose)
        {
            _inner = inner;
            _onDispose = onDispose;
        }

        public string ConnectionString { get => _inner.ConnectionString; set => _inner.ConnectionString = value; }
        public int ConnectionTimeout => _inner.ConnectionTimeout;
        public string Database => _inner.Database;
        public ConnectionState State => _inner.State;
        public IDbTransaction BeginTransaction() => _inner.BeginTransaction();
        public IDbTransaction BeginTransaction(IsolationLevel il) => _inner.BeginTransaction(il);
        public void ChangeDatabase(string databaseName) => _inner.ChangeDatabase(databaseName);
        public void Close() { _inner.Close(); DisposeOnce(); }
        public IDbCommand CreateCommand() => _inner.CreateCommand();
        public void Open() => _inner.Open();
        public void Dispose() { try { _inner.Dispose(); } finally { DisposeOnce(); } }

        private int _disposed;
        private void DisposeOnce()
        {
            if (System.Threading.Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _onDispose();
            }
        }
    }
    #nullable restore
}
