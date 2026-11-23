using System.ComponentModel;

namespace Microsoft.AspNetCore.Http;

/// <summary>
/// A custom binder that uses <see cref="TypeConverter"/>s to convert from a <see cref="string"/> to the target <typeparamref name="TValue"/>.
/// </summary>
/// <typeparam name="TValue">The that providers a TypeConverter implementation</typeparam>
public readonly struct TypeConverter<TValue>
{
    // Cache the type coverter instance for this generic type
    private static readonly TypeConverter s_converter = TypeDescriptor.GetConverter(typeof(TValue));

    public TValue Value { get; }

    public TypeConverter(TValue value)
    {
        Value = value;
    }

    public static implicit operator TValue(TypeConverter<TValue> value) => value.Value;

    public override string ToString()
    {
        return Value!.ToString()!;
    }

    public static bool TryParse(string s, out TypeConverter<TValue> result)
    {
        if (s_converter is null || !s_converter.CanConvertFrom(typeof(string)))
        {
            result = default;
            return false;
        }

        var value = (TValue?)s_converter.ConvertFromInvariantString(s);

        if (value is null)
        {
            result = default;
            return false;
        }

        result = new(value);
        return true;
    }
}