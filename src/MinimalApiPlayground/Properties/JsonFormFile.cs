using System.Text.Json;

namespace Microsoft.AspNetCore.Http;

/// <summary>
/// Represents the first JSON file in a multipart/form-data request (i.e. a form upload) typed as .
/// </summary>
public class JsonFormFile<TValue> : JsonFormFile
{
    public JsonFormFile(TValue value)
        : base()
    {
        Value = value;
    }

    public TValue? Value { get; }

    public new static async ValueTask<object?> BindAsync(HttpContext context)
    {
        var jsonFile = (JsonFormFile?)await JsonFormFile.BindAsync(context);
        
        if (jsonFile is JsonFormFile)
        {
            var value = await jsonFile.DeserializeAsync<TValue>();
            if (value is TValue)
            {
                return new JsonFormFile<TValue>(value);
            }
        }

        return null;
    }

    public override Stream OpenReadStream() =>
        throw new InvalidOperationException("Cannot open underlying file stream directly. Access value via the Value property.");
}

/// <summary>
/// Represents the first JSON file in a multipart/form-data request (i.e. a form upload).
/// </summary>
public class JsonFormFile
{
    private static JsonSerializerOptions _webJsonOptions = new (JsonSerializerDefaults.Web);

    protected IFormFile? FormFile;

    protected JsonFormFile()
    {

    }

    public JsonFormFile(IFormFile formFile)
    {
        FormFile = formFile;
    }

    public static async ValueTask<object?> BindAsync(HttpContext context)
    {
        if (!context.Request.HasFormContentType)
        {
            return null;
        }

        var form = await context.Request.ReadFormAsync();
        if (form.Files.Count != 1)
        {
            return null;
        }

        var file = form.Files[0];
        if (file.ContentType != "application/json")
        {
            return null;
        }

        var result = new JsonFormFile(file);
        return result;
    }

    public virtual Stream OpenReadStream()
    {
        if (FormFile is IFormFile)
        {
            return FormFile.OpenReadStream();
        }

        throw new InvalidOperationException("Cannot open the file read stream before BindAsync is called.");
    }

    public ValueTask<T?> DeserializeAsync<T>() => DeserializeAsync<T>(_webJsonOptions);

    public async ValueTask<T?> DeserializeAsync<T>(JsonSerializerOptions? jsonOptions = null)
    {
        using var fileStream = OpenReadStream();
        return await JsonSerializer.DeserializeAsync<T>(fileStream, jsonOptions);
    }
}