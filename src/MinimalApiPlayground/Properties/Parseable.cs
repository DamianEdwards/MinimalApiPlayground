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

    public static Parseable<T> Parse(string s, IFormatProvider? provider)
    {
        throw new NotImplementedException();
    }

    public static bool TryParse([NotNullWhen(true)] string? s, IFormatProvider? provider, out Parseable<T> result)
    {
        T innerValue;
        var parsed = T.TryParse(s, provider, out innerValue);
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