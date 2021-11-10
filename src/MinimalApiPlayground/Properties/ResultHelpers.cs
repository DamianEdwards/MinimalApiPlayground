using System.Text;
using System.Xml.Serialization;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using Microsoft.Net.Http.Headers;
using MinimalApis.Extensions.Metadata;

public static class ResultExtensions
{
    public static CreatedJsonOrXml<TResult> CreatedJsonOrXml<TResult>(this IResultExtensions resultExtensions, TResult responseBody, string contentType)
    {
        ArgumentNullException.ThrowIfNull(resultExtensions, nameof(resultExtensions));

        global::CreatedJsonOrXml<TResult>.ThrowIfUnsupportedContentType(contentType);

        return new CreatedJsonOrXml<TResult>(responseBody, contentType);
    }
}

public class CreatedJsonOrXml<TResult> : IResult, IProvideEndpointResponseMetadata
{
    private readonly TResult _responseBody;
    private readonly string _contentType;

    public CreatedJsonOrXml(TResult responseBody, string contentType)
    {
        ThrowIfUnsupportedContentType(contentType);

        _responseBody = responseBody;
        _contentType = contentType;
    }

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        // Likely should honor Accpets header, etc.
        httpContext.Response.StatusCode = StatusCodes.Status201Created;
        httpContext.Response.ContentType = _contentType;

        switch (_contentType)
        {
            case "application/xml":
                // This is terrible code, don't do this
                var xml = new XmlSerializer(typeof(TResult));
                using (var ms = new MemoryStream())
                {
                    xml.Serialize(ms, _responseBody);
                    ms.Seek(0, SeekOrigin.Begin);
                    await ms.CopyToAsync(httpContext.Response.Body);
                }
                break;

            case "application/json":
            default:
                await httpContext.Response.WriteAsJsonAsync(_responseBody);
                break;
        }
    }

    public static IEnumerable<object> GetMetadata(Endpoint endpoint, IServiceProvider services)
    {
        yield return new Mvc.ProducesResponseTypeAttribute(typeof(TResult), StatusCodes.Status201Created, "application/json", "application/xml");
    }

    internal static void ThrowIfUnsupportedContentType(string contentType)
    {
        if (!string.Equals(contentType, "application/json", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(contentType, "application/xml", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Value provided for {contentType} must be either 'application/json' or 'application/xml'.", nameof(contentType));
        }
    }
}

internal static class ResponseContentTypeHelper
{
    /// <summary>
    /// Gets the content type and encoding that need to be used for the response.
    /// The priority for selecting the content type is:
    /// 1. ContentType property set on the action result
    /// 2. <see cref="HttpResponse.ContentType"/> property set on <see cref="HttpResponse"/>
    /// 3. Default content type set on the action result
    /// </summary>
    /// <remarks>
    /// The user supplied content type is not modified and is used as is. For example, if user
    /// sets the content type to be "text/plain" without any encoding, then the default content type's
    /// encoding is used to write the response and the ContentType header is set to be "text/plain" without any
    /// "charset" information.
    /// </remarks>
    public static void ResolveContentTypeAndEncoding(
        string? actionResultContentType,
        string? httpResponseContentType,
        (string defaultContentType, Encoding defaultEncoding) @default,
        Func<string, Encoding?> getEncoding,
        out string resolvedContentType,
        out Encoding resolvedContentTypeEncoding)
    {
        var (defaultContentType, defaultContentTypeEncoding) = @default;

        // 1. User sets the ContentType property on the action result
        if (actionResultContentType != null)
        {
            resolvedContentType = actionResultContentType;
            var actionResultEncoding = getEncoding(actionResultContentType);
            resolvedContentTypeEncoding = actionResultEncoding ?? defaultContentTypeEncoding;
            return;
        }

        // 2. User sets the ContentType property on the http response directly
        if (!string.IsNullOrEmpty(httpResponseContentType))
        {
            var mediaTypeEncoding = getEncoding(httpResponseContentType);
            if (mediaTypeEncoding != null)
            {
                resolvedContentType = httpResponseContentType;
                resolvedContentTypeEncoding = mediaTypeEncoding;
            }
            else
            {
                resolvedContentType = httpResponseContentType;
                resolvedContentTypeEncoding = defaultContentTypeEncoding;
            }

            return;
        }

        // 3. Fall-back to the default content type
        resolvedContentType = defaultContentType;
        resolvedContentTypeEncoding = defaultContentTypeEncoding;
    }

    public static Encoding? GetEncoding(string mediaType)
    {
        if (MediaTypeHeaderValue.TryParse(mediaType, out var parsed))
        {
            return parsed.Encoding;
        }

        return default;
    }
}