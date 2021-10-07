using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Xml.Serialization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Net.Http.Headers;
using MvcProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;

namespace MiniEssentials
{
    using MiniEssentials.Results;

    public static class ResultExtensions
    {
        public static Ok Ok(this IResultExtensions resultExtensions, string? message = null)
        {
            return new Ok(message);
        }

        public static Text Text(this IResultExtensions resultExtensions, string text, string? contentType = null)
        {
            return new Text(text, contentType);
        }

        public static BadRequest BadRequest(this IResultExtensions resultExtensions, int statusCode = StatusCodes.Status400BadRequest, string? message = null)
        {
            return new BadRequest(statusCode, message);
        }

        public static StatusCode StatusCode(this IResultExtensions resultExtensions, int statusCode, string? text, string? contentType = null)
        {
            return new StatusCode(statusCode, text, contentType);
        }

        public static ProblemDetails Problem(this IResultExtensions resultExtensions, string? detail = null, string? instance = null, int? statusCode = null, string? title = null, string? type = null, Dictionary<string, string>? extensions = null)
        {
            var problemDetails = new MvcProblemDetails
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

            return new ProblemDetails(problemDetails);
        }

        public static IResult Problem(this IResultExtensions resultExtensions, MvcProblemDetails problemDetails)
        {
            return new ProblemDetails(problemDetails);
        }

        public static IResult CreatedWithContentType<T>(this IResultExtensions resultExtensions, T responseBody, string contentType)
        {
            ArgumentNullException.ThrowIfNull(resultExtensions, nameof(resultExtensions));

            return new CreatedWithContentTypeResult<T>(responseBody, contentType);
        }

        public static IResult Html(this IResultExtensions resultExtensions, string html)
        {
            ArgumentNullException.ThrowIfNull(resultExtensions, nameof(resultExtensions));

            return new Html(html);
        }

        class CreatedWithContentTypeResult<T> : IResult
        {
            private readonly T _responseBody;
            private readonly string _contentType;

            public CreatedWithContentTypeResult(T responseBody, string contentType)
            {
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
        }
    }
}

namespace MiniEssentials.Results
{
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

        public ObjectResult(object value)
        {
            Value = value;
        }

        public object Value { get; }

        public abstract string DefaultContentType { get; }

        public string? ContentType { get; init; }

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

            await WriteResult(httpContext, resolvedContentTypeEncoding);
        }

        protected abstract Task WriteResult(HttpContext httpContext, Encoding contentTypeEncoding);
    }

    public class Json : ObjectResult
    {
        public Json(object value)
            : base(value)
        {

        }

        public JsonSerializerOptions? JsonSerializerOptions { get; init; }

        public override string DefaultContentType => "application/json; charset=utf-8";

        protected override async Task WriteResult(HttpContext httpContext, Encoding contentTypeEncoding)
        {
            await httpContext.Response.WriteAsJsonAsync(Value, JsonSerializerOptions, ContentType);
        }
    }

    public class ProblemDetails : Json
    {
        public ProblemDetails(MvcProblemDetails problemDetails)
            : base(problemDetails)
        {
            ContentType = "application/problem+json";
            ProblemDetailsValue = problemDetails;
            StatusCode = problemDetails.Status ??= ProblemDetailsValue is HttpValidationProblemDetails ?
                    StatusCodes.Status400BadRequest :
                    StatusCodes.Status500InternalServerError;
        }

        public MvcProblemDetails ProblemDetailsValue { get; }

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

    public class StatusCode : ContentResult
    {
        public StatusCode(int statusCode, string? text, string? contentType = null)
        {
            StatusCode = statusCode;
            ResponseContent = text;
            ContentType = contentType;
        }
    }

    public class Ok : StatusCode
    {
        public Ok(string? message = null)
            : base(StatusCodes.Status200OK, message)
        {

        }
    }

    public class Ok<TResult> : Json where TResult : notnull
    {
        public Ok(TResult result)
            : base(result)
        {

        }
    }

    public class Text : StatusCode
    {
        public Text(string text, string? contentType = null)
            : base(StatusCodes.Status200OK, text, contentType)
        {

        }
    }

    public class Html : Text
    {
        public Html(string html)
            : base(html, "text/html")
        {

        }
    }

    public class BadRequest : StatusCode
    {
        public BadRequest(int statusCode = StatusCodes.Status400BadRequest, string? message = null)
            : base(statusCode, message)
        {

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