using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
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
    [InlineData(400, "Development")]
    [InlineData(404, "Development")]
    [InlineData(405, "Development")]
    [InlineData(414, "Development")]
    [InlineData(415, "Development")]
    [InlineData(422, "Development")]
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
        Assert.Null(problemDetails?.Exception?.Error);
        Assert.Null(problemDetails?.Exception?.Path);
        Assert.Null(problemDetails?.Exception?.Endpoint);
        Assert.Null(problemDetails?.Exception?.RouteValues);
        Assert.Null(problemDetails?.Exception?.Headers);
    }

    [Fact]
    public async Task GET_Throw_Responds_With_ProblemDetails_In_Development()
    {
        await using var application = new PlaygroundApplication("Development");

        var problemDetails = await GET_Throw_Responds_With_ProblemDetails(application);

        // Development only details
        Assert.NotNull(problemDetails?.Exception);
        Assert.NotNull(problemDetails?.Exception?.Error);
        Assert.NotNull(problemDetails?.Exception?.Path);
        Assert.NotNull(problemDetails?.Exception?.Endpoint);
        Assert.NotNull(problemDetails?.Exception?.RouteValues);
        Assert.NotNull(problemDetails?.Exception?.Headers);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task GET_Throw_Responds_With_ProblemDetails_When_Client_Accepts_Json(string environment)
    {
        await using var application = new PlaygroundApplication(environment);

        var problemDetails = await GET_Throw_Responds_With_ProblemDetails(application);

        Assert.NotNull(problemDetails);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task GET_Throw_Responds_With_ProblemDetails_When_Client_Accepts_Star(string environment)
    {
        await using var application = new PlaygroundApplication(environment);

        var problemDetails = await GET_Throw_Responds_With_ProblemDetails(application, "*/*");

        Assert.NotNull(problemDetails);
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public async Task GET_Throw_Responds_With_PlainText_When_Client_Does_Not_Accept_Json(string environment)
    {
        await using var application = new PlaygroundApplication(environment);

        using var client = application.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/throw");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));
        using var response = await client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(MediaTypeWithQualityHeaderValue.Parse("text/plain; charset=utf-8"), response.Content.Headers.ContentType);
    }

    private async Task<ProblemDetails?> GET_Throw_Responds_With_ProblemDetails(PlaygroundApplication application, string? accept = null)
    {
        using var client = application.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/throw");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept ?? "application/json"));
        using var response = await client.SendAsync(request);
        var contentType = response.Content.Headers.ContentType;
        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(MediaTypeHeaderValue.Parse("application/problem+json"), contentType);
        Assert.Equal((int)HttpStatusCode.InternalServerError, problemDetails?.Status);
        Assert.NotNull(problemDetails?.Title);
        Assert.NotNull(problemDetails?.RequestId);

        return problemDetails;
    }

    public class ProblemDetails
    {
        public string? Type { get; set; }
        public string? Title {  get; set; }
        public int Status { get; set; }
        public string? Detail { get; set; }
        public string? RequestId { get; set; }
        public ExceptionDetails? Exception { get; set; }

        public class ExceptionDetails
        {
            public string? Error { get; set; }
            public PathString Path { get; set; }
            public string? Endpoint { get; set; }
            public IDictionary<string, IEnumerable<string>>? Headers { get; set; }
            public IDictionary<string, object>? RouteValues { get; set; }
            public IEnumerable<string>? Query { get; set; }
        }
    }
}
