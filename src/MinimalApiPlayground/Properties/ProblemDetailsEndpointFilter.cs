using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace MinimalApiPlayground.Properties;

/// <summary>
/// Defines an endpoint filter that modifies <see cref="ProblemHttpResult"/> instances returned by route handler delegates
/// using the <see cref="IProblemDetailsService"/>.
/// </summary>
public class ProblemDetailsServiceEndpointFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        => await next(context) switch
        {
            ProblemHttpResult problemHttpResult => new ProblemDetailsServiceAwareResult(problemHttpResult.StatusCode, problemHttpResult.ProblemDetails),
            ProblemDetails problemDetails => new ProblemDetailsServiceAwareResult(null, problemDetails),
            { } result => result,
            null => null
        };

    private class ProblemDetailsServiceAwareResult : IResult, IValueHttpResult, IValueHttpResult<ProblemDetails>
    {
        private readonly int? _statusCode;

        public ProblemDetailsServiceAwareResult(int? statusCode, ProblemDetails problemDetails)
        {
            _statusCode = statusCode ?? problemDetails.Status;
            Value = problemDetails;
        }

        public ProblemDetails Value { get; }

        object? IValueHttpResult.Value => Value;

        public async Task ExecuteAsync(HttpContext httpContext)
        {
            if (httpContext.RequestServices.GetService<IProblemDetailsService>() is IProblemDetailsService problemDetailsService)
            {
                if (_statusCode is { } statusCode)
                {
                    httpContext.Response.StatusCode = statusCode;
                }
                await problemDetailsService.WriteAsync(new ProblemDetailsContext
                {
                    HttpContext = httpContext,
                    ProblemDetails = Value
                });
            }
        }
    }
}
