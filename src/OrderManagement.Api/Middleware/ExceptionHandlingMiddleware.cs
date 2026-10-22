using Microsoft.AspNetCore.Mvc;
using OrderManagement.Api.Exceptions;

namespace OrderManagement.Api.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (ApiException ex)
        {
            logger.LogInformation(ex, "Request failed with {ErrorCode}: {Message}", ex.ErrorCode, ex.Message);
            await WriteProblemAsync(context, ex.StatusCode, ex.ErrorCode, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception processing request");
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError, "INTERNAL_ERROR", "An unexpected error occurred.");
        }
    }

    private static async Task WriteProblemAsync(HttpContext context, int statusCode, string errorCode, string message)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = errorCode,
            Detail = message,
            Instance = context.Request.Path
        };
        problem.Extensions["correlationId"] = context.TraceIdentifier;

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(problem);
    }
}
