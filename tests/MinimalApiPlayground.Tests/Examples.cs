using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Net.Http.Headers;
using Xunit;

namespace MinimalApiPlayground.Tests
{
    public class Examples
    {
        [Fact]
        public async Task GET_Root_Responds_OK()
        {
            await using var application = new PlaygroundApplication();

            var client = application.CreateClient();
            var response = await client.GetAsync("/");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("Hello World!", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task GET_Hello_Responds_OK()
        {
            await using var application = new PlaygroundApplication();

            var client = application.CreateClient();
            var response = await client.GetAsync("/hello");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("{\"hello\":\"World\"}", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task GET_Goodbye_Responds_OK()
        {
            await using var application = new PlaygroundApplication();

            var client = application.CreateClient();
            var response = await client.GetAsync("/goodbye");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("{\"goodbye\":\"World\"}", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task GET_HelloFunc_Responds_OK()
        {
            await using var application = new PlaygroundApplication();

            var client = application.CreateClient();
            var response = await client.GetAsync("/hellofunc");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("Hello World from Endpoints.HelloWorldFunc", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task GET_JsonRaw_Responds_OK()
        {
            await using var application = new PlaygroundApplication();

            var client = application.CreateClient();
            var response = await client.GetAsync("/jsonraw");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("{\"Id\":123,\"Name\":\"Example\"}", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task POST_JsonRaw_Responds_OK()
        {
            await using var application = new PlaygroundApplication();

            var jsonString = "{\"test\":123}";
            var jsonContent = new StringContent(jsonString);
            jsonContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");

            var client = application.CreateClient();
            var response = await client.PostAsync("/jsonraw", jsonContent);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal($"Thanks for the JSON:\r\n{jsonString}", await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task POST_JsonRaw_With_NonJson_ContentType_Header_Responds_UnsupportedMediaType()
        {
            await using var application = new PlaygroundApplication();

            var jsonContent = new StringContent("");
            jsonContent.Headers.ContentType = MediaTypeHeaderValue.Parse("text/plain");

            var client = application.CreateClient();
            var response = await client.PostAsync("/jsonraw", jsonContent);

            Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        }

        [Fact]
        public async Task GET_Html_Responds_OK()
        {
            await using var application = new PlaygroundApplication();

            var client = application.CreateClient();
            var response = await client.GetAsync("/html");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        }

        class PlaygroundApplication : WebApplicationFactory<Program>
        {
            protected override IHost CreateHost(IHostBuilder builder)
            {
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