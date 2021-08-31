using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

// HACK: Workardound for https://github.com/dotnet/aspnetcore/issues/35990
[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly)]

namespace MinimalApiPlayground.Tests
{
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
                    // Replace SQL Lite with test DB
                    return new SqliteConnection("Data Source=testtodos.db");
                });
            });

            return base.CreateHost(builder);
        }
    }
}