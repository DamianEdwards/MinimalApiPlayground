using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using Xunit;

namespace MinimalApiPlayground.Tests;

public partial class Swagger
{
    [Fact]
    public async Task SwaggerUI_Responds_OK_In_Development()
    {
        await using var application = new PlaygroundApplication("Development");

        var client = application.CreateClient();
        var response = await client.GetAsync("/docs/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task SwaggerUI_Redirects_To_Canonical_Path_In_Development()
    {
        await using var application = new PlaygroundApplication("Development");

        var client = application.CreateClient(new () { AllowAutoRedirect = false });
        var response = await client.GetAsync("/docs");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/docs/", response.Headers.Location?.ToString());
    }
}
