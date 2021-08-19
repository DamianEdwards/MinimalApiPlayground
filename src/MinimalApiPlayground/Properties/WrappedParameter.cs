using System.Globalization;
using System.Reflection;

interface IWrapped<out T> where T : new()
{
    T Value { get; }
}

class Wrapped<T> : IWrapped<T> where T : new()
{
    public Wrapped(T value)
    {
        Value = value;

        // Do stuff with value...
    }

    public T Value { get; }

    private static readonly Type[] TryParseParamTypes = new[] { typeof(string), typeof(T).MakeByRefType() };

    public static bool TryParse(string? input, out Wrapped<T>? value)
    {
        if (typeof(T) == typeof(bool))
        {
            var parsed = bool.TryParse(input, out var parsedValue);
            value = new Wrapped<T>((T)(object)parsedValue);
            return parsed;
        }
        else if (typeof(T) == typeof(int))
        {
            var parsed = int.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedValue);
            value = new Wrapped<T>((T)(object)parsedValue);
            return parsed;
        }
        else if (typeof(T) == typeof(long))
        {
            var parsed = long.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedValue);
            value = new Wrapped<T>((T)(object)parsedValue);
            return parsed;
        }
        else if (typeof(T) == typeof(float))
        {
            var parsed = float.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedValue);
            value = new Wrapped<T>((T)(object)parsedValue);
            return parsed;
        }
        else if (typeof(T) == typeof(double))
        {
            var parsed = double.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedValue);
            value = new Wrapped<T>((T)(object)parsedValue);
            return parsed;
        }
        else if (typeof(T) == typeof(decimal))
        {
            var parsed = decimal.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedValue);
            value = new Wrapped<T>((T)(object)parsedValue);
            return parsed;
        }
        else if (typeof(T) == typeof(byte))
        {
            var parsed = byte.TryParse(input, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedValue);
            value = new Wrapped<T>((T)(object)parsedValue);
            return parsed;
        }
        else if (typeof(T) == typeof(DateTime))
        {
            var parsed = DateTime.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsedValue);
            value = new Wrapped<T>((T)(object)parsedValue);
            return parsed;
        }
        else if (typeof(T) == typeof(DateTimeOffset))
        {
            var parsed = DateTimeOffset.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsedValue);
            value = new Wrapped<T>((T)(object)parsedValue);
            return parsed;
        }
        else if (typeof(T) == typeof(DateOnly))
        {
            var parsed = DateOnly.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsedValue);
            value = new Wrapped<T>((T)(object)parsedValue);
            return parsed;
        }
        else if (typeof(T) == typeof(TimeOnly))
        {
            var parsed = TimeOnly.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsedValue);
            value = new Wrapped<T>((T)(object)parsedValue);
            return parsed;
        }
        else if (typeof(T) == typeof(TimeSpan))
        {
            var parsed = TimeSpan.TryParse(input, CultureInfo.InvariantCulture, out var parsedValue);
            value = new Wrapped<T>((T)(object)parsedValue);
            return parsed;
        }
        else if (typeof(T).IsAssignableTo(typeof(IParseable<>)))
        {

        }
        else if (typeof(T) == typeof(Enum))
        {
            var parsed = Enum.TryParse(typeof(T), input, out var parsedValue);
            if (parsed && parsedValue is object)
            {
                value = new Wrapped<T>((T)parsedValue);
            }
            else
            {
                value = null;
            }
            return parsed;
        }
        else if (input != null)
        {
            var converter = System.ComponentModel.TypeDescriptor.GetConverter(typeof(T));
            var T = converter.ConvertFromString(input);
        }

        // Fallback to reflection
        var valueType = typeof(T);

        // Needs a method like: bool TryParse(string input, out T value)
        var tryParseMethod = valueType.GetMethod(nameof(TryParse), BindingFlags.Public | BindingFlags.Static, TryParseParamTypes);
        if (tryParseMethod is MethodInfo && tryParseMethod.ReturnType == typeof(bool))
        {
            var parseMethodParams = tryParseMethod.GetParameters();
            if (parseMethodParams.Length == 2)
            {
                var firstParam = parseMethodParams[0];
                var secondParam = parseMethodParams[1];

                if (firstParam.ParameterType == typeof(string) && secondParam.IsOut && secondParam.ParameterType == valueType.MakeByRefType())
                {
                    var parameters = new object?[] { input, null };

                    var parsedResult = tryParseMethod.Invoke(null, parameters);

                    if (parsedResult is bool parsed && parsed && parameters[1] is T tValue)
                    {
                        value = new Wrapped<T>(tValue);
                        return true;
                    }
                }
            }
        }

        value = null;
        return false;
    }
}