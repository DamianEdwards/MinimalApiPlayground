using Microsoft.Net.Http.Headers;

namespace Microsoft.AspNetCore.Http;

public static class HttpContextExtensions
{
    private static readonly MediaTypeHeaderValue _jsonMediaType = new("application/json");

    /// <summary>
    /// Determines if the request accepts responses formatted as JSON via the <c>Accepts</c> header.
    /// </summary>
    /// <param name="httpRequest">The <see cref="HttpRequest"/>.</param>
    /// <returns><c>true</c> if the <c>Accept</c> header contains a media type compatible with "application/json".</returns>
    public static bool AcceptsJson(this HttpRequest httpRequest) => Accepts(httpRequest, _jsonMediaType);

    /// <summary>
    /// Determines if the request accepts responses formatted as the specified media type via the <c>Accepts</c> header.
    /// </summary>
    /// <param name="httpRequest">The <see cref="HttpRequest"/>.</param>
    /// <param name="mediaType">The media type.</param>
    /// <returns><c>true</c> if the <c>Accept</c> header contains a compatible media type.</returns>
    public static bool Accepts(this HttpRequest httpRequest, string mediaType) =>
        Accepts(httpRequest, new MediaTypeHeaderValue(mediaType));

    /// <summary>
    /// Determines if the request accepts responses formatted as the specified media type via the <c>Accepts</c> header.
    /// </summary>
    /// <param name="httpRequest">The <see cref="HttpRequest"/>.</param>
    /// <param name="mediaType">The <see cref="MediaTypeHeaderValue"/>.</param>
    /// <returns><c>true</c> if the <c>Accept</c> header contains a compatible media type.</returns>
    public static bool Accepts(this HttpRequest httpRequest, MediaTypeHeaderValue mediaType)
    {
        if (httpRequest.GetTypedHeaders().Accept is { Count: > 0 } acceptHeader)
        {
            for (var i = 0; i < acceptHeader.Count; i++)
            {
                var acceptHeaderValue = acceptHeader[i];

                if (mediaType.IsSubsetOf(acceptHeaderValue))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
