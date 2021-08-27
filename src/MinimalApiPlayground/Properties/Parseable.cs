using System.Diagnostics.CodeAnalysis;

class Parseable<T> : IParseable<Parseable<T>> where T : IParseable<T>
{
    public Parseable()
    {

    }

    public Parseable(T? value)
    {
        Value = value;
    }

    public T? Value { get; set; }

    public static Parseable<T> Parse(string value, IFormatProvider? provider)
    {
        if (!TryParse(value, provider, out var result))
        {
            throw new ArgumentException("Could not parse supplied value.", nameof(value));
        }

        return result;
    }

    public static bool TryParse([NotNullWhen(true)] string? value, IFormatProvider? provider, out Parseable<T> result)
    {
        T innerValue;
        var parsed = T.TryParse(value, provider, out innerValue);
        if (parsed)
        {
            result = new Parseable<T>(innerValue);
        }
        else
        {
            result = new Parseable<T>();
        }
        return parsed;
    }
}