using System.Xml.Serialization;

namespace System.IO;

public static class StreamExtensions
{
    public static async ValueTask<T?> ReadFromXmlAsync<T>(this HttpRequest httpRequest, long? contentLength) where T : new() =>
        await ReadFromXmlAsync<T>(httpRequest.Body, contentLength);

    public static async ValueTask<T?> ReadFromXmlAsync<T>(this Stream stream, long? contentLength) where T : new()
    {
        // This is terrible code, don't do this, seriously
        var buffer = new byte[contentLength ?? 1024];
        var read = await stream.ReadAsync(buffer);

        var xml = new XmlSerializer(typeof(T));
        using var ms = new MemoryStream(buffer);
        T? result = (T)xml.Deserialize(ms)!;

        return result;
    }

    public static Task WriteAsXmlAsync<T>(this Stream stream)
    {
        return Task.CompletedTask;
    }
}
