using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;

namespace Microsoft.AspNetCore.Http;

public static class OpenApiEndpointConventionBuilderExtensions
{
    private static readonly EndpointIgnoreMetadata IgnoreMetadata = new();

    /// <summary>
    /// Adds an EndpointNameMetadata item to the Metadata for all endpoints produced by the builder.<br />
    /// The name is used to lookup the endpoint during link generation and as an operationId when generating OpenAPI documentation.<br />
    /// The name must be unique per endpoint.
    /// </summary>
    /// <param name="builder">The Microsoft.AspNetCore.Builder.IEndpointConventionBuilder.</param>
    /// <param name="name">The endpoint name.</param>
    /// <returns>The Microsoft.AspNetCore.Builder.IEndpointConventionBuilder.</returns>
    public static IEndpointConventionBuilder WithName(this IEndpointConventionBuilder builder, string name)
    {
        // Once Swashbuckle issue is fixed this will set operationId in the swagger doc
        // https://github.com/domaindrivendev/Swashbuckle.AspNetCore/issues/2165
        builder.WithMetadata(new EndpointNameMetadata(name));

        return builder;
    }

    /// <summary>
    /// Adds an EndpointGroupNameMetadata item to the Metadata for all endpoints produced by the builder.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="groupName"></param>
    /// <returns>The Microsoft.AspNetCore.Builder.IEndpointConventionBuilder.</returns>
    public static IEndpointConventionBuilder WithGroupName(this IEndpointConventionBuilder builder, string groupName)
    {
        // Swashbuckle uses group name to match APIs with OpenApi documents by default
        // See https://github.com/domaindrivendev/Swashbuckle.AspNetCore/blob/master/src/Swashbuckle.AspNetCore.SwaggerGen/SwaggerGenerator/SwaggerGeneratorOptions.cs#L59
        // Minimal APIs currently doesn't populate the ApiDescription with a group name but we will change that so this can work as intended.
        // Note that EndpointGroupNameMetadata doesn't exist in ASP.NET Core today so we'll have to add that too.
        builder.WithMetadata(new EndpointGroupNameMetadata(groupName));

        return builder;
    }

    /// <summary>
    /// Adds metadata indicating an endpoint should be ignored by consumers of endpoint metadata, e.g. when generating OpenAPI documentation.
    /// </summary>
    /// <param name="builder">The Microsoft.AspNetCore.Builder.IEndpointConventionBuilder.</param>
    /// <returns>The Microsoft.AspNetCore.Builder.IEndpointConventionBuilder.</returns>
    public static IEndpointConventionBuilder Ignore(this IEndpointConventionBuilder builder)
    {
        // Swashbuckle won't include an API in a given document if it has a group name set and it doesn't match the document name,
        // so setting the group name to a random value effectively hides the API from all OpenAPI documents by default
        // See https://github.com/domaindrivendev/Swashbuckle.AspNetCore/blob/master/src/Swashbuckle.AspNetCore.SwaggerGen/SwaggerGenerator/SwaggerGeneratorOptions.cs#L59
        // We may instead want to add a more first-class piece of metadata to indicate the endpoint should be ignored from metadata readers,
        // e.g. https://github.com/dotnet/aspnetcore/issues/34068, which of course will require updating Swashbuckle to honor this too.
        builder.WithMetadata(IgnoreMetadata);

        return builder;
    }

    /// <summary>
    /// Adds metadata indicating the type of response an endpoint produces.
    /// </summary>
    /// <typeparam name="TResponse">The type of the response.</typeparam>
    /// <param name="builder">The Microsoft.AspNetCore.Builder.IEndpointConventionBuilder.</param>
    /// <param name="statusCode">The response status code. Defatuls to StatusCodes.Status200OK.</param>
    /// <param name="contentType">The response content type. Defaults to "application/json"</param>
    /// <param name="additionalContentTypes">Additional response content types the endpoint produces for the supplied status code.</param>
    /// <returns>The Microsoft.AspNetCore.Builder.IEndpointConventionBuilder.</returns>
    public static IEndpointConventionBuilder Produces<TResponse>(this IEndpointConventionBuilder builder,
        int statusCode = StatusCodes.Status200OK,
        string? contentType = "application/json",
        params string[] additionalContentTypes)
    {
        return Produces(builder, statusCode, typeof(TResponse), contentType, additionalContentTypes);
    }

    /// <summary>
    /// Adds metadata indicating the type of response an endpoint produces.
    /// </summary>
    /// <param name="builder">The Microsoft.AspNetCore.Builder.IEndpointConventionBuilder.</param>
    /// <param name="statusCode">The response status code. Defatuls to StatusCodes.Status200OK.</param>
    /// <param name="responseType">The type of the response. Defaults to null.</param>
    /// <param name="contentType">The response content type. Defaults to "application/json" if responseType is not null, otherwise defaults to null.</param>
    /// <param name="additionalContentTypes">Additional response content types the endpoint produces for the supplied status code.</param>
    /// <returns>The Microsoft.AspNetCore.Builder.IEndpointConventionBuilder.</returns>
    public static IEndpointConventionBuilder Produces(this IEndpointConventionBuilder builder,
        int statusCode = StatusCodes.Status200OK,
        Type? responseType = null,
        string? contentType = null,
        params string[] additionalContentTypes)
    {
        if (responseType is Type && string.IsNullOrEmpty(contentType))
        {
            contentType = "application/json";
        }

        builder.WithMetadata(new ProducesMetadataAttribute(responseType, statusCode, contentType, additionalContentTypes));

        return builder;
    }

    /// <summary>
    /// Adds metadata indicating that the endpoint produces a Problem Details response.
    /// </summary>
    /// <param name="builder">The Microsoft.AspNetCore.Builder.IEndpointConventionBuilder.</param>
    /// <param name="statusCode">The response status code. Defatuls to StatusCodes.Status500InternalServerError.</param>
    /// <param name="contentType">The response content type. Defaults to "application/problem+json".</param>
    /// <returns>The Microsoft.AspNetCore.Builder.IEndpointConventionBuilder.</returns>
    public static IEndpointConventionBuilder ProducesProblem(this IEndpointConventionBuilder builder,
        int statusCode = StatusCodes.Status500InternalServerError,
        string contentType = "application/problem+json")
    {
        return Produces<ProblemDetails>(builder, statusCode, contentType);
    }

    /// <summary>
    /// Adds metadata indicating that the endpoint produces a Problem Details response including validation errors.
    /// </summary>
    /// <param name="builder">The Microsoft.AspNetCore.Builder.IEndpointConventionBuilder.</param>
    /// <param name="statusCode">The response status code. Defatuls to StatusCodes.Status400BadRequest.</param>
    /// <param name="contentType">The response content type. Defaults to "application/problem+json".</param>
    /// <returns>The Microsoft.AspNetCore.Builder.IEndpointConventionBuilder.</returns>
    public static IEndpointConventionBuilder ProducesValidationProblem(this IEndpointConventionBuilder builder,
        int statusCode = StatusCodes.Status400BadRequest,
        string contentType = "application/problem+json")
    {
        return Produces<HttpValidationProblemDetails>(builder, statusCode, contentType);
    }
}
