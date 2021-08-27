namespace MinimalApiPlayground.ModelBinding
{
    using System.Reflection;

    /// <summary>
    /// Represents a type that will use a registered <see cref="IParameterBinder<TModel>"/> to popuate a
    /// parameter of type <typeparamref name="TModel"/> to a route handler delegate.
    /// </summary>
    /// <typeparam name="TModel">The parameter type.</typeparam>
    public class Model<TModel>
    {
        private static readonly ParameterInfo _emptyParameter = new EmptyParameter();

        private readonly TModel? _value;

        public Model(TModel? modelValue)
        {
            _value = modelValue;
        }

        public TModel? Value => _value;

        private static Model<TModel> Create(TModel? value) => new Model<TModel>(value);

        public static implicit operator TModel?(Model<TModel> model) => model.Value;

        // RequestDelegateFactory discovers this method via reflection and code-gens calls to it to populate
        // parameter values for declared route handler delegates.
        public static async ValueTask<object?> BindAsync(HttpContext context)
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<Model<TModel>>>();

            var binder = LookupBinder(context.RequestServices, logger);

            var value = await binder.BindAsync(context, _emptyParameter);

            return Create(value);
        }

        private static IParameterBinder<TModel> LookupBinder(IServiceProvider services, ILogger logger)
        {
            // TODO: Is this lookup worth caching?
            var binder = services.GetService<IParameterBinder<TModel>>();

            if (binder is IParameterBinder<TModel>)
            {
                logger.LogDebug($"{nameof(IParameterBinder<object>)}<{{0}}> resolved from DI container.", typeof(TModel).Name);

                return binder;
            }

            logger.LogDebug($"{nameof(IParameterBinder<object>)}<{{0}}> could not be resovled from DI container, using default JSON binder.", typeof(TModel).Name);
            return DefaultJsonParameterBinder.Instance;
        }

        class EmptyParameter : ParameterInfo
        {
            public EmptyParameter() : base() { }
        }

        class DefaultJsonParameterBinder : IParameterBinder<TModel>
        {
            private DefaultJsonParameterBinder() { }

            public static IParameterBinder<TModel> Instance = new DefaultJsonParameterBinder();

            public async ValueTask<TModel?> BindAsync(HttpContext context, ParameterInfo parameter)
            {
                return await context.Request.ReadFromJsonAsync<TModel>(context.RequestAborted);
            }
        }
    }

    public interface IParameterBinder<T>
    {
        ValueTask<T?> BindAsync(HttpContext context, ParameterInfo parameter);
    }

    public interface IExtensionBinder<TSelf> where TSelf : IExtensionBinder<TSelf>
    {
        static abstract bool TryParse(string? value, out TSelf result);

        static abstract ValueTask<TSelf?> BindAsync(HttpContext context, ParameterInfo parameter);
    }
}

namespace Microsoft.Extensions.DependencyInjection
{
    using MinimalApiPlayground.ModelBinding;

    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers a type to use as a parameter binder for route handler delegates.
        /// </summary>
        /// <typeparam name="TBinder">The type to register as a parameter binder.</typeparam>
        /// <typeparam name="TModel">The parameter type to register the binder for.</typeparam>
        /// <param name="services">The IServiceCollection.</param>
        /// <returns>The IServiceCollection.</returns>
        public static IServiceCollection AddParameterBinder<TBinder, TModel>(this IServiceCollection services)
            where TBinder : class, IParameterBinder<TModel> =>
                services.AddSingleton<IParameterBinder<TModel>, TBinder>();
    }
}
