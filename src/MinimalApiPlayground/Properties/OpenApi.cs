using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

[assembly:HostingStartup(typeof(OpenApiConfiguration))]

public class OpenApiConfiguration : IHostingStartup, IStartupFilter
{
    public void Configure(IWebHostBuilder builder)
    {
        builder.ConfigureServices((context, services) =>
        {
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
        services.AddSwaggerGen(options =>
        {
            //options.RequestBodyFilter<ConsumesRequestTypeRequestFilter>();
            options.OperationFilter<ConsumesRequestTypeRequestFilter>();
            options.TagActionsBy(TagsSelector);
            options.CustomSchemaIds(SchemaIdSelector);
            options.SwaggerDoc(Version, new OpenApiInfo { Title = hostingEnvironment.ApplicationName, Version = Version });
        });
        services.AddTransient<IStartupFilter, OpenApiConfiguration>();
    }

    internal static void ConfigureSwashbuckle(IApplicationBuilder app)
    {
        var env = app.ApplicationServices.GetRequiredService<IHostEnvironment>();

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
            if (controller is object)
            {
                tags.Add(controller);
            }
        }

        return tags;
    }

    private static string SchemaIdSelector(Type type)
    {
        if (type.CustomAttributes.Any(a => a.AttributeType == typeof(CompilerGeneratedAttribute)))
        {
            var match = Regex.Match(type.Name, @"AnonymousType\d+");
            return match.Success
                ? $"{type.Assembly.GetName().Name}.{match.Value}"
                : type.Name;
        }

        return type.Name;
    }
}

public class ConsumesRequestTypeRequestFilter : IRequestBodyFilter, IOperationFilter
{
    public void Apply(OpenApiRequestBody requestBody, RequestBodyFilterContext context)
    {
        throw new NotImplementedException();
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
                if (md.Type is object)
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

        var formFileParams = endpointMetadata.OfType<ApiParameterDescription>()
                                              .Where(pd => pd.Source == BindingSource.FormFile)
                                              .ToList();
        
        if (formFileParams.Count > 0)
        {
            operation.RequestBody = new();
            operation.RequestBody.Content["multipart/form-data"] = new() {  };

            foreach (var pd in formFileParams)
            {
                if (!operation.Parameters.Any(p => string.Equals(p.Name, pd.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    operation.Parameters.Add(new() { Name = pd.Name, Style = ParameterStyle.Form });
                }
            }
        }
    }
}