using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace Microsoft.AspNetCore.Diagnostics;

/// <summary>
/// Formats <see cref="DeveloperExceptionPageMiddleware"/> exceptions as JSON Problem Details if the client indicates it accepts JSON.
/// </summary>
public class ProblemDetailsDeveloperPageExceptionFilter : IDeveloperPageExceptionFilter
{
    private static readonly object ErrorContextItemsKey = new object();
    private static readonly MediaTypeHeaderValue _jsonMediaType = new MediaTypeHeaderValue("application/json");

    private static readonly RequestDelegate _respondWithProblemDetails = RequestDelegateFactory.Create((HttpContext context) =>
    {
        if (context.Items.TryGetValue(ErrorContextItemsKey, out var errorContextItem) && errorContextItem is ErrorContext errorContext)
        {
            return new ErrorProblemDetailsResult(errorContext.Exception);
        }

        return null;
    }).RequestDelegate;

    public async Task HandleExceptionAsync(ErrorContext errorContext, Func<ErrorContext, Task> next)
    {
        var headers = errorContext.HttpContext.Request.GetTypedHeaders();
        var acceptHeader = headers.Accept;

        if (acceptHeader?.Any(h => h.IsSubsetOf(_jsonMediaType)) == true)
        {
            errorContext.HttpContext.Items.Add(ErrorContextItemsKey, errorContext);
            await _respondWithProblemDetails(errorContext.HttpContext);
        }
        else
        {
            await next(errorContext);
        }
    }
}

internal class ErrorProblemDetailsResult : IResult
{
    private readonly Exception _ex;

    public ErrorProblemDetailsResult(Exception ex)
    {
        _ex = ex;
    }

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        var problemDetails = new ProblemDetails
        {
            Title = $"An unhandled exception occurred while processing the request",
            Detail = $"{_ex.GetType().Name}: {_ex.Message}",
            Status = _ex switch
            {
                BadHttpRequestException ex => ex.StatusCode,
                _ => StatusCodes.Status500InternalServerError
            }
        };
        problemDetails.Extensions.Add("exception", _ex.GetType().FullName);
        problemDetails.Extensions.Add("stack", _ex.StackTrace);
        problemDetails.Extensions.Add("headers", httpContext.Request.Headers.ToDictionary(kvp => kvp.Key, kvp => (string)kvp.Value));
        problemDetails.Extensions.Add("routeValues", httpContext.GetRouteData().Values);
        problemDetails.Extensions.Add("query", httpContext.Request.Query);
        var endpoint = httpContext.GetEndpoint();
        if (endpoint != null)
        {
            var routeEndpoint = endpoint as RouteEndpoint;
            var httpMethods = endpoint?.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods;
            problemDetails.Extensions.Add("endpoint", new {
                endpoint?.DisplayName,
                routePattern = routeEndpoint?.RoutePattern.RawText,
                routeOrder = routeEndpoint?.Order,
                httpMethods = httpMethods != null ? string.Join(", ", httpMethods) : ""
            });
        }

        var result = Results.Json(problemDetails, statusCode: problemDetails.Status, contentType: "application/problem+json");

        await result.ExecuteAsync(httpContext);
    }
}

public static class ProblemDetailsDeveloperPageExtensions
{
    /// <summary>
    /// Adds a <see cref="IDeveloperPageExceptionFilter"/> that formats all exceptions as JSON Problem Details to clients
    /// that indicate they support JSON via the Accepts header.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/></param>
    /// <returns>The <see cref="IServiceCollection"/></returns>
    public static IServiceCollection AddProblemDetailsDeveloperPageExceptionFilter(this IServiceCollection services) =>
        services.AddSingleton<IDeveloperPageExceptionFilter, ProblemDetailsDeveloperPageExceptionFilter>();
}
