using Microsoft.AspNetCore.Diagnostics;
using ResolveAI.CaseManagement.Contracts;
using ResolveAI.Platform;

namespace ResolveAI.Host.Infrastructure;

internal sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            AccessDeniedException => (403, "Access denied."),
            CaseValidationException validation => (400, validation.Message),
            _ => (500, "An unexpected error occurred.")
        };
        if (status == 500)
            logger.LogError("Request failed ({ErrorType}), trace {TraceId}", exception.GetType().Name, context.TraceIdentifier);
        await Results.Problem(statusCode: status, title: title,
            extensions: new Dictionary<string, object?> { ["traceId"] = context.TraceIdentifier }).ExecuteAsync(context);
        return true;
    }
}
