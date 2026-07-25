using MyWebApiEventSrvManagerProj.Middleware;
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
            _logger.LogError(ex, "Unhandled exception occurred while processing {Method} {Path}",
                context.Request.Method, context.Request.Path);

            await HandleExceptionAsync(context, ex);
        }
    }

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title) = ExceptionMapper.MapException(exception);

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


    private static string GetSectionSuffix(int statusCode) => statusCode switch
    {
        400 => "5.1",
        401 => "5.2",
        404 => "5.5",
        409 => "5.10",
        _ => "6.1"
    };
}