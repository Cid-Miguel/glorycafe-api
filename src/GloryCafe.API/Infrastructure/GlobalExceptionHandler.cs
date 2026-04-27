using GloryCafe.Application.Common.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GloryCafe.API.Infrastructure;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger,
        IHostEnvironment env)
    {
        _logger = logger;
        _env = env;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title, problem) = Map(exception);

        if (status >= 500)
            _logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);
        else
            _logger.LogInformation(exception, "Handled exception: {Message}", exception.Message);

        problem.Status = status;
        problem.Title = title;
        problem.Type = $"https://httpstatuses.io/{status}";
        problem.Instance = httpContext.Request.Path;
        problem.Extensions["traceId"] = httpContext.TraceIdentifier;

        httpContext.Response.StatusCode = status;
        httpContext.Response.ContentType = "application/problem+json";
        await httpContext.Response.WriteAsJsonAsync(problem, problem.GetType(), options: null, cancellationToken: cancellationToken);
        return true;
    }

    private (int status, string title, ProblemDetails problem) Map(Exception ex)
    {
        switch (ex)
        {
            case ValidationException vex:
                var validation = new ValidationProblemDetails(vex.Errors)
                {
                    Detail = "One or more validation errors occurred."
                };
                return (StatusCodes.Status400BadRequest, "Validation failed", validation);

            case NotFoundException:
                return (StatusCodes.Status404NotFound, "Resource not found", new ProblemDetails
                {
                    Detail = ex.Message
                });

            case InvalidCredentialsException:
                return (StatusCodes.Status401Unauthorized, "Invalid credentials", new ProblemDetails
                {
                    Detail = ex.Message
                });

            case ConflictException:
                return (StatusCodes.Status409Conflict, "Conflict", new ProblemDetails
                {
                    Detail = ex.Message
                });

            default:
                var detail = _env.IsDevelopment() ? ex.ToString() : "An unexpected error occurred.";
                return (StatusCodes.Status500InternalServerError, "Server error", new ProblemDetails
                {
                    Detail = detail
                });
        }
    }
}
