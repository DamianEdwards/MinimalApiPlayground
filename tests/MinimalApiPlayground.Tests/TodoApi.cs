using System.Net;
using System.Net.Http.Headers;
using Xunit;

namespace MinimalApiPlayground.Tests
{
    public class TodoApi
    {
        private static readonly string _validTodosJsonFileName = "todos-valid.json";
        private static readonly string _invalidTodosJsonFileName = "todos-invalid.json";

        [Fact]
        public async Task POST_FromFile_Valid_Responds_Created()
        {
            await using var application = new PlaygroundApplication();

            using var formContent = new MultipartFormDataContent();
            using var fileContent = new StreamContent(File.OpenRead(_validTodosJsonFileName));
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
            formContent.Add(fileContent, "todosFile", _validTodosJsonFileName);

            using var client = application.CreateClient();
            using var response = await client.PostAsync("/todos/fromfile", formContent);
            var responseBody = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.NotNull(response.Headers.Location);
            Assert.Matches("My Todo from a file", responseBody);
            Assert.Matches("Another Todo from a file", responseBody);
        }

        [Fact]
        public async Task POST_FromFile_Invalid_Responds_BadRequest()
        {
            await using var application = new PlaygroundApplication();

            using var formContent = new MultipartFormDataContent();
            using var fileContent = new StreamContent(File.OpenRead(_invalidTodosJsonFileName));
            fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse("application/json");
            formContent.Add(fileContent, "todosFile", _invalidTodosJsonFileName);

            using var client = application.CreateClient();
            using var response = await client.PostAsync("/todos/fromfile", formContent);
            var responseBody = await response.Content.ReadAsStringAsync();

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(response.Content.Headers.ContentType, MediaTypeHeaderValue.Parse("application/problem+json"));
            Assert.Matches("\\[1\\].Title", responseBody);
            Assert.Matches("The Title field is required", responseBody);
        }
    }
}
