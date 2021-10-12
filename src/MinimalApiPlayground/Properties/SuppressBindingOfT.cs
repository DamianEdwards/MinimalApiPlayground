using System.Reflection;

/// <summary>
/// Suprresses the default binding logic of RequestDelegateFactory when accepted as a parameter to a route handler.
/// </summary>
/// <typeparam name="TValue">The <see cref="Type"/> of the parameter to suppress binding for.</typeparam>
public class SuppressBinding<TValue>
{
    public SuppressBinding(TValue? value)
    {
        Value = value;
    }
    public TValue? Value { get; }

    public static ValueTask<SuppressBinding<TValue?>> BindAsync(HttpContext httpContext, ParameterInfo parameter)
    {
        return ValueTask.FromResult(new SuppressBinding<TValue?>(default(TValue)));
    }
}
