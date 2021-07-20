using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Formatters;

namespace Microsoft.AspNetCore.Mvc;

// Intent is to support (status-code, (content-type, schema[]))[] like OpenAPI itself does, e.g. https://github.com/domaindrivendev/Swashbuckle.AspNetCore/issues/1691
// In the framework we would likely just fix the existing ProducesResponseTypeAttribute class to support setting the content-type(s)
public class ProducesMetadataAttribute : ProducesResponseTypeAttribute, IApiResponseMetadataProvider
{
    public ProducesMetadataAttribute(int statusCode)
        : base(statusCode)
    {

    }

    public ProducesMetadataAttribute(Type type, int statusCode)
        : base(type, statusCode)
    {

    }

    public ProducesMetadataAttribute(Type? type, int statusCode, string? contentType, params string[] additionalContentTypes)
        : base(statusCode)
    {
        if (type is Type)
        {
            Type = type;
        }

        if (!string.IsNullOrEmpty(contentType))
        {
            // We want to ensure that the given provided content types are valid values, so
            // we validate them using the semantics of MediaTypeHeaderValue.
            MediaTypeHeaderValue.Parse(contentType);

            for (var i = 0; i < additionalContentTypes.Length; i++)
            {
                MediaTypeHeaderValue.Parse(additionalContentTypes[i]);
            }

            ContentTypes = GetContentTypes(contentType, additionalContentTypes);
        }
    }

    public MediaTypeCollection ContentTypes { get; set; } = new();

    public void SetContentTypes(MediaTypeCollection contentTypes)
    {
        contentTypes.Clear();
        foreach (var contentType in ContentTypes)
        {
            contentTypes.Add(contentType);
        }
    }

    private MediaTypeCollection GetContentTypes(string firstArg, string[] args)
    {
        var completeArgs = new List<string>(args.Length + 1);
        completeArgs.Add(firstArg);
        completeArgs.AddRange(args);
        var contentTypes = new MediaTypeCollection();
        foreach (var arg in completeArgs)
        {
            var contentType = new MediaType(arg);
            if (contentType.HasWildcard)
            {
                throw new InvalidOperationException("Don't do this");
            }

            contentTypes.Add(arg);
        }

        return contentTypes;
    }
}