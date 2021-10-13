using System.Collections.Immutable;
using System.Reflection;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MiniEssentials.Metadata;

namespace MiniEssentials;

public class EndpointProvidesMetadataApiDescriptionProvider : IApiDescriptionProvider
{
    private readonly IServiceProvider _services;
    private readonly EndpointDataSource _endpointDataSource;

    public EndpointProvidesMetadataApiDescriptionProvider(IServiceProvider services, EndpointDataSource endpointDataSource)
    {
        _services = services;
        _endpointDataSource = endpointDataSource;
    }

    public int Order => -1200;

    public void OnProvidersExecuting(ApiDescriptionProviderContext context)
    {

    }

    public void OnProvidersExecuted(ApiDescriptionProviderContext context)
    {
        foreach (var endpoint in _endpointDataSource.Endpoints.OfType<RouteEndpoint>())
        {
            var method = endpoint.Metadata.OfType<MethodInfo>().FirstOrDefault();
            if (method is null)
            {
                continue;
            }

            var excludeFromDescMetadata = endpoint.Metadata.OfType<ExcludeFromDescriptionAttribute>().FirstOrDefault();
            if (excludeFromDescMetadata is not null)
                continue;

            var returnType = AwaitableInfo.GetMethodReturnType(method);
            var parameters = method.GetParameters();
            var returnTypeProvidesMetadata = returnType.IsAssignableTo(typeof(IProvideEndpointResponseMetadata));
            var parametersProvideMetadata = parameters.Any(p => p.ParameterType.IsAssignableTo(typeof(IProvideEndpointParameterMetadata)));

            if (!returnTypeProvidesMetadata && !parametersProvideMetadata)
            {
                continue;
            }

            // Route handler delegate has a return type and/or parameters that can provide metadata
            var apiDescription = context.Results.FirstOrDefault(a => a.ActionDescriptor.EndpointMetadata.Contains(method));

            if (apiDescription is null) throw new InvalidOperationException($"Couldn't find existing {nameof(ApiDescription)} for endpoint with route '{endpoint.RoutePattern.RawText}'.");

            if (returnTypeProvidesMetadata)
            {
                var returnTypeMetadata = IProvideEndpointResponseMetadata.GetMetadataLateBound(returnType, endpoint, _services);
                foreach (var item in returnTypeMetadata)
                {
                    apiDescription.ActionDescriptor.EndpointMetadata.Add(item);
                }

                var responseMetadata = returnTypeMetadata.OfType<IApiResponseMetadataProvider>().ToList();

                if (apiDescription.SupportedResponseTypes.Count == 1 && responseMetadata.Count > 0)
                {
                    // Remove the default response type if we're going to add our own
                    var existingResponseType = apiDescription.SupportedResponseTypes[0];
                    if (existingResponseType.StatusCode == StatusCodes.Status200OK
                        && existingResponseType.Type == typeof(void))
                    {
                        apiDescription.SupportedResponseTypes.RemoveAt(0);
                    }
                }

                foreach (var responseType in responseMetadata)
                {
                    var apiResponseType = new ApiResponseType();
                    apiResponseType.StatusCode = responseType.StatusCode;
                    apiResponseType.Type = responseType.Type ?? typeof(void);
                    apiResponseType.ModelMetadata = CreateModelMetadata(apiResponseType.Type);

                    var contentTypes = new MediaTypeCollection();
                    responseType.SetContentTypes(contentTypes);

                    foreach (var format in contentTypes.Select(ct => new ApiResponseFormat { MediaType = ct }))
                    {
                        apiResponseType.ApiResponseFormats.Add(format);
                    }

                    // Swashbuckle doesn't support multiple response types with the same status code
                    if (!apiDescription.SupportedResponseTypes.Any(existingResponseType => existingResponseType.StatusCode == apiResponseType.StatusCode))
                    {
                        apiDescription.SupportedResponseTypes.Add(apiResponseType);
                    }
                }
            }

            if (parametersProvideMetadata)
            {
                foreach (var parameter in parameters)
                {
                    var parameterType = parameter.ParameterType;

                    if (!parameterType.IsAssignableTo(typeof(IProvideEndpointParameterMetadata)))
                    {
                        continue;
                    }

                    var parameterTypeMetadata = IProvideEndpointParameterMetadata.GetMetadataLateBound(parameter, _services);
                    foreach (var item in parameterTypeMetadata)
                    {
                        apiDescription.ActionDescriptor.EndpointMetadata.Add(item);
                    }

                    var parameterAcceptsMetadata = IProvideEndpointParameterMetadata.GetMetadataLateBound(parameter, _services).OfType<IAcceptsMetadata>().FirstOrDefault();
                    if (parameterAcceptsMetadata is null)
                    {
                        continue;
                    }

                    var acceptsRequestType = parameterAcceptsMetadata.RequestType;
                    var isOptional = parameterAcceptsMetadata.IsOptional;
                    var parameterDescription = new ApiParameterDescription
                    {
                        Name = acceptsRequestType is not null ? acceptsRequestType.Name : typeof(void).Name,
                        ModelMetadata = CreateModelMetadata(acceptsRequestType ?? typeof(void)),
                        Source = BindingSource.Body,
                        Type = acceptsRequestType ?? typeof(void),
                        IsRequired = !isOptional,
                    };
                    apiDescription.ParameterDescriptions.Add(parameterDescription);

                    var supportedRequestFormats = apiDescription.SupportedRequestFormats;
                    foreach (var contentType in parameterAcceptsMetadata.ContentTypes)
                    {
                        supportedRequestFormats.Add(new ApiRequestFormat
                        {
                            MediaType = contentType
                        });
                    }
                }
            }
        }
    }

    private static EndpointModelMetadata CreateModelMetadata(Type type)
    {
        return new EndpointModelMetadata(ModelMetadataIdentity.ForType(type));
    }
}

internal class EndpointModelMetadata : ModelMetadata
{
    public EndpointModelMetadata(ModelMetadataIdentity identity) : base(identity)
    {
        IsBindingAllowed = true;
    }

    public override IReadOnlyDictionary<object, object> AdditionalValues { get; } = ImmutableDictionary<object, object>.Empty;
    public override string? BinderModelName { get; }
    public override Type? BinderType { get; }
    public override BindingSource? BindingSource { get; }
    public override bool ConvertEmptyStringToNull { get; }
    public override string? DataTypeName { get; }
    public override string? Description { get; }
    public override string? DisplayFormatString { get; }
    public override string? DisplayName { get; }
    public override string? EditFormatString { get; }
    public override ModelMetadata? ElementMetadata { get; }
    public override IEnumerable<KeyValuePair<EnumGroupAndName, string>>? EnumGroupedDisplayNamesAndValues { get; }
    public override IReadOnlyDictionary<string, string>? EnumNamesAndValues { get; }
    public override bool HasNonDefaultEditFormat { get; }
    public override bool HideSurroundingHtml { get; }
    public override bool HtmlEncode { get; }
    public override bool IsBindingAllowed { get; }
    public override bool IsBindingRequired { get; }
    public override bool IsEnum { get; }
    public override bool IsFlagsEnum { get; }
    public override bool IsReadOnly { get; }
    public override bool IsRequired { get; }
    public override ModelBindingMessageProvider ModelBindingMessageProvider { get; } = new DefaultModelBindingMessageProvider();
    public override string? NullDisplayText { get; }
    public override int Order { get; }
    public override string? Placeholder { get; }
    public override ModelPropertyCollection Properties { get; } = new(Enumerable.Empty<ModelMetadata>());
    public override IPropertyFilterProvider? PropertyFilterProvider { get; }
    public override Func<object, object>? PropertyGetter { get; }
    public override Action<object, object?>? PropertySetter { get; }
    public override bool ShowForDisplay { get; }
    public override bool ShowForEdit { get; }
    public override string? SimpleDisplayProperty { get; }
    public override string? TemplateHint { get; }
    public override bool ValidateChildren { get; }
    public override IReadOnlyList<object> ValidatorMetadata { get; } = Array.Empty<object>();
}

public static class EndpointProvidesMetadataApiDescriptionProviderExtensions
{
    public static IServiceCollection AddEndpointsProvidesMetadataApiExplorer(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();

        services.TryAddEnumerable(
            ServiceDescriptor.Transient<IApiDescriptionProvider, EndpointProvidesMetadataApiDescriptionProvider>());

        return services;
    }
}