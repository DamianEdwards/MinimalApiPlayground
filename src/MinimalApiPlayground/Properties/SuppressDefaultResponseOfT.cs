using System.Reflection;
using Microsoft.AspNetCore.Http.Metadata;
using MinimalApis.Extensions.Binding;

/// <summary>
/// Suprresses the default response logic of RequestDelegateFactory when accepted as a parameter to a route handler.
/// Default binding of the <typeparamref name="TValue"/> will still occur.
/// </summary>
/// <typeparam name="TValue">The <see cref="Type"/> of the parameter.</typeparam>
public class SuppressDefaultResponse<TValue> : IEndpointParameterMetadataProvider
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

    public static void PopulateMetadata(EndpointParameterMetadataContext context)
    {
        context.EndpointMetadata.Add(new Mvc.ConsumesAttribute(typeof(TValue), "application/json"));

    }
}
