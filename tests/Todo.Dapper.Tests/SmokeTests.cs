using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace TodoDapper.Tests;

public class SmokeTests
{
    [Fact]
    public async Task GetTodos_Returns_OK()
    {
        await using var application = new TodoApplication();

        var client = application.CreateClient();
        var response = await client.GetAsync("/todos");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetSwaggerDoc_Returns_OK()
    {
        await using var application = new TodoApplication();

        var client = application.CreateClient();
        var response = await client.GetAsync("/swagger/v1/swagger.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetSwaggerUI_Returns_OK()
    {
        await using var application = new TodoApplication();

        var client = application.CreateClient();
        var response = await client.GetAsync("/swagger/index.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    class TodoApplication : WebApplicationFactory<TodoApp>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            // Add mock/test services to the builder here
            builder.ConfigureServices(services =>
            {
                services.AddScoped<SqliteConnection>(sp =>
                {
                    // Replace SQL Lite with test DB
                    return new SqliteConnection("Data Source=testtodos.db");
                });
            });

            return base.CreateHost(builder);
        }
    }
}
