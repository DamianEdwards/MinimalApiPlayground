using MiniEssentials.Metadata;

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

    protected static IEnumerable<object> GetMetadata(Endpoint endpoint, IServiceProvider services, params Type[] resultTypes)
    {
        var metadata = new List<object>();

        foreach (var resultType in resultTypes)
        {
            if (resultType.IsAssignableTo(typeof(IProvideEndpointResponseMetadata)))
            {
                metadata.AddRange(IProvideEndpointResponseMetadata.GetMetadataLateBound(resultType, endpoint, services));
            }
        }

        return metadata;
    }
}

public sealed class Results<TResult1, TResult2> : ResultsBase, IProvideEndpointResponseMetadata
    where TResult1 : IResult
    where TResult2 : IResult
{

    private Results(IResult activeResult) : base(activeResult)
    {

    }

    public static implicit operator Results<TResult1, TResult2>(TResult1 result) => new(result);

    public static implicit operator Results<TResult1, TResult2>(TResult2 result) => new(result);

    public static IEnumerable<object> GetMetadata(Endpoint endpoint, IServiceProvider services) => GetMetadata(endpoint, services, typeof(TResult1), typeof(TResult2));
}

public sealed class Results<TResult1, TResult2, TResult3> : ResultsBase, IProvideEndpointResponseMetadata
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

    public static IEnumerable<object> GetMetadata(Endpoint endpoint, IServiceProvider services) => GetMetadata(endpoint, services, typeof(TResult1), typeof(TResult2), typeof(TResult3));
}

public sealed class Results<TResult1, TResult2, TResult3, TResult4> : ResultsBase, IProvideEndpointResponseMetadata
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

    public static IEnumerable<object> GetMetadata(Endpoint endpoint, IServiceProvider services) => GetMetadata(endpoint, services, typeof(TResult1), typeof(TResult2), typeof(TResult3), typeof(TResult4));
}
