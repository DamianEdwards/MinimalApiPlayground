// Example of a way to adjust the default responses from Minimal APIs

class MutateResponseMiddleware
{
    private readonly RequestDelegate _next;

    public MutateResponseMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext httpContext)
    {
        await _next(httpContext);

        if (httpContext.GetEndpoint()?.Metadata.GetMetadata<IMutateResponseMetadata>() is IMutateResponseMetadata mutateResponseMetadata)
        {
            if (httpContext.Response.HasStarted)
            {
                throw new InvalidOperationException("Can't mutate response after headers have been sent to client.");
            }
            if (mutateResponseMetadata.StatusCode != null)
            {
                httpContext.Response.StatusCode = mutateResponseMetadata.StatusCode.Value;
            }
            if (mutateResponseMetadata.Message != null)
            {
                await httpContext.Response.WriteAsync(mutateResponseMetadata.Message);
            }
        }
    }
}

public static class MutateResponseMiddlewareExtensions
{
    public static IApplicationBuilder UseMutateResponse(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<MutateResponseMiddleware>();
    }
}

interface IMutateResponseMetadata
{
    int? StatusCode { get; }

    string? Message { get; }
}

class MutateResponse : IMutateResponseMetadata
{
    public MutateResponse(int? statusCode, string? message)
    {
        StatusCode = statusCode;
        Message = message;
    }

    public int? StatusCode { get; }

    public string? Message { get; }
}

static class MutateResponseMetadataExtensions
{
    public static MinimalActionEndpointConventionBuilder MutateResponse(this MinimalActionEndpointConventionBuilder builder, int? statusCode, string? message)
    {
        builder.WithMetadata(new MutateResponse(statusCode, message));
        return builder;
    }
}