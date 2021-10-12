using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace MinimalApiPlayground.Tests;

internal class PlaygroundApplication : WebApplicationFactory<Program>
{
    private readonly string _environment;

    public PlaygroundApplication(string environment = "Development")
    {
        _environment = environment;
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.UseEnvironment(_environment);

        // Add mock/test services to the builder here
        builder.ConfigureServices(services =>
        {
            services.AddScoped(sp =>
            {
                // Replace SQLite with in-memory database for tests
                return new DbContextOptionsBuilder<TodoDb>()
                .UseInMemoryDatabase("Tests")
                .UseApplicationServiceProvider(sp)
                .Options;
            });
        });

        return base.CreateHost(builder);
    }
}
