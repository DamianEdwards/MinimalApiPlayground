using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text.Json;
using MinimalApiPlayground.ModelBinding;

namespace Microsoft.AspNetCore.Http;

/// <summary>
/// Represents a validated object of the type specified by TTarget.
/// </summary>
/// <typeparam name="TValue">The type of the object being validated.</typeparam>
public class Validated<TValue> : IExtensionBinder<Validated<TValue>> where TValue : class
{
    private static readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public Validated(TValue value)
    {
        Value = value;
    }

    public TValue Value { get; }

    public bool IsValid
    {
        get => TryValidate(out var _);
    }

    public IDictionary<string, string[]> Errors { get; private set; } = new Dictionary<string, string[]>();

    public bool TryValidate(out IDictionary<string, string[]> errors)
    {
        var isValid = MinimalValidation.TryValidate(Value, out errors);
        Errors = errors;
        return isValid;
    }

    public static async ValueTask<Validated<TValue>?> BindAsync(HttpContext context, ParameterInfo parameter)
    {
        if (!context.Request.HasJsonContentType())
        {
            throw new BadHttpRequestException(
                "Request content type was not a recognized JSON content type.",
                StatusCodes.Status415UnsupportedMediaType);
        }

        var value = await context.Request.ReadFromJsonAsync<TValue>(_jsonSerializerOptions);

        return value == null ? null : new Validated<TValue>(value);
    }

    public void Deconstruct(out TValue value, out bool isValid)
    {
        value = Value;
        isValid = IsValid;
    }

    public void Deconstruct(out TValue value, out bool isValid, out IDictionary<string, string[]> errors)
    {
        value = Value;
        isValid = IsValid;
        errors = Errors;
    }
}