using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Xml.Serialization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Net.Http.Headers;

namespace MiniEssentials
{
    using MiniEssentials.Results;

    public static class ResultExtensions
    {
        public static Ok Ok(this IResultExtensions resultExtensions, string? message = null)
        {
            return new Ok(message);
        }

        public static Ok<TResult> Ok<TResult>(this IResultExtensions resultExtensions, TResult result)
        {
            return new Ok<TResult>(result);
        }

        public static Accepted Accepted(this IResultExtensions resultExtensions)
        {
            return new Accepted();
        }

        public static NoContent NoContent(this IResultExtensions resultExtensions)
        {
            return new NoContent();
        }

        public static Created Created(this IResultExtensions resultExtensions, string uri, object? value)
        {
            return new Created(uri, value);
        }

        public static Created<TResult> Created<TResult>(this IResultExtensions resultExtensions, string uri, TResult result)
        {
            return new Created<TResult>(uri, result);
        }

        public static Text Text(this IResultExtensions resultExtensions, string text, string? contentType = null)
        {
            return new Text(text, contentType);
        }

        public static PlainText PlainText(this IResultExtensions resultExtensions, string text)
        {
            return new PlainText(text);
        }

        public static NotFound NotFound(this IResultExtensions resultExtensions, string? message = null)
        {
            return new NotFound(message);
        }

        public static BadRequest BadRequest(this IResultExtensions resultExtensions, string? message = null, int statusCode = StatusCodes.Status400BadRequest)
        {
            return new BadRequest(message, statusCode);
        }

        public static UnprocessableEntity UnprocessableEntity(this IResultExtensions resultExtensions, string? message = null)
        {
            return new UnprocessableEntity(message);
        }

        public static UnsupportedMediaType UnsupportedMediaType(this IResultExtensions resultExtensions, string? message = null)
        {
            return new UnsupportedMediaType(message);
        }

        public static StatusCode StatusCode(this IResultExtensions resultExtensions, int statusCode, string? text, string? contentType = null)
        {
            return new StatusCode(statusCode, text, contentType);
        }

        public static Problem Problem(this IResultExtensions resultExtensions, string? detail = null, string? instance = null, int? statusCode = null, string? title = null, string? type = null, Dictionary<string, object>? extensions = null)
        {
            var problemDetails = new Mvc.ProblemDetails
            {
                Title = title,
                Detail = detail,
                Status = statusCode,
                Instance = instance,
                Type = type
            };
            if (extensions != null)
            {
                foreach (var extension in extensions)
                {
                    problemDetails.Extensions.Add(extension.Key, extension.Value);
                }
            }

            return new Problem(problemDetails);
        }

        public static ValidationProblem ValidationProblem(this IResultExtensions resultExtensions, Dictionary<string, string[]> errors)
        {
            return new ValidationProblem(errors);
        }

        public static ValidationProblem ValidationProblem(this IResultExtensions resultExtensions, IDictionary<string, string[]> errors)
        {
            return new ValidationProblem(errors);
        }

        public static IResult Problem(this IResultExtensions resultExtensions, Mvc.ProblemDetails problemDetails)
        {
            return new Problem(problemDetails);
        }

        public static CreatedJsonOrXml<TResult> CreatedJsonOrXml<TResult>(this IResultExtensions resultExtensions, TResult responseBody, string contentType)
        {
            ArgumentNullException.ThrowIfNull(resultExtensions, nameof(resultExtensions));

            Results.CreatedJsonOrXml<TResult>.ThrowIfUnsupportedContentType(contentType);

            return new CreatedJsonOrXml<TResult>(responseBody, contentType);
        }

        public static IResult Html(this IResultExtensions resultExtensions, string html)
        {
            ArgumentNullException.ThrowIfNull(resultExtensions, nameof(resultExtensions));

            return new Html(html);
        }
    }
}

namespace MiniEssentials.Results
{
    using MiniEssentials.Metadata;

    public abstract class ContentResult : IResult
    {
        private const string DefaultContentType = "text/plain; charset=utf-8";
        private static readonly Encoding DefaultEncoding = Encoding.UTF8;

        public string? ContentType { get; init; }

        public string? ResponseContent { get; init; }

        public int? StatusCode { get; init; }

        public async Task ExecuteAsync(HttpContext httpContext)
        {
            var response = httpContext.Response;

            ResponseContentTypeHelper.ResolveContentTypeAndEncoding(
                ContentType,
                response.ContentType,
                (DefaultContentType, DefaultEncoding),
                ResponseContentTypeHelper.GetEncoding,
                out var resolvedContentType,
                out var resolvedContentTypeEncoding);

            response.ContentType = resolvedContentType;

            if (StatusCode != null)
            {
                response.StatusCode = StatusCode.Value;
            }

            if (ResponseContent != null)
            {
                response.ContentLength = resolvedContentTypeEncoding.GetByteCount(ResponseContent);
                await response.WriteAsync(ResponseContent, resolvedContentTypeEncoding);
            }
        }
    }

    public abstract class ObjectResult : IResult
    {
        //protected const string DefaultContentType = "application/json; charset=utf-8";
        protected static readonly Encoding DefaultEncoding = Encoding.UTF8;

        public ObjectResult(object? value)
        {
            Value = value;
        }

        public object? Value { get; }

        public abstract string DefaultContentType { get; }

        public string? ContentType { get; init; }

        public int? StatusCode { get; init; }

        public virtual async Task ExecuteAsync(HttpContext httpContext)
        {
            var response = httpContext.Response;

            ResponseContentTypeHelper.ResolveContentTypeAndEncoding(
                ContentType,
                response.ContentType,
                (DefaultContentType, DefaultEncoding),
                ResponseContentTypeHelper.GetEncoding,
                out var resolvedContentType,
                out var resolvedContentTypeEncoding);

            response.ContentType = resolvedContentType;

            if (StatusCode != null)
            {
                response.StatusCode = StatusCode.Value;
            }

            await WriteResult(httpContext, resolvedContentTypeEncoding);
        }

        protected abstract Task WriteResult(HttpContext httpContext, Encoding contentTypeEncoding);
    }

    public class Json : ObjectResult
    {
        protected const string JsonContentType = "application/json";

        public Json(object? value)
            : base(value)
        {

        }

        public JsonSerializerOptions? JsonSerializerOptions { get; init; }

        public override string DefaultContentType => $"{JsonContentType}; charset=utf-8";

        protected override async Task WriteResult(HttpContext httpContext, Encoding contentTypeEncoding)
        {
            await httpContext.Response.WriteAsJsonAsync(Value, JsonSerializerOptions, ContentType);
        }
    }

    public class StatusCode : ContentResult
    {
        public StatusCode(int statusCode, string? text, string? contentType = null)
        {
            StatusCode = statusCode;
            ResponseContent = text;
            ContentType = contentType;
        }
    }

    public class Ok : StatusCode, IProvideEndpointResponseMetadata
    {
        protected const int ResponseStatusCode = StatusCodes.Status200OK;

        public Ok(string? message = null)
            : base(ResponseStatusCode, message)
        {

        }

        public static IEnumerable<object> GetMetadata(Endpoint endpoint, IServiceProvider services)
        {
            yield return new Mvc.ProducesResponseTypeAttribute(ResponseStatusCode);
        }
    }

    public class Ok<TResult> : Json, IProvideEndpointResponseMetadata
    {
        public Ok(TResult result)
            : base(result)
        {

        }

        public static IEnumerable<object> GetMetadata(Endpoint endpoint, IServiceProvider services)
        {
            yield return new Mvc.ProducesResponseTypeAttribute(typeof(TResult), StatusCodes.Status200OK, Json.JsonContentType);
        }
    }

    public class Created : Json, IProvideEndpointResponseMetadata
    {
        protected const int ResponseStatusCode = StatusCodes.Status201Created;

        public Created(string uri, object? value)
            : base(value)
        {
            Uri = uri;
            StatusCode = ResponseStatusCode;
        }

        public string Uri { get; }

        public override Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.Headers.Location = Uri;
            return base.ExecuteAsync(httpContext);
        }

        public static IEnumerable<object> GetMetadata(Endpoint endpoint, IServiceProvider services)
        {
            yield return new Mvc.ProducesResponseTypeAttribute(ResponseStatusCode);
        }
    }

    public class Created<TResult> : Created, IProvideEndpointResponseMetadata
    {
        public Created(string uri, TResult? value)
            : base(uri, value)
        {

        }

        public new static IEnumerable<object> GetMetadata(Endpoint endpoint, IServiceProvider services)
        {
            yield return new Mvc.ProducesResponseTypeAttribute(typeof(TResult), ResponseStatusCode, Json.JsonContentType);
        }
    }

    public class Accepted : StatusCode, IProvideEndpointResponseMetadata
    {
        private const int ResponseStatusCode = StatusCodes.Status202Accepted;

        public Accepted(string? message = null)
            : base(ResponseStatusCode, message)
        {

        }

        public static IEnumerable<object> GetMetadata(Endpoint endpoint, IServiceProvider services)
        {
            yield return new Mvc.ProducesResponseTypeAttribute(ResponseStatusCode);
        }
    }

    public class NoContent : StatusCode, IProvideEndpointResponseMetadata
    {
        private const int ResponseStatusCode = StatusCodes.Status204NoContent;

        public NoContent(string? message = null)
            : base(ResponseStatusCode, message)
        {

        }

        public static IEnumerable<object> GetMetadata(Endpoint endpoint, IServiceProvider services)
        {
            yield return new Mvc.ProducesResponseTypeAttribute(ResponseStatusCode);
        }
    }

    public class NotFound : StatusCode, IProvideEndpointResponseMetadata
    {
        private const int ResponseStatusCode = StatusCodes.Status404NotFound;

        public NotFound(string? message = null)
            : base(ResponseStatusCode, message)
        {

        }

        public static IEnumerable<object> GetMetadata(Endpoint endpoint, IServiceProvider services)
        {
            yield return new Mvc.ProducesResponseTypeAttribute(ResponseStatusCode);
        }
    }

    public class Text : StatusCode
    {
        public Text(string text, string? contentType = null)
            : base(StatusCodes.Status200OK, text, contentType)
        {

        }
    }

    public class PlainText : StatusCode, IProvideEndpointResponseMetadata
    {
        private const string PlainTextMediaType = "text/plain";

        public PlainText(string text)
            : base(StatusCodes.Status200OK, text, PlainTextMediaType)
        {

        }

        public static IEnumerable<object> GetMetadata(Endpoint endpoint, IServiceProvider services)
        {
            yield return new Mvc.ProducesAttribute(PlainTextMediaType);
        }
    }

    public class Html : Text, IProvideEndpointResponseMetadata
    {
        private const string HtmlMediaType = "text/html";

        public Html(string html)
            : base(html, HtmlMediaType)
        {

        }

        public static IEnumerable<object> GetMetadata(Endpoint endpoint, IServiceProvider services)
        {
            yield return new Mvc.ProducesAttribute(HtmlMediaType);
        }
    }

    public class BadRequest : StatusCode, IProvideEndpointResponseMetadata
    {
        private const int ResponseStatusCode = StatusCodes.Status400BadRequest;

        public BadRequest(string? message = null, int statusCode = ResponseStatusCode)
            : base(statusCode, message)
        {

        }

        public static IEnumerable<object> GetMetadata(Endpoint endpoint, IServiceProvider services)
        {
            yield return new Mvc.ProducesResponseTypeAttribute(ResponseStatusCode);
        }
    }

    public class UnprocessableEntity : StatusCode, IProvideEndpointResponseMetadata
    {
        private const int ResponseStatusCode = StatusCodes.Status422UnprocessableEntity;

        public UnprocessableEntity(string? message = null)
            : base(ResponseStatusCode, message)
        {

        }

        public static IEnumerable<object> GetMetadata(Endpoint endpoint, IServiceProvider services)
        {
            yield return new Mvc.ProducesResponseTypeAttribute(ResponseStatusCode);
        }
    }

    public class UnsupportedMediaType : StatusCode, IProvideEndpointResponseMetadata
    {
        public UnsupportedMediaType(string? message = null)
            : base(StatusCodes.Status415UnsupportedMediaType, message)
        {

        }

        public static IEnumerable<object> GetMetadata(Endpoint endpoint, IServiceProvider services)
        {
            yield return new Mvc.ProducesResponseTypeAttribute(StatusCodes.Status415UnsupportedMediaType);
        }
    }

    public class Problem : Json
    {
        protected const string ResponseContentType = "application/problem+json";

        public Problem(Mvc.ProblemDetails problemDetails)
            : base(problemDetails)
        {
            ContentType = ResponseContentType;
            ProblemDetailsValue = problemDetails;
            StatusCode = problemDetails.Status ??= StatusCodes.Status500InternalServerError;
        }

        public Mvc.ProblemDetails ProblemDetailsValue { get; }

        protected override async Task WriteResult(HttpContext httpContext, Encoding contentTypeEncoding)
        {
            if (StatusCode == null)
            {
                throw new InvalidOperationException("StatusCode should be set in constructor.");
            }

            ProblemDetailsValue.Status = StatusCode;

            if (ProblemDetailsDefaults.Defaults.TryGetValue(ProblemDetailsValue.Status.Value, out var defaults))
            {
                ProblemDetailsValue.Title ??= defaults.Title;
                ProblemDetailsValue.Type ??= defaults.Type;
            }

            if (!ProblemDetailsValue.Extensions.ContainsKey("requestId"))
            {
                ProblemDetailsValue.Extensions.Add("requestId", Activity.Current?.Id ?? httpContext.TraceIdentifier);
            }

            //await httpContext.Response.WriteAsJsonAsync(_problemDetails, _problemDetails.GetType(), options: null, contentType: "application/problem+json");

            await base.WriteResult(httpContext, contentTypeEncoding);
        }
    }

    public class ValidationProblem : Problem, IProvideEndpointResponseMetadata
    {
        private const int ResponseStatusCode = StatusCodes.Status400BadRequest;

        public ValidationProblem(IDictionary<string, string[]> errors)
            : base(new HttpValidationProblemDetails(errors)
            {
                Title = "One or more validation errors occurred.",
                Status = ResponseStatusCode
            })
        {

        }

        public static IEnumerable<object> GetMetadata(Endpoint endpoint, IServiceProvider services)
        {
            yield return new Mvc.ProducesResponseTypeAttribute(typeof(HttpValidationProblemDetails), ResponseStatusCode, ResponseContentType);
        }
    }

    public class CreatedJsonOrXml<T> : IResult, IProvideEndpointResponseMetadata
    {
        private readonly T _responseBody;
        private readonly string _contentType;

        public CreatedJsonOrXml(T responseBody, string contentType)
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
                    var xml = new XmlSerializer(typeof(T));
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
            yield return new Mvc.ProducesResponseTypeAttribute(typeof(T), StatusCodes.Status201Created, "application/json", "application/xml");
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
}