using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc.ApiExplorer;

namespace Microsoft.AspNetCore.Http;

public static class EndpointConventionBuilderExtensions
{
    public static DelegateEndpointConventionBuilder AcceptsFormFile(this DelegateEndpointConventionBuilder builder, string fieldName)
    {
        builder.WithMetadata(new ConsumesRequestTypeAttribute("multipart/form-data"));
        builder.WithMetadata(new ApiParameterDescription { Name = fieldName, Source = Mvc.ModelBinding.BindingSource.FormFile });

        return builder;
    }

    public static DelegateEndpointConventionBuilder RequiresAntiforgery(this DelegateEndpointConventionBuilder builder)
    {
        builder.WithMetadata(new AntiforgeryMetadata());

        return builder;
    }
}