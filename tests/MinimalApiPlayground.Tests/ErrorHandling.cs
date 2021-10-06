using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace MinimalApiPlayground.Tests;

public class ErrorHandling
{
    [Fact]
    public async Task GET_Throw_Responds_500()
    {
        await using var application = new PlaygroundApplication("Production");

        using var client = application.CreateClient();
        using var response = await client.GetAsync("/throw");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    [Theory]
    // BUG: Skipping theory cases doesn't work in VS due to https://github.com/xunit/visualstudio.xunit/issues/266#issuecomment-530557920
    [InlineData(400, "Development", Skip = "Fixed in rc.2: https://github.com/dotnet/aspnetcore/issues/35857")]
    [InlineData(404, "Development", Skip = "Fixed in rc.2: https://github.com/dotnet/aspnetcore/issues/35857")]
    [InlineData(405, "Development", Skip = "Fixed in rc.2: https://github.com/dotnet/aspnetcore/issues/35857")]
    [InlineData(414, "Development", Skip = "Fixed in rc.2: https://github.com/dotnet/aspnetcore/issues/35857")]
    [InlineData(415, "Development", Skip = "Fixed in rc.2: https://github.com/dotnet/aspnetcore/issues/35857")]
    [InlineData(422, "Development", Skip = "Fixed in rc.2: https://github.com/dotnet/aspnetcore/issues/35857")]
    [InlineData(400, "Production")]
    [InlineData(404, "Production")]
    [InlineData(405, "Production")]
    [InlineData(414, "Production")]
    [InlineData(415, "Production")]
    [InlineData(422, "Production")]
    public async Task GET_Throw_With_400_StatusCode_Responds_WithStatusCode(int statusCode, string environment)
    {
        await using var application = new PlaygroundApplication(environment);

        using var client = application.CreateClient();
        using var response = await client.GetAsync($"/throw/{statusCode}");

        Assert.Equal((HttpStatusCode)statusCode, response.StatusCode);
    }

    [Fact]
    public async Task GET_Throw_Responds_With_ProblemDetails_In_Production()
    {
        await using var application = new PlaygroundApplication("Production");

        var problemDetails = await GET_Throw_Responds_With_ProblemDetails(application);
        Assert.NotNull(problemDetails?.Title);
        
        // Development only details
        Assert.Null(problemDetails?.Exception);
        Assert.Null(problemDetails?.Stack);
        Assert.Null(problemDetails?.Headers);
        Assert.Null(problemDetails?.RouteValues);
        Assert.Null(problemDetails?.Query);
        Assert.Null(problemDetails?.Endpoint);
    }

    [Fact]
    public async Task GET_Throw_Responds_With_ProblemDetails_In_Development()
    {
        await using var application = new PlaygroundApplication("Development");

        var problemDetails = await GET_Throw_Responds_With_ProblemDetails(application);

        // Development only details
        Assert.NotNull(problemDetails?.Exception);
        Assert.NotNull(problemDetails?.Stack);
        Assert.NotNull(problemDetails?.Headers);
        Assert.NotNull(problemDetails?.RouteValues);
        Assert.NotNull(problemDetails?.Query);
        Assert.NotNull(problemDetails?.Endpoint);
    }

    private async Task<ProblemDetails?> GET_Throw_Responds_With_ProblemDetails(PlaygroundApplication application)
    {
        using var client = application.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/throw");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await client.SendAsync(request);
        var contentType = response.Content.Headers.ContentType;
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(MediaTypeHeaderValue.Parse("application/problem+json"), contentType);
        Assert.Equal((int)HttpStatusCode.InternalServerError, problemDetails?.Status);
        Assert.NotNull(problemDetails?.Title);
        Assert.NotNull(problemDetails?.Detail);

        return problemDetails;
    }

    public class ProblemDetails
    {
        public string? Title {  get; set; }
        public int Status { get; set; }
        public string? Detail { get; set; }
        public string? Exception { get; set; }
        public string? Stack { get; set; }
        public IDictionary<string, string>? Headers { get; set; }
        public IDictionary<string, object>? RouteValues { get; set; }
        public IEnumerable<string>? Query { get; set; }
        public EndpointDetails? Endpoint { get; set; }
    }

    public class EndpointDetails
    {
        public string? DisplayName { get; set; }
        public string? RoutePattern { get; set; }
        public int RouteOrder { get; set; }
        public string? HttpMethods { get; set; }
    }
}
