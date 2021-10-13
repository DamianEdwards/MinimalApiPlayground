namespace MinimalApiPlayground.ModelBinding
{
    using System.Collections.Generic;
    using System.Reflection;
    using MiniEssentials.Metadata;

    /// <summary>
    /// Represents a type that will use a registered <see cref="IParameterBinder<TModel>"/> to popuate a
    /// parameter of type <typeparamref name="TModel"/> to a route handler delegate.
    /// </summary>
    /// <typeparam name="TModel">The parameter type.</typeparam>
    public struct Model<TModel> : IExtensionBinder<Model<TModel?>>, IProvideEndpointParameterMetadata
    {
        private readonly TModel? _value;

        public Model(TModel? modelValue)
        {
            _value = modelValue;
        }

        public TModel? Value => _value;

        private static Model<TModel?> WrapResult(TModel? value) => new(value);

        public static implicit operator TModel?(Model<TModel> model) => model.Value;

        // RequestDelegateFactory discovers this method via reflection and code-gens calls to it to populate
        // parameter values for declared route handler delegates.
        public static async ValueTask<Model<TModel?>> BindAsync(HttpContext context, ParameterInfo parameter)
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<Model<TModel>>>();

            var binder = LookupBinder(context.RequestServices, logger);

            if (binder != null)
            {
                var value = await binder.BindAsync(context, parameter);
                return WrapResult(value);
            }

            var (defaultBinderResult, statusCode) = await DefaultBinder<TModel>.GetValueAsync(context, parameter);

            if (statusCode != StatusCodes.Status200OK)
            {
                // Binding issue
                throw new BadHttpRequestException("Bad request", statusCode);
            }

            return WrapResult(defaultBinderResult);
        }

        public static IEnumerable<object> GetMetadata(ParameterInfo parameter, IServiceProvider services)
        {
            var logger = services.GetRequiredService<ILogger<Model<TModel>>>();
            var binder = LookupBinder(services, logger);

            return binder switch
            {
                IProvideEndpointResponseMetadata => IProvideEndpointParameterMetadata.GetMetadataLateBound(parameter, services),
                _ => new[] { new Mvc.ConsumesAttribute(typeof(TModel), "application/json") }
            };
        }

        private const string Template_ResolvedFromDI = nameof(IParameterBinder<object>) + "<{ParameterBinderTargetTypeName}> resolved from DI container.";
        private const string Template_NotResolvedFromDI = nameof(IParameterBinder<object>) + "<{ParameterBinderTargetTypeName}> could not be resovled from DI container, using default binder.";

        private static IParameterBinder<TModel>? LookupBinder(IServiceProvider services, ILogger logger)
        {
            var binder = services.GetService<IParameterBinder<TModel>>();

            if (binder is not null)
            {
                logger.LogDebug(Template_ResolvedFromDI, typeof(TModel).Name);

                return binder;
            }

            logger.LogDebug(Template_NotResolvedFromDI, typeof(TModel).Name);

            return null;
        }
    }

    public interface IParameterBinder<T>
    {
        ValueTask<T?> BindAsync(HttpContext context, ParameterInfo parameter);
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
