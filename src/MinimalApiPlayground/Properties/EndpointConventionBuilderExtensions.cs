using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApiExplorer;

namespace Microsoft.AspNetCore.Http;

public static class EndpointConventionBuilderExtensions
{
    public static MinimalActionEndpointConventionBuilder WithTags(this MinimalActionEndpointConventionBuilder builder, params string[] tags)
    {
        builder.WithMetadata(new TagsAttribute(tags));

        return builder;
    }

    public static MinimalActionEndpointConventionBuilder Accepts<TRequest>(this MinimalActionEndpointConventionBuilder builder, string? contentType = null, params string[] otherContentTypes)
    {
        Accepts(builder, typeof(TRequest), contentType, otherContentTypes);

        return builder;
    }

    public static MinimalActionEndpointConventionBuilder Accepts(this MinimalActionEndpointConventionBuilder builder, Type requestType, string? contentType = null, params string[] otherContentTypes)
    {
        builder.WithMetadata(
            new ConsumesRequestTypeAttribute(requestType, contentType ?? "application/json", otherContentTypes)
        );

        return builder;
    }

    public static MinimalActionEndpointConventionBuilder AcceptsFormFile(this MinimalActionEndpointConventionBuilder builder, string fieldName)
    {
        builder.WithMetadata(new ConsumesRequestTypeAttribute("multipart/form-data"));
        builder.WithMetadata(new ApiParameterDescription { Name = fieldName, Source = Mvc.ModelBinding.BindingSource.FormFile });

        return builder;
    }

    public static MinimalActionEndpointConventionBuilder RequiresAntiforgery(this MinimalActionEndpointConventionBuilder builder)
    {
        builder.WithMetadata(new AntiforgeryMetadata());

        return builder;
    }
}