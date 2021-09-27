internal static class DefaultBinder<TValue>
{
    private static object _itemsKey = new();
    private static readonly RequestDelegate _defaultRequestDelegate = RequestDelegateFactory.Create(DefaultValueDelegate).RequestDelegate;

    public static async Task<(TValue?, int)> GetValueAsync(HttpContext httpContext)
    {
        var originalStatusCode = httpContext.Response.StatusCode;

        await _defaultRequestDelegate(httpContext);

        if (originalStatusCode != httpContext.Response.StatusCode)
        {
            // Default binder ran and detected an issue
            httpContext.Response.StatusCode = originalStatusCode;
            return (default(TValue?), httpContext.Response.StatusCode);
        }

        return ((TValue?)httpContext.Items[_itemsKey], StatusCodes.Status200OK);
    }

    private static IResult DefaultValueDelegate(TValue value, HttpContext httpContext)
    {
        httpContext.Items[_itemsKey] = value;
        
        return FakeResult.Instance;
    }

    private class FakeResult : IResult
    {
        public static FakeResult Instance { get; } = new FakeResult();

        public Task ExecuteAsync(HttpContext httpContext)
        {
            return Task.CompletedTask;
        }
    }
}
