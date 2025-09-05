using Microsoft.Extensions.Options;

public static class OptionsMonitorTestExtensions
{
    public static IOptionsMonitor<T> ToOptionsMonitor<T>(this IOptions<T> options) where T : class, new()
        => new StaticOptionsMonitor<T>(options.Value);

    private sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T> where T : class, new()
    {
        private readonly T _value;
        public StaticOptionsMonitor(T value) => _value = value;
        public T CurrentValue => _value;
        public T Get(string? name) => _value;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}


