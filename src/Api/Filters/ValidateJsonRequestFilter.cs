namespace FlashAssessment.Api.Filters;

public sealed class ValidateJsonRequestFilter : IMiddleware
{
    private readonly FlashAssessment.Application.Common.RequestLimitsOptions _options;

    public ValidateJsonRequestFilter(Microsoft.Extensions.Options.IOptions<FlashAssessment.Application.Common.RequestLimitsOptions> options)
    {
        _options = options.Value;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // Only validate JSON bodies for POST/PUT/PATCH
        if (HttpMethods.IsPost(context.Request.Method) || HttpMethods.IsPut(context.Request.Method) || HttpMethods.IsPatch(context.Request.Method))
        {
            var contentType = context.Request.ContentType ?? string.Empty;
            if (!contentType.StartsWith("application/json", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status415UnsupportedMediaType;
                await context.Response.WriteAsync("Unsupported media type. Expected application/json.");
                return;
            }

            // Enforce content-length when provided
            if (context.Request.ContentLength.HasValue && context.Request.ContentLength.Value > _options.MaxJsonBytes)
            {
                context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
                await context.Response.WriteAsync("Payload too large.");
                return;
            }
        }

        await next(context);
    }
}


