namespace Microsoft.AspNetCore.Antiforgery;

public class AntiforgeryMiddleware
{
    internal const string AntiforgeryMiddlewareInvokedKey = "__AntiforgeryMiddlewareInvoked";
    private static readonly object AntiforgeryMiddlewareInvokedValue = new object();

    private readonly RequestDelegate _next;
    private readonly IAntiforgery _antiforgery;

    public AntiforgeryMiddleware(RequestDelegate next, IAntiforgery antiforgery)
    {
        _next = next;
        _antiforgery = antiforgery;
    }

    public async Task Invoke(HttpContext httpContext)
    {
        var endpoint = httpContext.GetEndpoint();

        if (endpoint is Endpoint && !httpContext.Items.ContainsKey(AntiforgeryMiddlewareInvokedKey))
        {
            httpContext.Items.Add(AntiforgeryMiddlewareInvokedKey, AntiforgeryMiddlewareInvokedValue);

            if (endpoint.Metadata.GetMetadata<IAntiforgeryMetadata>() is IAntiforgeryMetadata metadata)
            {
                if (metadata is IDisableAntiforgery)
                {
                    await _next(httpContext);
                    return;
                }

                await _antiforgery.ValidateRequestAsync(httpContext);
            }
        }

        await _next(httpContext);
    }
}

public static class AntiforgeryMiddlewareExtensions
{
    public static IApplicationBuilder UseAntiforgery(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<AntiforgeryMiddleware>();
    }
}

public interface IAntiforgeryMetadata
{

}

public interface IDisableAntiforgery : IAntiforgeryMetadata
{

}

public class AntiforgeryMetadata : IAntiforgeryMetadata
{

}
