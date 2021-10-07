namespace MiniEssentials.Results;

public abstract class ResultsBase : IResult
{
    private readonly IResult _result;

    protected ResultsBase(IResult activeResult)
    {
        _result = activeResult;
    }

    public async Task ExecuteAsync(HttpContext httpContext)
    {
        await _result.ExecuteAsync(httpContext);
    }
}

public sealed class Results<TResult1, TResult2> : ResultsBase
    where TResult1 : IResult
    where TResult2 : IResult
{

    private Results(IResult activeResult) : base(activeResult)
    {

    }

    public static implicit operator Results<TResult1, TResult2>(TResult1 result) => new(result);

    public static implicit operator Results<TResult1, TResult2>(TResult2 result) => new(result);
}

public sealed class Results<TResult1, TResult2, TResult3> : ResultsBase
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
}

public sealed class Results<TResult1, TResult2, TResult3, TResult4> : ResultsBase
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
}