using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;

//[assembly: HostingStartup(typeof(RouteEndpointAddressSchemeStartup))]

public class RouteEndpointAddressSchemeStartup : IHostingStartup
{
    public void Configure(IWebHostBuilder builder)
    {
        builder.ConfigureServices((context, services) =>
        {
            services.AddEndpointAddressSchemes();
        });
    }
}

public class RouteEndpointInstanceAddressScheme : IEndpointAddressScheme<RouteEndpoint>
{
    private readonly IEnumerable<EndpointDataSource> _endpointSources;

    public RouteEndpointInstanceAddressScheme(IEnumerable<EndpointDataSource> endpointSources)
    {
        _endpointSources = endpointSources;
    }

    public IEnumerable<Endpoint> FindEndpoints(RouteEndpoint endpoint)
    {
        foreach (var endpointSource in _endpointSources)
        {
            foreach (var ep in endpointSource.Endpoints)
            {
                if (ep == endpoint)
                {
                    yield return ep;
                }
            }
        }
    }
}

public static class EndpointInstanceAddressSchemeServiceCollectionExtensions
{
    public static IServiceCollection AddEndpointAddressSchemes(this IServiceCollection services)
    {
        services.AddSingleton<IEndpointAddressScheme<RouteEndpoint>, RouteEndpointInstanceAddressScheme>();
        return services;
    }
}