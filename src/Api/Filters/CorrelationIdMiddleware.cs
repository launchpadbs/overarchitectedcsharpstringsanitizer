namespace FlashAssessment.Api.Filters;

public sealed class CorrelationIdMiddleware : IMiddleware
{
    private const string HeaderName = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (!context.Request.Headers.TryGetValue(HeaderName, out var correlationId) || string.IsNullOrWhiteSpace(correlationId))
        {
            correlationId = Guid.NewGuid().ToString("N");
        }
        context.Response.Headers[HeaderName] = correlationId!;
        context.TraceIdentifier = correlationId!;
        using (context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Correlation").BeginScope(new Dictionary<string, object>{{"CorrelationId", correlationId!.ToString()}}))
        {
            await next(context);
        }
    }
}


