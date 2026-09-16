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
        // Every mapped failure carries a stable machine-readable code; clients must not match on the title text.
        var (status, code, title) = exception switch
        {
            AccessDeniedException => (403, "AccessDenied", "Access denied."),
            ProposalRequestException { Code: "OperationConflict" } => (409, "OperationConflict", "Operation ID was already used for different input."),
            ProposalRequestException proposal => (400, proposal.Code, "Invalid proposal request or input limit exceeded."),
            CaseValidationException validation => (400, "ValidationFailed", validation.Message),
            BusinessReadException { Failure: BusinessReadFailure.InvalidReference } => (400, "InvalidReference", "Invalid business reference."),
            BusinessReadException { Failure: BusinessReadFailure.NotFound } => (404, "NotFound", "Business resource not found."),
            BusinessReadException { Failure: BusinessReadFailure.AssociationMismatch } => (422, "AssociationMismatch", "Business references do not belong together."),
            BusinessReadException => (503, "Unavailable", "Business data unavailable."),
            _ => (500, "InternalError", "An unexpected error occurred.")
        };
        if (status == 500)
            logger.LogError("Request failed ({ErrorType}), trace {TraceId}", exception.GetType().Name, context.TraceIdentifier);
        await Results.Problem(statusCode: status, title: title,
            extensions: new Dictionary<string, object?>
            {
                ["traceId"] = context.TraceIdentifier,
                ["code"] = code
            }).ExecuteAsync(context);
        return true;
    }
}
