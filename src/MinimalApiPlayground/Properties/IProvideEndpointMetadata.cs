using System.Reflection;

namespace MiniEssentials.Metadata;

public interface IProvideEndpointResponseMetadata
{
    static abstract IEnumerable<object> GetMetadata(Endpoint endpoint, IServiceProvider services);

    internal static IEnumerable<object> GetMetadataLateBound(Type? type, Endpoint endpoint, IServiceProvider services)
    {
        var routeHandlerMethod = endpoint.Metadata.FirstOrDefault(m => m.GetType().IsAssignableTo(typeof(MethodInfo))) as MethodInfo;
        if (routeHandlerMethod is null && type is null)
        {
            return Enumerable.Empty<object>();
        }

        var targetType = type ?? AwaitableInfo.GetMethodReturnType(routeHandlerMethod);
        if (!targetType.IsAssignableTo(typeof(IProvideEndpointResponseMetadata)))
        {
            throw new ArgumentException($"Target type {targetType.FullName} must implement {nameof(IProvideEndpointResponseMetadata)}", nameof(targetType));
        }

        // TODO: Cache the method lookup and delegate creation? This is only called during first calls to ApiExplorer.
        var method = targetType.GetMethod(nameof(GetMetadata), BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

        if (method == null)
        {
            return Enumerable.Empty<object>();
        }

        // IEnumerable<object> GetMetadata(Endpoint endpoint, IServiceProvider services)
        var getMetadata = method.CreateDelegate<Func<Endpoint, IServiceProvider, IEnumerable<object>>>();
        var metadata = getMetadata(endpoint, services);

        return metadata ?? Enumerable.Empty<object>();
    }
}

public interface IProvideEndpointParameterMetadata
{
    static abstract IEnumerable<object> GetMetadata(ParameterInfo parameter, IServiceProvider services);

    internal static IEnumerable<object> GetMetadataLateBound(ParameterInfo parameter, IServiceProvider services)
    {
        var targetType = parameter.ParameterType;

        if (!targetType.IsAssignableTo(typeof(IProvideEndpointParameterMetadata)))
        {
            throw new ArgumentException($"Target type {targetType.FullName} must implement {nameof(IProvideEndpointParameterMetadata)}", nameof(targetType));
        }

        // TODO: Cache the method lookup and delegate creation? This is only called during first calls to ApiExplorer.
        var method = targetType.GetMethod(nameof(GetMetadata), BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

        if (method == null)
        {
            return Enumerable.Empty<object>();
        }

        // IEnumerable<object> GetMetadata(ParameterInfo parameter, IServiceProvider services)
        var getMetadata = method.CreateDelegate<Func<ParameterInfo, IServiceProvider, IEnumerable<object>>>();
        var metadata = getMetadata(parameter, services);

        return metadata ?? Enumerable.Empty<object>();
    }
}