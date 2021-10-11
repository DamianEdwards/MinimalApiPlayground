using System.Reflection;

namespace MiniEssentials.Results;

public abstract class ResultsBase : IResult
{
    protected ResultsBase(IResult activeResult)
    {
        Result = activeResult;
    }

    public IResult Result { get; }

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        await Result.ExecuteAsync(httpContext);
    }

    protected static IEnumerable<object> GetMetadata(params Type[] resultTypes)
    {
        var metadata = new List<object>();

        foreach (var resultType in resultTypes)
        {
            if (resultType.IsAssignableTo(typeof(IProvideEndpointMetadata)))
            {
                metadata.AddRange(IProvideEndpointMetadata.GetMetadata(resultType));
            }
        }

        return metadata;
    }
}

public sealed class Results<TResult1, TResult2> : ResultsBase, IProvideEndpointMetadata
    where TResult1 : IResult
    where TResult2 : IResult
{

    private Results(IResult activeResult) : base(activeResult)
    {

    }

    public static implicit operator Results<TResult1, TResult2>(TResult1 result) => new(result);

    public static implicit operator Results<TResult1, TResult2>(TResult2 result) => new(result);

    public static IEnumerable<object> GetMetadata() => GetMetadata(typeof(TResult1), typeof(TResult2));
}

public sealed class Results<TResult1, TResult2, TResult3> : ResultsBase, IProvideEndpointMetadata
    where TResult1 : IResult
    where TResult2 : IResult
    where TResult3 : IResult
{
    private Results(IResult activeResult) : base(activeResult)
    {

    }

    public static implicit operator Results<TResult1, TResult2, TResult3>(TResult1 result) => new(result);

    public static implicit operator Results<TResult1, TResult2, TResult3>(TResult2 result) => new(result);

    public static implicit operator Results<TResult1, TResult2, TResult3>(TResult3 result) => new(result);

    public static IEnumerable<object> GetMetadata() => GetMetadata(typeof(TResult1), typeof(TResult2), typeof(TResult3));
}

public sealed class Results<TResult1, TResult2, TResult3, TResult4> : ResultsBase, IProvideEndpointMetadata
    where TResult1 : IResult
    where TResult2 : IResult
    where TResult3 : IResult
    where TResult4 : IResult
{
    private Results(IResult activeResult) : base(activeResult)
    {

    }

    public static implicit operator Results<TResult1, TResult2, TResult3, TResult4>(TResult1 result) => new(result);

    public static implicit operator Results<TResult1, TResult2, TResult3, TResult4>(TResult2 result) => new(result);

    public static implicit operator Results<TResult1, TResult2, TResult3, TResult4>(TResult3 result) => new(result);

    public static implicit operator Results<TResult1, TResult2, TResult3, TResult4>(TResult4 result) => new(result);

    public static IEnumerable<object> GetMetadata() => GetMetadata(typeof(TResult1), typeof(TResult2), typeof(TResult3), typeof(TResult4));
}

public interface IProvideEndpointMetadata
{
    static abstract IEnumerable<object> GetMetadata();

    internal static IEnumerable<object> GetMetadata(Type targetType)
    {
        if (!targetType.IsAssignableTo(typeof(IProvideEndpointMetadata)))
        {
            throw new ArgumentException($"Target type {targetType.FullName} must implement {nameof(IProvideEndpointMetadata)}", nameof(targetType));
        }

        // TODO: Cache the method lookup and delegate creation?
        var method = targetType.GetMethod(nameof(GetMetadata), BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);

        if (method == null)
        {
            return Enumerable.Empty<object>();
        }

        var methodDelegate = method.CreateDelegate<Func<IEnumerable<object>>>();
        //var methodResult = method.Invoke(null, null);
        var methodResult = methodDelegate();

        return methodResult ?? Enumerable.Empty<object>();
    }
}