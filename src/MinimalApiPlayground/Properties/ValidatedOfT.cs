using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Microsoft.AspNetCore.Http;

/// <summary>
/// Represents a validated object of the type specified by TTarget.
/// </summary>
/// <typeparam name="TValue">The type of the object being validated.</typeparam>
public class Validated<TValue> where TValue : class
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

    public static async ValueTask<Validated<TValue>?> BindAsync(HttpContext context)
    {
        var value = await context.Request.ReadFromJsonAsync<TValue>(_jsonSerializerOptions);

        if (value == null)
        {
            return null;
        }

        return new Validated<TValue>(value);
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