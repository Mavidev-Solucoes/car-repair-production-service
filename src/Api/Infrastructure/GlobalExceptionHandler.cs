using Application.Common.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Api.Infrastructure;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is ValidationException validationException)
        {
            var errors = validationException.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(e => e.ErrorMessage).ToArray());

            var problemDetails = new ValidationProblemDetails(errors)
            {
                Title = "Validation failed.",
                Status = StatusCodes.Status400BadRequest,
                Type = "https://www.rfc-editor.org/rfc/rfc9110.html#name-400-bad-request"
            };

            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            httpContext.Response.ContentType = "application/problem+json";

            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
            return true;
        }

        if (exception is NotFoundException)
        {
            var problemDetails = new ProblemDetails
            {
                Title = exception.Message,
                Status = StatusCodes.Status404NotFound,
                Type = "https://www.rfc-editor.org/rfc/rfc9110.html#name-404-not-found"
            };

            httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
            httpContext.Response.ContentType = "application/problem+json";

            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
            return true;
        }

        if (exception is BusinessRuleException)
        {
            var problemDetails = new ProblemDetails
            {
                Title = exception.Message,
                Status = StatusCodes.Status409Conflict,
                Type = "https://www.rfc-editor.org/rfc/rfc9110.html#name-409-conflict"
            };

            httpContext.Response.StatusCode = StatusCodes.Status409Conflict;
            httpContext.Response.ContentType = "application/problem+json";

            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
            return true;
        }

        logger.LogError(exception, "Unhandled exception occurred while processing request.");

        var errorDetails = new ProblemDetails
        {
            Title = "An unexpected error occurred.",
            Status = StatusCodes.Status500InternalServerError,
            Type = "https://www.rfc-editor.org/rfc/rfc9110.html#name-500-internal-server-error"
        };

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(errorDetails, cancellationToken);
        return true;
    }
}
