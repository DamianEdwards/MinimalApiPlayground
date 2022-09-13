using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;


[assembly:HostingStartup(typeof(OpenApiConfiguration))]

public class OpenApiConfiguration : IHostingStartup, IStartupFilter
{
    public void Configure(IWebHostBuilder builder)
    {
        builder.ConfigureServices((context, services) =>
        {
            services.AddAntiforgery();
            ConfigureSwashbuckle(services, context.HostingEnvironment);
        });
    }

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            ConfigureSwashbuckle(app);
            next(app);
        };
    }

    private static readonly string Version = "v1";

    internal static void ConfigureSwashbuckle(IServiceCollection services, IWebHostEnvironment hostingEnvironment)
    {
        services.AddEndpointsApiExplorer();
        services.AddSingleton<IConfigureOptions<SwaggerGenOptions>, ConfigureSwaggerGenOptions>();
        services.AddSwaggerGen(options =>
        {
            //options.RequestBodyFilter<ConsumesRequestTypeRequestFilter>();
            options.SchemaFilter<XmlSchemaFilter>();
            options.InferSecuritySchemes();
            options.DocumentFilter<InferGlobalSecurityRequirementsFilter>();
            options.DocumentFilter<InferServersFilter>();
        });
        services.Configure<SwaggerGeneratorOptions>(options =>
        {
            options.InferSecuritySchemes = true;
        });
        services.AddTransient<IStartupFilter, OpenApiConfiguration>();
    }

    class ConfigureSwaggerGenOptions : IConfigureOptions<SwaggerGenOptions>
    {
        private readonly IWebHostEnvironment _hostingEnvironment;

        public ConfigureSwaggerGenOptions(IWebHostEnvironment hostingEnvironment)
        {
            _hostingEnvironment = hostingEnvironment;
        }

        public void Configure(SwaggerGenOptions options)
        {
            options.OperationFilter<ConsumesRequestTypeRequestFilter>();
            options.TagActionsBy(TagsSelector);
            options.CustomSchemaIds(SchemaIdSelector);
            options.CustomOperationIds(OperationIdSelector);
            options.SwaggerDoc(Version, new OpenApiInfo { Title = _hostingEnvironment.ApplicationName, Version = Version });
        }
    }

    internal static void ConfigureSwashbuckle(IApplicationBuilder app)
    {
        var env = app.ApplicationServices.GetRequiredService<IHostEnvironment>();

        app.UseHttpsRedirection();

        var rewriterOptions = new RewriteOptions();
        if (env.IsDevelopment())
        {
            // Configure rules for Swagger UI
            // redirect from 'docs' to 'docs/'
            rewriterOptions.AddRedirect($"^docs$", $"docs/");
            // rewrite 'docs/' to 'docs/index.html'
            rewriterOptions.AddRewrite($"^docs/$", $"docs/index.html", skipRemainingRules: false);
            // rewrite 'docs/*' to 'swagger/*'
            rewriterOptions.AddRewrite($"^docs/(.+)$", $"swagger/$1", skipRemainingRules: true);
        }
        // Configure rules for Swagger docs
        // rewrite 'openapi.json' to 'swagger/{Version}/swagger.json'
        rewriterOptions.AddRewrite($"^openapi.json$", $"swagger/{Version}/swagger.json", skipRemainingRules: true);
        app.UseRewriter(rewriterOptions);

        app.UseSwagger();

        if (env.IsDevelopment())
        {
            app.UseSwaggerUI(options =>
            {
                // NOTE: The leading slash is *very* important in the document path below as the JS served
                //       attempts to workaround a relative path issue that breaks the UI without it
                options.SwaggerEndpoint($"/swagger/{Version}/swagger.json", $"{env.ApplicationName} v1");
            });
        }
    }

    private static IList<string> TagsSelector(ApiDescription api)
    {
        var tags = new List<string>();

        foreach (var em in api.ActionDescriptor.EndpointMetadata)
        {
            if (em is ITagsMetadata itm)
            {
                tags.AddRange(itm.Tags);
            }
        }

        if (tags.Count == 0)
        {
            // Swashbuckle defaults to using the controller route value as a tag so add it here
            // if there wasn't more specific tag metadata present
            var controller = api.ActionDescriptor.RouteValues["controller"];
            if (controller is not null)
            {
                tags.Add(controller);
            }
        }

        return tags;
    }

    private static string SchemaIdSelector(Type modelType)
    {
        if (!modelType.IsConstructedGenericType)
            return modelType.Name.Replace("[]", "Array");

        var prefix = modelType.GetGenericArguments()
            .Select(genericArg => SchemaIdSelector(genericArg))
            .Aggregate((previous, current) => previous + current);

        var name = modelType.Name;

        if (modelType.CustomAttributes.Any(a => a.AttributeType == typeof(CompilerGeneratedAttribute)))
        {
            var match = Regex.Match(modelType.Name, @"AnonymousType\d+");
            name = match.Success
                ? match.Value
                : modelType.Name;
        }

        return prefix + name.Split('`').First();
    }

    private static string? OperationIdSelector(ApiDescription apiDescription)
    {
        // Default: https://github.com/domaindrivendev/Swashbuckle.AspNetCore/blob/cf7c50b70390a296f748d9a1f894823e2d9c7280/src/Swashbuckle.AspNetCore.SwaggerGen/SwaggerGenerator/SwaggerGeneratorOptions.cs#L63
        string? methodName = null;

        if (apiDescription.ActionDescriptor.EndpointMetadata.FirstOrDefault(e => e is MethodInfo) is MethodInfo methodInfo)
        {


            if (Roslyn.GeneratedNameParser.TryParseGeneratedName(methodInfo.Name, out Roslyn.GeneratedNameKind generatedNameKind, out int openBracketOffset, out int closeBracketOffset))
            {
                // Method name is compiler generated
                if (generatedNameKind == Roslyn.GeneratedNameKind.LocalFunction
                    && Roslyn.GeneratedNameParser.TryParseLocalFunctionName(methodInfo.Name, out string? localFunctionName))
                {
                    methodName = localFunctionName;
                }
                else
                {
                    methodName = null;
                }
            }
            else
            {
                methodName = methodInfo.Name;
            }
        }

        var actionDescriptor = apiDescription.ActionDescriptor;

        // Resolve the operation ID from the route name and fallback to the
        // endpoint name if no route name is available. This allows us to
        // generate operation IDs for endpoints that are defined using
        // minimal APIs.
        return
            actionDescriptor.AttributeRouteInfo?.Name
            ?? (actionDescriptor.EndpointMetadata.FirstOrDefault(m => m is IEndpointNameMetadata) as IEndpointNameMetadata)?.EndpointName
            ?? methodName;
    }
}

/// <summary>
/// Infers servers for a swagger document based on the configured address for the application's HTTP server
/// </summary>
internal class InferServersFilter : IDocumentFilter
{
    private readonly IServerAddressesFeature _serverAddresses;

    public InferServersFilter(IServer server)
    {
        _serverAddresses = server.Features.Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException($"Could not resolve {nameof(IServerAddressesFeature)} server feature");
    }

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        foreach (var address in _serverAddresses.Addresses)
        {
            swaggerDoc.Servers.Add(new() { Url = address });
        }
    }
}

/// <summary>
/// Infers global security requirements for a swagger document based on the security schemes contained in the document
/// </summary>
internal class InferGlobalSecurityRequirementsFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        foreach (var scheme in swaggerDoc.Components.SecuritySchemes)
        {
            swaggerDoc.SecurityRequirements.Add(new()
            {
                {
                    new() { Reference = new() { Type = ReferenceType.SecurityScheme, Id = scheme.Key } },
                    new string[0] // scopes
                }
            });
        }
    }
}

internal class XmlSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (string.Equals(schema.Type, "object", StringComparison.OrdinalIgnoreCase))
        {
            if (context.Type == typeof(Todo))
            {
                foreach (var prop in schema.Properties)
                {
                    // Convert from camelCase to PascalCase
                    if (prop.Key.Length > 0 && Char.IsLower(prop.Key[0]))
                    {
                        prop.Value.Xml = new OpenApiXml
                        {
                            Name = Char.ToUpper(prop.Key[0]) + prop.Key[1..]
                        };
                    }
                    
                }
            }
        }
    }
}

internal class ConsumesRequestTypeRequestFilter : IOperationFilter
{
    private readonly AntiforgeryOptions? _antiForgeryOptions;

    public ConsumesRequestTypeRequestFilter(IServiceProvider serviceProvider)
    {
        _antiForgeryOptions = serviceProvider.GetService<IOptions<AntiforgeryOptions>>()?.Value;
    }

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var endpointMetadata = context.ApiDescription.ActionDescriptor.EndpointMetadata;

        var consumesMetdata = endpointMetadata
                                  .Where(a => a as IApiRequestMetadataProvider2 != null)
                                  .Select(a => (IApiRequestMetadataProvider2)a)
                                  .ToList();

        if (consumesMetdata.Count > 0)
        {
            operation.RequestBody = new()
            {
                Required = true
            };

            foreach (var md in consumesMetdata)
            {
                OpenApiSchema? requestSchema = null;
                if (md.Type is not null)
                {
                    requestSchema = context.SchemaGenerator.GenerateSchema(md.Type, context.SchemaRepository);
                }

                var mediaTypes = new MediaTypeCollection();
                md.SetContentTypes(mediaTypes);
                foreach (var mediaType in mediaTypes)
                {
                    operation.RequestBody.Content[mediaType] = new() { Schema = requestSchema };
                }
            }
        }

        var antiforgeryMetadata = endpointMetadata.OfType<IAntiforgeryMetadata>()
                                                  .ToList();

        var requiresAntiForgeryToken = antiforgeryMetadata.Count > 0;

        if (requiresAntiForgeryToken && _antiForgeryOptions is not null)
        {
            if (!antiforgeryMetadata.Any(m => m is IDisableAntiforgery))
            {
                var xsrfHeaderTokenParameter = new OpenApiParameter
                {
                    In = ParameterLocation.Header,
                    Name = _antiForgeryOptions.HeaderName,
                    Schema = new OpenApiSchema
                    {
                        Type = "string"
                    }
                };
                operation.Parameters.Add(xsrfHeaderTokenParameter);

                var xsrfCookieTokenParameter = new OpenApiParameter
                {
                    In = ParameterLocation.Cookie,
                    Name = _antiForgeryOptions.Cookie.Name,
                    Schema = new OpenApiSchema
                    {
                        Type = "string"
                    }
                };
                operation.Parameters.Add(xsrfCookieTokenParameter);
            }
        }

        var formFileParams = endpointMetadata.OfType<ApiParameterDescription>()
                                             .Where(pd => pd.Source == BindingSource.FormFile)
                                             .ToList();
        
        if (formFileParams.Count > 0 || requiresAntiForgeryToken)
        {
            var properties = new Dictionary<string, OpenApiSchema>();

            foreach (var pd in formFileParams)
            {
                if (properties.ContainsKey(pd.Name)) continue;

                properties.Add(pd.Name, new OpenApiSchema { Type = "string", Format = "binary" });
            }

            if (requiresAntiForgeryToken && _antiForgeryOptions is not null)
            {
                //properties.Add(_antiForgeryOptions.FormFieldName, new OpenApiSchema { Type = "string" });
            }

            var schema = new OpenApiSchema
            {
                Type = "object",
                Properties = properties
            };

            operation.RequestBody = new();
            operation.RequestBody.Content["multipart/form-data"] = new OpenApiMediaType
            {
                Schema = schema,
                Encoding = schema.Properties.ToDictionary(
                    entry => entry.Key,
                    entry => new OpenApiEncoding { Style = ParameterStyle.Form }
                )
            };
        }
    }
}