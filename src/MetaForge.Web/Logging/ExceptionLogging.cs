using FluentValidation;
using MetaForge.Shared.Exceptions;

namespace MetaForge.Web.Logging;

public static class ExceptionLogging
{
    public static void LogUnhandledException(
        this ILogger logger,
        HttpContext context,
        Exception exception)
    {
        var logLevel = GetLogLevel(exception);

        logger.Log(
            logLevel,
            exception,
            "Unhandled exception {ExceptionType} for {RequestMethod} {RequestPath} by {UserName} TraceId={TraceId} QueryString={QueryString} RemoteIp={RemoteIp}",
            exception.GetType().Name,
            context.Request.Method,
            context.Request.Path.Value ?? "/",
            context.User?.Identity?.Name ?? "anonymous",
            context.TraceIdentifier,
            context.Request.QueryString.Value ?? string.Empty,
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown");
    }

    private static LogLevel GetLogLevel(Exception exception) =>
        exception switch
        {
            ValidationException => LogLevel.Warning,
            BusinessException => LogLevel.Warning,
            NotFoundException => LogLevel.Information,
            _ => LogLevel.Error
        };
}
