using System.Collections.Concurrent;
using System.Reflection;
using System.Reflection.Emit;

internal static class DefaultBinder<TValue>
{
    private static readonly string _itemsKeyS = "__DefaultBinder<TValue>_ValueResult_Key";
    private static readonly ConcurrentDictionary<(Type, ParameterInfo?), RequestDelegate> _delegateCache = new();

    public static async Task<(TValue?, int)> GetValueAsync(HttpContext httpContext, ParameterInfo? parameter = null)
    {
        ArgumentNullException.ThrowIfNull(httpContext, nameof(httpContext));

        var cacheKey = (typeof(TValue), parameter);
        var requestDelegate = _delegateCache.GetOrAdd(cacheKey, CreateRequestDelegateUsingRefEmit);

        var originalStatusCode = httpContext.Response.StatusCode;
        await requestDelegate(httpContext);
        var postBindingStatusCode = httpContext.Response.StatusCode;

        if (originalStatusCode != postBindingStatusCode)
        {
            // Default binder ran and detected an issue
            httpContext.Response.StatusCode = originalStatusCode;
            return (default(TValue?), postBindingStatusCode);
        }

        return ((TValue?)httpContext.Items[_itemsKeyS], StatusCodes.Status200OK);
    }

    private static MethodInfo CompletedTask = typeof(Task).GetMethod("get_CompletedTask")!;
    private static MethodInfo HttpContext_getItems = typeof(HttpContext).GetMethod("get_Items")!;
    private static MethodInfo Dictionary_setItem = typeof(IDictionary<object,object?>).GetMethod("set_Item")!;
    private static Type[] IResult_types = new[] { typeof(IResult) };
    private static Type[] ExecuteAsync_ParamTypes = new [] { typeof(HttpContext) };
    private static MethodInfo IResult_ExecuteAsync = typeof(IResult).GetMethod("ExecuteAsync")!;
    private static Type[] Execute_ParamTypes = new[] { typeof(TValue), typeof(HttpContext) };
    private static Type RouteHandler_DelegateType = typeof(Func<,,>).MakeGenericType(typeof(TValue), typeof(HttpContext), typeof(IResult));

    private static RequestDelegate CreateRequestDelegateUsingRefEmit((Type TargetType, ParameterInfo? Parameter) key)
    {
        // Module to generate:
        // class FakeResult : IResult
        // {
        //     public Task ExecuteAsync(HttpContext httpContext)
        //     {
        //         return Task.CompletedTask;
        //     }
        // }
        // public static class RouteHandler
        // {
        //     private static readonly IResult _result = new FakeResult();
        //     public static Task<IResult> Execute(SomeType value, HttpContext httpContext)
        //     {
        //         httpContext.Items[_itemsKey] = value;
        //         return _result;
        //     }
        // }

        var targetType = key.TargetType;
        var parameter = key.Parameter;
        var parameterName = parameter?.Name ?? "value";

        var assemblyName = $"{nameof(DefaultBinder<TValue>)}.Assembly.{targetType}.{parameterName}";
        var asm = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(assemblyName), AssemblyBuilderAccess.Run);
        var module = asm.DefineDynamicModule(assemblyName);

        // internal sealed class FakeResult : IResult {
        var fakeResultBuilder = module.DefineType("FakeResult", TypeAttributes.Class | TypeAttributes.Sealed, null, IResult_types);
        fakeResultBuilder.AddInterfaceImplementation(typeof(IResult));
        
        // public Task ExecuteAsync(HttpContext context) {
        var fakeResultExecuteAsync = fakeResultBuilder.DefineMethod("ExecuteAsync", MethodAttributes.Public | MethodAttributes.Virtual, typeof(Task), ExecuteAsync_ParamTypes);
        fakeResultExecuteAsync.DefineParameter(0, ParameterAttributes.None, "httpContext");
        fakeResultBuilder.DefineMethodOverride(fakeResultExecuteAsync, IResult_ExecuteAsync);
        var fakeResultExecuteAsyncIl = fakeResultExecuteAsync.GetILGenerator();
        fakeResultExecuteAsyncIl.EmitCall(OpCodes.Call, CompletedTask, null);
        fakeResultExecuteAsyncIl.Emit(OpCodes.Ret);

        Type fakeResultType = fakeResultBuilder.CreateType()!;
        var fakeResultTypeCtor = fakeResultType.GetConstructor(Type.EmptyTypes)!;

        // public sealed class RouteHandler {
        var routeHandlerBuilder = module.DefineType("RouteHandler", TypeAttributes.Class | TypeAttributes.Public | TypeAttributes.Sealed);

        // private static IResult _result = new FakeResult();
        var resultInstanceField = routeHandlerBuilder.DefineField("_result", typeof(IResult), FieldAttributes.Private | FieldAttributes.Static);
        var routeHandlerStaticCtor = routeHandlerBuilder.DefineConstructor(MethodAttributes.Private | MethodAttributes.Static, CallingConventions.Standard, null);
        var routeHandlerStaticCtorIl = routeHandlerStaticCtor.GetILGenerator();
        routeHandlerStaticCtorIl.Emit(OpCodes.Newobj, fakeResultTypeCtor);
        routeHandlerStaticCtorIl.Emit(OpCodes.Stsfld, resultInstanceField);
        routeHandlerStaticCtorIl.Emit(OpCodes.Ret);

        // public static IResult Execute(MyType value, HttpContext httpContext) {
        var routeHandlerExecute = routeHandlerBuilder.DefineMethod("Execute", MethodAttributes.Public | MethodAttributes.Static, typeof(IResult), Execute_ParamTypes);
        // Method parameters start at 1, 0 is the return value
        var param1 = routeHandlerExecute.DefineParameter(1, ParameterAttributes.None, parameterName);
        var param2 = routeHandlerExecute.DefineParameter(2, ParameterAttributes.None, "httpContext");
        
        // TODO: Clone attributes from original parameter on to generated parameter
        //       This might be impossible...
        //param1.SetCustomAttribute(new CustomAttributeBuilder())
        //var param1attr = parameter.GetCustomAttributes(false);
        //for (int i = 0; i < param1attr.Length; i++)
        //{
        //    var attr = param1attr[i];
        //}
        
        var routeHandlerExecuteIl = routeHandlerExecute.GetILGenerator();
        // httpContext.Items["MyItemsKey"] = value;
        routeHandlerExecuteIl.Emit(OpCodes.Ldarg_1);
        routeHandlerExecuteIl.EmitCall(OpCodes.Callvirt, HttpContext_getItems, null);
        routeHandlerExecuteIl.Emit(OpCodes.Ldstr, _itemsKeyS);
        routeHandlerExecuteIl.Emit(OpCodes.Ldarg_0);
        routeHandlerExecuteIl.EmitCall(OpCodes.Callvirt, Dictionary_setItem, null);
        // return _result;
        routeHandlerExecuteIl.Emit(OpCodes.Ldsfld, resultInstanceField);
        routeHandlerExecuteIl.Emit(OpCodes.Ret);

        var routeHandlerType = routeHandlerBuilder.CreateType()!;

        // Create the route handler delegate
        // Func<TargetType, HttpContext, IResult>
        var routeHandlerMethod = routeHandlerType.GetMethod("Execute")!;
        var routeHandlerDelegate = routeHandlerMethod.CreateDelegate(RouteHandler_DelegateType);
        
        return RequestDelegateFactory.Create(routeHandlerDelegate).RequestDelegate;
    }

    // BUG: This doesn't work right now as the parameters for dynamic methods can't have names!
    //      RequestDelegateFactory.Create throws if the delegate passed to it has unnamed parameters.
    //private static RequestDelegate CreateRequestDelegateUsingExpression((Type, ParameterInfo) key)
    //{
    //    var valueParam = Expression.Parameter(key.Item1, key.Item2.Name);
    //    var contextParam = Expression.Parameter(typeof(HttpContext), "httpContext");
    //    var itemsProp = Expression.Property(contextParam, "Items");
    //    var indexer = typeof(IDictionary<object, object>).GetProperty("Item");
    //    var itemsDictIndex = Expression.Property(itemsProp, indexer!, Expression.Constant(_itemsKey));
    //    var returnTarget = Expression.Label(typeof(IResult));

    //    var compiled = Expression.Lambda<Func<TValue, HttpContext, IResult>>(
    //        Expression.Block(
    //            Expression.Assign(itemsDictIndex, valueParam),
    //            Expression.Label(returnTarget, Expression.Constant(FakeResult.Instance))
    //        ),
    //        new[] { valueParam, contextParam})
    //        .Compile();

    //    return RequestDelegateFactory.Create(compiled).RequestDelegate;
    //}

    // We could do this whole class with just generics and the following method if not for the fact
    // that the RequestDelegateFactory passes the ParameterInfo to BindAsync for any parameters of types
    // that have a custom binding method, and that binding logic could potentially use the parameter name or
    // other aspects of the ParameterInfo, e.g. custom attributes.
    //private static IResult DefaultValueDelegate(TValue value, HttpContext httpContext)
    //{
    //    httpContext.Items[_itemsKey] = value;

    //    return FakeResult.Instance;
    //}
    //private class FakeResult : IResult
    //{
    //    public static FakeResult Instance { get; } = new FakeResult();

    //    public Task ExecuteAsync(HttpContext httpContext)
    //    {
    //        return Task.CompletedTask;
    //    }
    //}
}
