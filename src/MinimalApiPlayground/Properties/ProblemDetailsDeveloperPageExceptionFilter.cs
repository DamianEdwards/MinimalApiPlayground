using System.Diagnostics;
using Microsoft.Net.Http.Headers;

namespace Microsoft.AspNetCore.Diagnostics;

/// <summary>
/// Formats <see cref="DeveloperExceptionPageMiddleware"/> exceptions as JSON Problem Details if the client indicates it accepts JSON.
/// </summary>
public class ProblemDetailsDeveloperPageExceptionFilter : IDeveloperPageExceptionFilter
{
    private static readonly MediaTypeHeaderValue _problemJsonMediaType = new("application/problem+json");

    public async Task HandleExceptionAsync(ErrorContext errorContext, Func<ErrorContext, Task> next)
    {
        var headers = errorContext.HttpContext.Request.GetTypedHeaders();
        var acceptHeader = headers.Accept;
        var ex = errorContext.Exception;
        var httpContext = errorContext.HttpContext;

        if (acceptHeader?.Any(h => _problemJsonMediaType.IsSubsetOf(h)) == true)
        {
            var problemDetails = new Mvc.ProblemDetails
            {
                Title = $"An unhandled exception occurred while processing the request",
                Detail = $"{ex.GetType().Name}: {ex.Message}",
                Status = ex switch
                {
                    BadHttpRequestException bhre => bhre.StatusCode,
                    _ => StatusCodes.Status500InternalServerError
                }
            };

            problemDetails.Extensions.Add("requestId", Activity.Current?.Id ?? httpContext.TraceIdentifier);
            problemDetails.Extensions.Add("exception", ex.GetType().FullName);
            problemDetails.Extensions.Add("stack", ex.StackTrace);
            problemDetails.Extensions.Add("headers", httpContext.Request.Headers.ToDictionary(kvp => kvp.Key, kvp => (string?)kvp.Value));
            problemDetails.Extensions.Add("routeValues", httpContext.GetRouteData().Values);
            problemDetails.Extensions.Add("query", httpContext.Request.Query);
            var endpoint = httpContext.GetEndpoint();
            if (endpoint != null)
            {
                var routeEndpoint = endpoint as RouteEndpoint;
                var httpMethods = endpoint?.Metadata.GetMetadata<IHttpMethodMetadata>()?.HttpMethods;
                problemDetails.Extensions.Add("endpoint", new
                {
                    endpoint?.DisplayName,
                    routePattern = routeEndpoint?.RoutePattern.RawText,
                    routeOrder = routeEndpoint?.Order,
                    httpMethods = httpMethods != null ? string.Join(", ", httpMethods) : ""
                });
            }

            await Results.Problem(problemDetails).ExecuteAsync(httpContext);
        }
        else
        {
            await next(errorContext);
        }
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
