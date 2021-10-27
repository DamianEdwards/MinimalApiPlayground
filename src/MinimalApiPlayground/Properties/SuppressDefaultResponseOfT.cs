using System.Reflection;
using MinimalApis.Extensions.Binding;
using MinimalApis.Extensions.Metadata;

/// <summary>
/// Suprresses the default response logic of RequestDelegateFactory when accepted as a parameter to a route handler.
/// Default binding of the <typeparamref name="TValue"/> will still occur.
/// </summary>
/// <typeparam name="TValue">The <see cref="Type"/> of the parameter.</typeparam>
public class SuppressDefaultResponse<TValue> : IProvideEndpointParameterMetadata
{
    public SuppressDefaultResponse(TValue? value, int statusCode)
    {
        Value = value;
        StatusCode = statusCode;
    }

    public SuppressDefaultResponse(Exception exception)
    {
        Exception = exception;
    }

    public TValue? Value { get; }

    public int StatusCode { get; }

    public Exception? Exception { get; }

    public static async ValueTask<SuppressDefaultResponse<TValue?>> BindAsync(HttpContext httpContext, ParameterInfo parameter)
    {
        try
        {
            // Manually invoke the default binding logic
            var (boundValue, statusCode) = await DefaultBinder<TValue>.GetValueAsync(httpContext);
            return new SuppressDefaultResponse<TValue?>(boundValue, statusCode);
        }
        catch (Exception ex)
        {
            // Exception occurred during binding!
            return new SuppressDefaultResponse<TValue?>(ex);
        }
    }

    public static IEnumerable<object> GetMetadata(ParameterInfo parameter, IServiceProvider services)
    {
        yield return new Mvc.ConsumesAttribute(typeof(TValue), "application/json");
    }
}
