using System.Diagnostics.CodeAnalysis;

struct Parseable<T> : IParseable<Parseable<T>> where T : IParseable<T>
{
    public Parseable(T value)
    {
        Value = value;
    }

    public T Value { get; set; }

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
        var parsed = T.TryParse(value, provider, out var innerValue);
        if (parsed)
        {
            result = new Parseable<T>(innerValue);
        }
        else
        {
            result = default;
        }
        return parsed;
    }
}