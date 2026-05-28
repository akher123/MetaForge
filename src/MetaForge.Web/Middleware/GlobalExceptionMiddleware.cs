using System.Net;
using System.Text.Json;
using MetaForge.Shared.Exceptions;
using FluentValidation;

namespace MetaForge.Web.Middleware;

/// <summary>
/// Global exception handling middleware.
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
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
            _logger.LogError(ex, "Unhandled exception");

            if (!IsApiRequest(context))
                throw;

            await HandleExceptionAsync(context, ex);
        }
    }

    private static bool IsApiRequest(HttpContext context) =>
        context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        if (exception is ValidationException validationException)
        {
            context.Response.ContentType = "application/json";
            context.Response.StatusCode = (int)HttpStatusCode.BadRequest;

            var fieldErrors = validationException.Errors
                .Where(e => !string.IsNullOrWhiteSpace(e.PropertyName))
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToList());

            var unboundMessages = validationException.Errors
                .Where(e => string.IsNullOrWhiteSpace(e.PropertyName))
                .Select(e => e.ErrorMessage)
                .ToList();

            var summary = fieldErrors.Count > 0
                ? "Please correct the highlighted fields."
                : unboundMessages.Count > 0
                    ? string.Join("; ", unboundMessages)
                    : "One or more validation errors occurred.";

            if (unboundMessages.Count > 0 && fieldErrors.Count > 0)
                summary = $"{summary} {string.Join("; ", unboundMessages)}";

            var payload = JsonSerializer.Serialize(new
            {
                error = summary,
                fieldErrors
            });

            return context.Response.WriteAsync(payload);
        }

        var (statusCode, message) = exception switch
        {
            NotFoundException => (HttpStatusCode.NotFound, exception.Message),
            BusinessException => (HttpStatusCode.BadRequest, exception.Message),
            _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred.")
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var errorPayload = JsonSerializer.Serialize(new { error = message });
        return context.Response.WriteAsync(errorPayload);
    }
}
