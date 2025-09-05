using System.Text.Json;
using FlashAssessment.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace FlashAssessment.Api.Filters;

public sealed class ExceptionHandlingMiddleware : IMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly FlashAssessment.Application.Common.ILogSanitizer? _logSanitizer;

    public ExceptionHandlingMiddleware(ILogger<ExceptionHandlingMiddleware> logger, FlashAssessment.Application.Common.ILogSanitizer? logSanitizer = null)
    {
        _logger = logger;
        _logSanitizer = logSanitizer;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (ConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict for {Path} with CorrelationId={CorrelationId}", context.Request.Path, context.TraceIdentifier);
            await WriteProblem(context, StatusCodes.Status409Conflict, "Concurrency conflict", ex.Message);
        }
        catch (DuplicateException ex)
        {
            _logger.LogWarning(ex, "Duplicate resource on {Path} with CorrelationId={CorrelationId}", context.Request.Path, context.TraceIdentifier);
            await WriteProblem(context, StatusCodes.Status409Conflict, "Duplicate resource", ex.Message);
        }
        catch (FluentValidation.ValidationException ex)
        {
            _logger.LogInformation(ex, "Validation failed on {Path} with CorrelationId={CorrelationId}", context.Request.Path, context.TraceIdentifier);
            await WriteProblem(context, StatusCodes.Status400BadRequest, "Validation failed", ex.Message);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogInformation(ex, "Not found {Path} with CorrelationId={CorrelationId}", context.Request.Path, context.TraceIdentifier);
            await WriteProblem(context, StatusCodes.Status404NotFound, "Not found", ex.Message);
        }
        catch (Exception ex)
        {
            var path = context.Request.Path.ToString();
            if (_logSanitizer is not null)
            {
                try { path = await _logSanitizer.SanitizeAsync(path, context.RequestAborted); } catch { }
            }
            _logger.LogError(ex, "Unhandled exception for {Path} with CorrelationId={CorrelationId}", path, context.TraceIdentifier);
            await WriteProblem(context, StatusCodes.Status500InternalServerError, "Unexpected error", ex.Message);
        }
    }

    private static async Task WriteProblem(HttpContext ctx, int statusCode, string title, string detail)
    {
        if (ctx.Response.HasStarted)
        {
            // If headers already sent, we can't write a problem response reliably
            return;
        }

        ctx.Response.Clear();
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = null, // do not echo raw details; keep generic per security rule
            Instance = ctx.TraceIdentifier
        };
        ctx.Response.StatusCode = statusCode;
        ctx.Response.ContentType = "application/problem+json; charset=utf-8";
        ctx.Response.Headers["Cache-Control"] = "no-store";
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }
}


