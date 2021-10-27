using System.Reflection;
using MinimalApis.Extensions.Metadata;

/// <summary>
/// Suprresses the default binding logic of RequestDelegateFactory when accepted as a parameter to a route handler.
/// </summary>
/// <typeparam name="TValue">The <see cref="Type"/> of the parameter to suppress binding for.</typeparam>
public class SuppressBinding<TValue> : IProvideEndpointParameterMetadata
{
    public SuppressBinding(TValue? value)
    {
        Value = value;
    }
    public TValue? Value { get; }

    public static ValueTask<SuppressBinding<TValue?>> BindAsync(HttpContext httpContext, ParameterInfo parameter)
    {
        return ValueTask.FromResult(new SuppressBinding<TValue?>(default));
    }

    public static IEnumerable<object> GetMetadata(ParameterInfo parameter, IServiceProvider services)
    {
        yield return new Mvc.ConsumesAttribute(typeof(TValue), "application/json");
    }
}
