using System.Net;
using Xunit;

namespace MinimalApiPlayground.Tests;

public partial class Swagger
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
}
