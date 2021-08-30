using MinimalApiPlayground.ModelBinding;
using System.Reflection;
using System.Text.Json;

namespace Microsoft.AspNetCore.Http;

/// <summary>
/// Represents a JSON file in a multipart/form-data request (i.e. a form upload).
/// </summary>
public class JsonFormFile<TValue> : JsonFormFile, IExtensionBinder<JsonFormFile<TValue>>
{
    public JsonFormFile(TValue value)
        : base()
    {
        Value = value;
    }

    public TValue? Value { get; }

    public new static async ValueTask<JsonFormFile<TValue>?> BindAsync(HttpContext context, ParameterInfo parameter)
    {
        var jsonFile = await JsonFormFile.BindAsync(context, parameter);
        
        if (jsonFile is not null)
        {
            var value = await jsonFile.DeserializeAsync<TValue>();
            if (value is not null)
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
/// Represents a JSON file in a multipart/form-data request (i.e. a form upload).
/// </summary>
public class JsonFormFile : IExtensionBinder<JsonFormFile>
{
    private static readonly JsonSerializerOptions _webJsonOptions = new (JsonSerializerDefaults.Web);

    protected IFormFile? FormFile;

    protected JsonFormFile()
    {

    }

    public JsonFormFile(IFormFile formFile)
    {
        FormFile = formFile;
    }

    public static async ValueTask<JsonFormFile?> BindAsync(HttpContext context, ParameterInfo parameter)
    {
        if (!context.Request.HasFormContentType)
        {
            return null;
        }

        var fieldName = parameter.Name;
        var form = await context.Request.ReadFormAsync();

        if (!string.IsNullOrEmpty(fieldName)
            && (form.Files.GetFile(fieldName) is IFormFile file)
            && file.ContentType == "application/json")

        {
            return new JsonFormFile(file);
        }

        return null;
    }

    public virtual Stream OpenReadStream()
    {
        if (FormFile is not null)
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