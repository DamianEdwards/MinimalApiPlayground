using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.OpenApi.Models;

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
            options.CustomOperationIds(BuildOperationId);
            options.DocInclusionPredicate(IncludeApiInDoc);
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

    private static string? BuildOperationId(ApiDescription api)
    {
        var httpMethod = api.HttpMethod;
        var controller = api.ActionDescriptor.RouteValues["controller"];
        var displayName = api.ActionDescriptor.DisplayName;
        
        // Following line relies on https://github.com/dotnet/aspnetcore/pull/34065
        var endpointNameMetadata = api.ActionDescriptor.EndpointMetadata.FirstOrDefault(m => m is EndpointNameMetadata) as EndpointNameMetadata;

        if (!string.IsNullOrEmpty(endpointNameMetadata?.EndpointName))
        {
            return endpointNameMetadata.EndpointName;
        }

        // Swashbuckle default: https://github.com/domaindrivendev/Swashbuckle.AspNetCore/blob/95cb4d370e08e54eb04cf14e7e6388ca974a686e/src/Swashbuckle.AspNetCore.SwaggerGen/SwaggerGenerator/SwaggerGeneratorOptions.cs#L64
        return api.ActionDescriptor.AttributeRouteInfo?.Name;
    }

    private static bool IncludeApiInDoc(string documentName, ApiDescription apiDescription)
    {
        var endpointIgnoreMetadata = apiDescription.ActionDescriptor.EndpointMetadata.FirstOrDefault(m => m is IEndpointIgnoreMetadata);

        if (endpointIgnoreMetadata is IEndpointIgnoreMetadata)
        {
            return false;
        }

        return string.IsNullOrEmpty(apiDescription.GroupName) || string.Equals(apiDescription.GroupName, documentName, StringComparison.OrdinalIgnoreCase);
    }
}