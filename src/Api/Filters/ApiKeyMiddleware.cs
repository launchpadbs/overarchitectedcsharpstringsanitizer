namespace FlashAssessment.Api.Filters;

public sealed class ApiKeyMiddleware : IMiddleware
{
    private const string HeaderName = "X-Api-Key";
    private readonly bool _enabled;
    private readonly string? _apiKey;

    public ApiKeyMiddleware(IConfiguration configuration)
    {
        _enabled = configuration.GetValue<bool>("Auth:Enabled");
        _apiKey = configuration.GetValue<string>("Auth:ApiKey");
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (!_enabled)
        {
            await next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out var header) || string.IsNullOrEmpty(_apiKey) || header != _apiKey)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Unauthorized");
            return;
        }

        await next(context);
    }
}


