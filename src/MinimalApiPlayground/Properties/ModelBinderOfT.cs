using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Options;

namespace Microsoft.AspNetCore.Http;

/// <summary>
/// <para>A bridge to support MVC's model binders in minimal APIs. Specify <see cref="ModelBinder{T}"/> as a paramter type
/// of your method to trigger the model binding system.
/// </para>
/// <para>
/// This requires registering the model binding services by calling
/// <see cref="MvcCoreServiceCollectionExtensions.AddMvcCore"/> or <see cref="MvcServiceCollectionExtensions.AddControllers"/>.
/// </para>
/// </summary>
/// <typeparam name="T">The type to model bind</typeparam>
public class ModelBinder<T>
{
    // This caches the model binding information so we don't need to create one from a factory every time
    private static readonly ConcurrentDictionary<(ParameterInfo, IModelBinderFactory, IModelMetadataProvider),
                                                 (IModelBinder, BindingInfo, ModelMetadata)> _cache = new();

    /// <summary>
    /// The model being bound
    /// </summary>
    public T? Model { get; }

    /// <summary>
    /// The validation information.
    /// </summary>
    public ModelStateDictionary ModelState { get; }

    public ModelBinder(T? model, ModelStateDictionary modelState)
    {
        Model = model;
        ModelState = modelState;
    }

    public void Deconstruct(out T? model, out ModelStateDictionary modelState)
    {
        model = Model;
        modelState = ModelState;
    }

    public static async ValueTask<ModelBinder<T>> BindAsync(HttpContext context, ParameterInfo parameter)
    {
        var modelBinderFactory = context.RequestServices.GetRequiredService<IModelBinderFactory>();
        var modelMetadataProvider = context.RequestServices.GetRequiredService<IModelMetadataProvider>();
        var parameterBinder = context.RequestServices.GetRequiredService<ParameterBinder>();

        var (binder, bindingInfo, metadata) = _cache.GetOrAdd((parameter, modelBinderFactory, modelMetadataProvider), static arg =>
        {
            var (parameter, modelBinderFactory, modelMetadataProvider) = arg;

            ModelMetadata metadata = modelMetadataProvider.GetMetadataForType(typeof(T));

            var bindingInfo = BindingInfo.GetBindingInfo(parameter.GetCustomAttributes(), metadata);

            var binder = modelBinderFactory.CreateBinder(new ModelBinderFactoryContext
            {
                BindingInfo = bindingInfo,
                Metadata = metadata,
                CacheToken = parameter
            });

            return (binder!, bindingInfo!, metadata!);
        });

        // Resolve the value provider factories from MVC options
        var valueProviderFactories = context.RequestServices.GetRequiredService<IOptions<MvcOptions>>().Value.ValueProviderFactories;

        // We don't have an action descriptor, so just make up a fake one. Custom binders that rely on 
        // a specific action descriptor (like ControllerActionDescriptor, won't work).
        var actionContext = new ActionContext(context, context.GetRouteData(), new ActionDescriptor());

        var valueProvider = await CompositeValueProvider.CreateAsync(actionContext, valueProviderFactories);
        var paramterDescriptor = new ParameterDescriptor
        {
            BindingInfo = bindingInfo,
            Name = parameter.Name!,
            ParameterType = parameter.ParameterType
        };

        var result = await parameterBinder.BindModelAsync(actionContext, binder, valueProvider, paramterDescriptor, metadata, value: null, container: null);

        return new ModelBinder<T>((T?)result.Model, actionContext.ModelState);
    }
}