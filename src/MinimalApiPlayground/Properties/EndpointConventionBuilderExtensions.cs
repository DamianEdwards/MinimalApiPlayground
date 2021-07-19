using Microsoft.AspNetCore.Mvc;

namespace Microsoft.AspNetCore.Http;

public static class EndpointConventionBuilderExtensions
{
    static readonly string[] NoHttpMethods = new[] { "[none]" };

    /// <summary>
    /// Adds an EndpointNameMetadata item to the Metadata for all builders produced by builder.
    /// </summary>
    /// <typeparam name="TBuilder"></typeparam>
    /// <param name="builder"></param>
    /// <param name="name"></param>
    /// <returns></returns>
    public static IEndpointConventionBuilder WithName(this IEndpointConventionBuilder builder, string name)
    {
        // Once Swashbuckle issue is fixed this will set operationId in the swagger too
        // https://github.com/domaindrivendev/Swashbuckle.AspNetCore/issues/2165

        builder.WithMetadata(new EndpointNameMetadata(name));

        return builder;
    }

    public static IEndpointConventionBuilder Produces<TResponse>(this IEndpointConventionBuilder builder,
        int statusCode = StatusCodes.Status200OK,
        string? contentType = "application/json")
    {
        return Produces(builder, statusCode, typeof(TResponse), contentType);
    }

    public static IEndpointConventionBuilder Produces(this IEndpointConventionBuilder builder,
        int statusCode = StatusCodes.Status200OK,
        Type? responseType = null,
        string? contentType = "application/json")
    {
        if (responseType is Type)
        {
            builder.WithMetadata(new ProducesResponseTypeAttribute(responseType, statusCode));
        }
        else
        {
            builder.WithMetadata(new ProducesResponseTypeAttribute(statusCode));
        }

        if (!string.IsNullOrEmpty(contentType))
        {
            // TODO: Allow multiple content types?
            // TODO: Content type per status code/response type? e.g. https://github.com/domaindrivendev/Swashbuckle.AspNetCore/issues/1691
            builder.WithMetadata(new ProducesAttribute(contentType));
        }

        return builder;
    }
}
