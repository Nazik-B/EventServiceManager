using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace MyWebApiEventSrvManagerProj.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title) = ExceptionMapper.MapException(exception);

        LogException(context, exception, statusCode);

        var problemDetails = new ProblemDetails
        {
            Type = $"https://tools.ietf.org/html/rfc9110#section-15.{GetSectionSuffix(statusCode)}",
            Title = title,
            Status = statusCode,
            Detail = exception.Message,
            Instance = context.Request.Path
        };
        problemDetails.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = statusCode;

        var json = JsonSerializer.Serialize(problemDetails, _jsonOptions);
        return context.Response.WriteAsync(json);
    }

    private void LogException(HttpContext context, Exception exception, int statusCode)
    {
        var method = context.Request.Method;
        var path = context.Request.Path;
        var traceId = context.TraceIdentifier;

        if (statusCode >= 500)
        {
            _logger.LogError(exception,
                "Server-side error while processing {Method} {Path}. StatusCode: {StatusCode}, TraceId: {TraceId}",
                method, path, statusCode, traceId);
        }
        else if (statusCode >= 400)
        {
            _logger.LogWarning(
                "Client error occurred while processing {Method} {Path}. StatusCode: {StatusCode}, Message: {Message}, TraceId: {TraceId}",
                method, path, statusCode, exception.Message, traceId);
        }
    }

    private static string GetSectionSuffix(int statusCode) => statusCode switch
    {
        400 => "5.1",
        401 => "5.2",
        404 => "5.5",
        409 => "5.8",
        _ => "6.1"
    };
}