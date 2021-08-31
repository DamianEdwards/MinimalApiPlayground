using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net;
using Xunit;

namespace MinimalApiPlayground.Tests
{
    public class Swagger
    {
        [Fact(Skip = "https://github.com/dotnet/aspnetcore/issues/35956")]
        public async Task SwaggerUI_Responds_OK_In_Development()
        {
            await using var application = new PlaygroundApplication();
            
            var client = application.CreateClient();
            var response = await client.GetAsync("/docs/");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact(Skip = "https://github.com/dotnet/aspnetcore/issues/35956")]
        public async Task SwaggerUI_Redirects_To_Canonical_Path_In_Development()
        {
            await using var application = new PlaygroundApplication();


            var client = application.CreateClient();
            var response = await client.GetAsync("/docs");

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("/docs/", response.Headers.Location?.ToString());
        }

        class PlaygroundApplication : WebApplicationFactory<Program>
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
}