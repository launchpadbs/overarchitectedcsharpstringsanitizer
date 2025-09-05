using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.Extensions.Logging;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using FlashAssessment.Application.Common.RateLimiting;
using FlashAssessment.Application.Common;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// Config & Options
var configuration = builder.Configuration;
var services = builder.Services;

services.AddControllers().AddJsonOptions(o =>
{
    o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
services.AddEndpointsApiExplorer();
services.AddSwaggerGen(c =>
{
    // Include XML documentation from projects (controllers and DTOs)
    var baseDir = AppContext.BaseDirectory;
    var xmlCandidates = new[]
    {
        Path.Combine(baseDir, "Api.xml"),
        Path.Combine(baseDir, "Application.xml"),
        Path.Combine(baseDir, "Domain.xml"),
        Path.Combine(baseDir, "Infrastructure.xml")
    };
    foreach (var xml in xmlCandidates)
    {
        if (File.Exists(xml))
        {
            c.IncludeXmlComments(xml, includeControllerXmlComments: true);
        }
    }
});
var healthOptions = configuration.GetSection("Health").Get<HealthOptions>() ?? new HealthOptions();
services.AddHealthChecks()
    .AddSqlServer(configuration.GetConnectionString("Sql")!, name: "sql", timeout: TimeSpan.FromSeconds(healthOptions.SqlTimeoutSeconds))
    .AddCheck<FlashAssessment.Api.Health.RegexCacheHealthCheck>("regex-cache");
services.AddMemoryCache();
// Optional Redis distributed cache for multi-instance scale-out
if (!string.IsNullOrWhiteSpace(configuration["Redis:Configuration"]))
{
    services.AddStackExchangeRedisCache(opts =>
    {
        opts.Configuration = configuration["Redis:Configuration"];
        opts.InstanceName = configuration["Redis:InstanceName"] ?? "flash:";
    });
}
services.Configure<FlashAssessment.Application.Words.ActiveWordsCacheOptions>(configuration.GetSection("Caching"));
services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<FlashAssessment.Application.Words.ActiveWordsCacheOptions>, FlashAssessment.Application.Words.ActiveWordsCacheOptionsValidator>();
services.Configure<RequestLimitsOptions>(configuration.GetSection("RequestLimits"));
services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<RequestLimitsOptions>, RequestLimitsOptionsValidator>();
services.Configure<HealthOptions>(configuration.GetSection("Health"));
services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<HealthOptions>, HealthOptionsValidator>();
services.Configure<ResilienceOptions>(configuration.GetSection("Resilience"));
services.Configure<RateLimitingOptions>(configuration.GetSection("RateLimiting"));
services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<RateLimitingOptions>, RateLimitingOptionsValidator>();

// Rate limiting: fixed window per IP for sanitize endpoint
services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    var rateOptions = builder.Configuration.GetSection("RateLimiting").Get<RateLimitingOptions>() ?? new RateLimitingOptions();

    // Sanitize policy
    options.AddPolicy("sanitize", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateOptions.Sanitize.PermitLimit,
                Window = TimeSpan.FromSeconds(rateOptions.Sanitize.WindowSeconds),
                QueueLimit = rateOptions.Sanitize.QueueLimit,
                AutoReplenishment = rateOptions.Sanitize.AutoReplenishment
            }));

// Optional: rate limit words management endpoints as well (per-IP lower rate)
    // Words policies
    options.AddPolicy("words_read", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateOptions.WordsRead.PermitLimit,
                Window = TimeSpan.FromSeconds(rateOptions.WordsRead.WindowSeconds),
                QueueLimit = rateOptions.WordsRead.QueueLimit,
                AutoReplenishment = rateOptions.WordsRead.AutoReplenishment
            }));

    options.AddPolicy("words_write", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = rateOptions.WordsWrite.PermitLimit,
                Window = TimeSpan.FromSeconds(rateOptions.WordsWrite.WindowSeconds),
                QueueLimit = rateOptions.WordsWrite.QueueLimit,
                AutoReplenishment = rateOptions.WordsWrite.AutoReplenishment
            }));

    options.OnRejected = async (context, token) =>
    {
        var metrics = context.HttpContext.RequestServices.GetService<FlashAssessment.Application.Common.ISanitizationMetrics>();
        metrics?.RateLimitRejected();
        var problem = new ProblemDetails
        {
            Title = "Too Many Requests",
            Status = StatusCodes.Status429TooManyRequests,
            Detail = "Rate limit exceeded. Please retry later.",
            Instance = context.HttpContext.TraceIdentifier
        };
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.ContentType = "application/problem+json";
        await context.HttpContext.Response.WriteAsync(JsonSerializer.Serialize(problem), token);
    };
});

// Middleware registrations
services.AddTransient<FlashAssessment.Api.Filters.ExceptionHandlingMiddleware>();
services.AddTransient<FlashAssessment.Api.Filters.ApiKeyMiddleware>();
services.AddTransient<FlashAssessment.Api.Filters.CorrelationIdMiddleware>();
services.AddScoped<FlashAssessment.Api.Filters.ValidateJsonRequestFilter>();

// FluentValidation automatic integration
services.AddFluentValidationAutoValidation();
services.AddValidatorsFromAssemblyContaining<FlashAssessment.Application.Words.Validators.CreateSensitiveWordRequestValidator>();

// Repositories & Services
services.AddSingleton<FlashAssessment.Infrastructure.Database.SqlConnectionFactory>(sp =>
{
	var cs = builder.Configuration.GetConnectionString("Sql")!;
	var logger = sp.GetRequiredService<ILogger<FlashAssessment.Infrastructure.Database.SqlConnectionFactory>>();
	return new FlashAssessment.Infrastructure.Database.SqlConnectionFactory(cs, TimeSpan.FromSeconds(30), logger);
});
services.AddScoped<FlashAssessment.Application.Common.ISqlConnectionFactory>(sp => sp.GetRequiredService<FlashAssessment.Infrastructure.Database.SqlConnectionFactory>());
services.AddScoped<FlashAssessment.Application.Words.ISensitiveWordRepository, FlashAssessment.Infrastructure.Repositories.SensitiveWordRepository>();
services.AddScoped<FlashAssessment.Application.Words.IActiveWordsProvider, FlashAssessment.Application.Words.ActiveWordsProvider>();
services.AddScoped<FlashAssessment.Application.Words.ISensitiveWordService, FlashAssessment.Application.Words.SensitiveWordService>();
services.AddSingleton<FlashAssessment.Application.Common.ISanitizationMetrics, FlashAssessment.Api.Observability.SanitizationMetrics>();
services.AddScoped<FlashAssessment.Application.Sanitization.ISanitizationService, FlashAssessment.Application.Sanitization.SanitizationService>();
services.AddScoped<FlashAssessment.Application.Common.ILogSanitizer, FlashAssessment.Api.Support.LogSanitizer>();

var app = builder.Build();

app.UseMiddleware<FlashAssessment.Api.Filters.CorrelationIdMiddleware>();
app.UseMiddleware<FlashAssessment.Api.Filters.ApiKeyMiddleware>();
app.UseMiddleware<FlashAssessment.Api.Filters.ExceptionHandlingMiddleware>();
app.UseRateLimiter();

app.UseSwagger();
app.UseSwaggerUI();

// Disable HTTPS redirection inside container (no TLS termination here)

app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => true
});

app.Run();

public partial class Program { }