using System.Net;
using System.Text.Json;
using FluentValidation;
using MetaForge.Shared.Exceptions;
using Microsoft.AspNetCore.Diagnostics;

namespace MetaForge.Web.Logging;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;

    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger,
        IHostEnvironment environment)
    {
        _logger = logger;
        _environment = environment;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogUnhandledException(httpContext, exception);

        if (IsApiRequest(httpContext))
        {
            await WriteApiErrorAsync(httpContext, exception, cancellationToken);
            return true;
        }

        if (_environment.IsDevelopment())
            return false;

        if (httpContext.Response.HasStarted)
            return false;

        httpContext.Response.Clear();
        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        httpContext.Response.Redirect("/Home/Error");
        return true;
    }

    private static bool IsApiRequest(HttpContext context) =>
        context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase);

    private static Task WriteApiErrorAsync(
        HttpContext context,
        Exception exception,
        CancellationToken cancellationToken)
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

            return context.Response.WriteAsync(payload, cancellationToken);
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
        return context.Response.WriteAsync(errorPayload, cancellationToken);
    }
}
