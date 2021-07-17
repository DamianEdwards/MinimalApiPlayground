using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Reflection;

namespace Microsoft.AspNetCore.Http;

public static class EndpointConventionBuilderExtensions
{
    static readonly string[] NoHttpMethods = new[] { "[none]" };

    public static IEndpointConventionBuilder WithName(this IEndpointConventionBuilder builder, string name)
    {
        //builder.Add(eb =>
        //    {
        //        var httpMethodMetadata = eb.Metadata.FirstOrDefault(i => i is HttpMethodMetadata) as HttpMethodMetadata;
        //        var methodInfo = eb.Metadata.FirstOrDefault(i => i is MethodInfo) as MethodInfo;

        //        Debug.WriteLine($"httpMethod.HttpMethods = {string.Join(',', httpMethodMetadata?.HttpMethods ?? NoHttpMethods)}");
        //        Debug.WriteLine($"methodInfo.Name = {methodInfo?.Name}");
        //    });
        //builder.WithMetadata(new RouteAttribute(null) { Name = name });
        //builder.WithMetadata(new RouteNameMetadata(name));
        builder.WithMetadata(new EndpointNameMetadata(name));

        return builder;
    }

    public static IEndpointConventionBuilder ProducesNotFound(this IEndpointConventionBuilder builder)
    {
        return builder.WithMetadata(new ProducesResponseTypeAttribute(StatusCodes.Status404NotFound));
    }

    public static IEndpointConventionBuilder ProducesProblem(this IEndpointConventionBuilder builder, int statusCode = StatusCodes.Status400BadRequest)
    {
        return builder.WithMetadata(new ProducesResponseTypeAttribute(typeof(ProblemDetails), statusCode));
    }

    public static IEndpointConventionBuilder ProducesValidationProblem(this IEndpointConventionBuilder builder)
    {
        return builder.WithMetadata(new ProducesResponseTypeAttribute(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest));
    }

    public static IEndpointConventionBuilder ProducesOk(this IEndpointConventionBuilder builder)
    {
        return Produces(builder, StatusCodes.Status200OK);
    }

    public static IEndpointConventionBuilder ProducesOk<TBody>(this IEndpointConventionBuilder builder)
    {
        return Produces<TBody>(builder, StatusCodes.Status200OK);
    }

    public static IEndpointConventionBuilder ProducesNoContent(this IEndpointConventionBuilder builder)
    {
        return Produces(builder, StatusCodes.Status204NoContent);
    }

    public static IEndpointConventionBuilder Produces<TBody>(this IEndpointConventionBuilder builder, int statusCode = StatusCodes.Status200OK)
    {
        return builder.WithMetadata(new ProducesResponseTypeAttribute(typeof(TBody), statusCode));
    }

    public static IEndpointConventionBuilder Produces(this IEndpointConventionBuilder builder, int statusCode)
    {
        return builder.WithMetadata(new ProducesResponseTypeAttribute(statusCode));
    }
}
