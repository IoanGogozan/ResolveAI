using Microsoft.AspNetCore.Diagnostics;
using ResolveAI.CaseManagement.Contracts;
using ResolveAI.Platform;
using ResolveAI.Execution.Contracts;
using ResolveAI.AI.Contracts;

namespace ResolveAI.Host.Infrastructure;

internal sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            AccessDeniedException => (403, "Access denied."),
            ProposalRequestException { Code: "OperationConflict" } => (409, "Operation ID was already used for different input."),
            ProposalRequestException => (400, "Invalid proposal request or input limit exceeded."),
            CaseValidationException validation => (400, validation.Message),
            BusinessReadException { Failure: BusinessReadFailure.InvalidReference } => (400, "Invalid business reference."),
            BusinessReadException { Failure: BusinessReadFailure.NotFound } => (404, "Business resource not found."),
            BusinessReadException { Failure: BusinessReadFailure.AssociationMismatch } => (422, "Business references do not belong together."),
            BusinessReadException => (503, "Business data unavailable."),
            _ => (500, "An unexpected error occurred.")
        };
        if (status == 500)
            logger.LogError("Request failed ({ErrorType}), trace {TraceId}", exception.GetType().Name, context.TraceIdentifier);
        await Results.Problem(statusCode: status, title: title,
            extensions: new Dictionary<string, object?>
            {
                ["traceId"] = context.TraceIdentifier,
                ["code"] = exception is BusinessReadException business ? business.Failure.ToString() : null
            }).ExecuteAsync(context);
        return true;
    }
}
