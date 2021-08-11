using Microsoft.AspNetCore.Mvc.Formatters;

namespace Microsoft.AspNetCore.Mvc.ApiExplorer;

public class ConsumesRequestTypeAttribute : ConsumesAttribute, IApiRequestMetadataProvider2
{
    public ConsumesRequestTypeAttribute(string contentType, params string[] otherContentTypes)
        : base(contentType, otherContentTypes)
    {

    }

    public ConsumesRequestTypeAttribute(Type type)
        : base("application/json")
    {
        Type = type ?? throw new ArgumentNullException(nameof(type));
    }

    public Type? Type { get; set; }
}

public interface IApiRequestMetadataProvider2 : IApiRequestMetadataProvider
{
    Type? Type { get; }
}