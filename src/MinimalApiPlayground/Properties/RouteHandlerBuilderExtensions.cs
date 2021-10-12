using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc.ApiExplorer;

namespace Microsoft.AspNetCore.Http;

public static class RouteHandlerBuilderExtensions
{
    public static RouteHandlerBuilder AcceptsFormFile(this RouteHandlerBuilder builder, string fieldName)
    {
        builder.WithMetadata(new ConsumesRequestTypeAttribute("multipart/form-data"));
        builder.WithMetadata(new ApiParameterDescription { Name = fieldName, Source = Mvc.ModelBinding.BindingSource.FormFile });

        return builder;
    }

    public static RouteHandlerBuilder RequiresAntiforgery(this RouteHandlerBuilder builder)
    {
        builder.WithMetadata(new AntiforgeryMetadata());

        return builder;
    }
}