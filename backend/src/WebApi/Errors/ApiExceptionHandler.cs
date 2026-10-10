using Application.Exceptions;
using Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace WebApi.Errors;

internal sealed class ApiExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var problemDetails = exception switch
        {
            NotFoundException notFound => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound, Title = "Resource not found", Detail = notFound.Message,
            },
            BusinessRuleViolationException violation => new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Business rule violation",
                Detail = violation.Message,
                Extensions =
                {
                    ["code"] = violation.Code
                },
            },
            // Malformed requests; Development throws them (ThrowOnBadRequest) instead of answering 400.
            BadHttpRequestException badRequest => new ProblemDetails
            {
                Status = badRequest.StatusCode,
            },
            _ => null,
        };

        if (problemDetails is null)
        {
            return false;
        }

        httpContext.Response.StatusCode = problemDetails.Status!.Value;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext, ProblemDetails = problemDetails, Exception = exception,
        }).ConfigureAwait(false);
    }
}
