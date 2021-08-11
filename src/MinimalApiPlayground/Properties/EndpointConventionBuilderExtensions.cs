using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;

namespace Microsoft.AspNetCore.Http;

public static class EndpointConventionBuilderExtensions
{
    public static MinimalActionEndpointConventionBuilder Accepts(this MinimalActionEndpointConventionBuilder builder, string contentType, params string[] otherContentTypes)
    {
        builder.WithMetadata(new ConsumesAttribute(contentType, otherContentTypes));

        return builder;
    }

    public static MinimalActionEndpointConventionBuilder Accepts<T>(this MinimalActionEndpointConventionBuilder builder, string? contentType = null, params string[] otherContentTypes)
    {
        Accepts(builder, typeof(T), contentType, otherContentTypes);

        return builder;
    }

    public static MinimalActionEndpointConventionBuilder Accepts(this MinimalActionEndpointConventionBuilder builder, Type requestType, string? contentType = null, params string[] otherContentTypes)
    {
        builder.WithMetadata(
            contentType is object
                ? new ConsumesRequestTypeAttribute(contentType, otherContentTypes)
                {
                    Type = requestType
                }
                : new ConsumesRequestTypeAttribute(requestType)
        );

        return builder;
    }
}