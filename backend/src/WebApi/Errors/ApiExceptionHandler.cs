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
            // A rule the request validation did not catch, checked by the domain object itself.
            InvalidValueException invalid => new HttpValidationProblemDetails(
                new Dictionary<string, string[]>
                {
                    [invalid.ParameterName] = [invalid.Message]
                })
            {
                Status = StatusCodes.Status400BadRequest,
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

        // The status is the contract; the body is only written when the client accepts JSON. Reporting "not
        // handled" here would let the middleware turn a mapped 404 into a 500.
        await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext, ProblemDetails = problemDetails, Exception = exception,
        }).ConfigureAwait(false);

        return true;
    }
}
