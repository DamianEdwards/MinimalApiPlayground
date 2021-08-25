using System.Text.Json;

namespace Microsoft.AspNetCore.Http;

public class JsonFormFile
{
    private static RequestDelegateResult BadRequestDelegate =
        RequestDelegateFactory.Create(() => Results.BadRequest());

    private static RequestDelegateResult UnsupportMediaTypeDelegate =
        RequestDelegateFactory.Create(() => Results.StatusCode(StatusCodes.Status415UnsupportedMediaType));

    private static JsonSerializerOptions _webJsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

    private IFormFile _formFile;

    public JsonFormFile(IFormFile formFile)
    {
        _formFile = formFile;
    }

    public static async ValueTask<object?> BindAsync(HttpContext context)
    {
        if (!context.Request.HasFormContentType)
        {
            await BadRequestDelegate.RequestDelegate(context);
            return null;
        }

        var form = await context.Request.ReadFormAsync();
        if (form.Files.Count != 1)
        {
            await BadRequestDelegate.RequestDelegate(context);
            return null;
        }

        var file = form.Files[0];
        if (file.ContentType != "application/json")
        {
            await UnsupportMediaTypeDelegate.RequestDelegate(context);
            return null;
        }

        var result = new JsonFormFile(file);
        return result;
    }

    public Stream OpenReadStream()
    {
        if (_formFile is IFormFile)
        {
            return _formFile.OpenReadStream();
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