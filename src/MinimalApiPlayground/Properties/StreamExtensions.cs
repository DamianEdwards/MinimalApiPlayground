using System.Text.Json;
using System.Xml.Serialization;

namespace System.IO;

public static class StreamExtensions
{
    public static async ValueTask<T?> ReadAsJsonAsync<T>(this Stream stream) where T : new()
    {
        T? result = await JsonSerializer.DeserializeAsync<T>(stream);
        return result;
    }

    public static async ValueTask<T?> ReadAsXmlAsync<T>(this Stream stream, long? contentLength) where T : new()
    {
        // This is terrible code, don't do this, seriously
        var buffer = new byte[contentLength ?? 1024];
        await stream.ReadAsync(buffer);
        
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