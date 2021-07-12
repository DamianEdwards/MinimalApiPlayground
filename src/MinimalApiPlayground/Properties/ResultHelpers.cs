using System.Diagnostics;
using System.Net.Mime;
using System.Text;

namespace Microsoft.AspNetCore.Http
{
    static class AppResults
    {
        public static IResult CreatedAt<T>(string routePattern, object routeValues, T responseBody) => new CreatedAtResult(routePattern, routeValues, responseBody);
        public static IResult Html(string html) => new HtmlResult(html);

        class CreatedAtResult : IResult
        {
            private readonly string? _url;
            private readonly string? _routePattern;
            private readonly object? _routeValues;
            private readonly object? _responseBody;

            public CreatedAtResult(string url, object? responseBody)
            {
                _url = url;
                _responseBody = responseBody;
            }

            public CreatedAtResult(string routePattern, object? routeValues, object? responseBody)
            {
                _routePattern = routePattern;
                _routeValues = routeValues;
                _responseBody = responseBody;
            }

            public async Task ExecuteAsync(HttpContext httpContext)
            {
                string? url;

                if (string.IsNullOrEmpty(_url) && !string.IsNullOrEmpty(_routePattern))
                {
                    var endpointSources = httpContext.RequestServices.GetRequiredService<IEnumerable<EndpointDataSource>>();
                    RouteEndpoint? targetEndpoint = null;
                    foreach (var endpointSource in endpointSources)
                    {
                        targetEndpoint = FindRouteEndpoint(endpointSource, HttpMethods.Get!, _routePattern);
                        if (targetEndpoint != null) break;
                    }

                    if (targetEndpoint == null)
                    {
                        throw new InvalidOperationException($"A route endpoint with pattern {_routePattern} was not found.");
                    }

                    var linkGenerator = httpContext.RequestServices.GetRequiredService<LinkGenerator>();
                    var routeValues = new RouteValueDictionary(_routeValues);
                    url = linkGenerator.GetPathByAddress(httpContext, targetEndpoint, routeValues);
                }
                else
                {
                    url = _url;
                }

                Debug.Assert(!string.IsNullOrEmpty(url));

                httpContext.Response.StatusCode = StatusCodes.Status201Created;
                httpContext.Response.Headers.Add("Location", url);

                if (_responseBody is object)
                {
                    await httpContext.Response.WriteAsJsonAsync(_responseBody, _responseBody.GetType());
                }
            }

            private static RouteEndpoint? FindRouteEndpoint(EndpointDataSource endpointDataSource, string httpMethod, string? routePattern)
            {
                foreach (var endpoint in endpointDataSource.Endpoints)
                {
                    var routeEndpoint = endpoint as RouteEndpoint;
                    if (routeEndpoint != null)
                    {
                        if ((routeEndpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Any(method => string.Equals(httpMethod, method)) ?? false)
                            && string.Equals(routeEndpoint.RoutePattern.RawText, routePattern, StringComparison.OrdinalIgnoreCase))
                        {
                            return routeEndpoint;
                        }
                    }
                }

                return null;
            }
        }

        class HtmlResult : IResult
        {
            private readonly string _html;

            public HtmlResult(string html)
            {
                _html = html;
            }

            public Task ExecuteAsync(HttpContext httpContext)
            {
                httpContext.Response.ContentType = MediaTypeNames.Text.Html;
                httpContext.Response.ContentLength = Encoding.UTF8.GetByteCount(_html);
                return httpContext.Response.WriteAsync(_html);
            }
        }
    }
}