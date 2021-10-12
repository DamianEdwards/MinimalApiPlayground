using System.Reflection;

public interface IInterfaceBinder<T> where T : IInterfaceBinder<T>
{
    public static async ValueTask<T?> BindAsync(HttpContext context, ParameterInfo parameter)
    {
        // Just JSON deserialize the body to TInput but this could be augmented to retrieve values for specific
        // properties on TInput from different parts of the request based on the property attributes like
        // FromQuery, FromRoute, etc.
        var input = await context.Request.ReadFromJsonAsync<T>();

        if (input != null)
        {
            // Leave fingerprint on the first string property
            var stringProp = input.GetType().GetProperties().FirstOrDefault(p =>
                p.PropertyType == typeof(string) && p.GetIndexParameters().Length == 0);
            
            if (stringProp != null)
            {
                var currentValue = stringProp.GetValue(input);
                stringProp.SetValue(input, $"{currentValue} [Bound via {nameof(IInterfaceBinder<T>)}]");
            }
        }

        return input;
    }
}
