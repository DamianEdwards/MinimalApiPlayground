using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using MinimalApis.Extensions.Binding;
using MinimalApis.Extensions.Results;
using MiniValidation;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Todos") ?? "Data Source=todos.db;Cache=Shared";

// Customize the JSON serialization options used by minimal with following line
//builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(o => o.SerializerOptions.IncludeFields = true);

builder.Services.AddAntiforgery();
builder.Services.AddSqlite<TodoDb>(connectionString);
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddProblemDetailsDeveloperPageExceptionFilter();
builder.Services.AddParameterBinder<TodoBinder, Todo>();

builder.Services.AddEndpointsProvidesMetadataApiExplorer();

// This enables MVC's model binders
builder.Services.AddMvcCore();

var app = builder.Build();

await EnsureDb(app.Services, app.Logger);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
}

app.UseAntiforgery();

// Error handling
var problemJsonMediaType = new MediaTypeHeaderValue("application/problem+json");
app.MapGet("/error", Results<Problem, StatusCode> (HttpContext context) =>
    {
        var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
        var badRequestEx = error as BadHttpRequestException;
        var statusCode = badRequestEx?.StatusCode ?? StatusCodes.Status500InternalServerError;

        if (context.Request.GetTypedHeaders().Accept?.Any(h => problemJsonMediaType.IsSubsetOf(h)) == true)
        {
            var extensions = new Dictionary<string, object> { { "requestId", Activity.Current?.Id ?? context.TraceIdentifier } };

            // JSON Problem Details
            return error switch
            {
                BadHttpRequestException ex => Results.Extensions.Problem(detail: ex.Message, statusCode: ex.StatusCode, extensions: extensions),
                _ => Results.Extensions.Problem(extensions: extensions)
            };
        }

        // Plain text
        return Results.Extensions.StatusCode(statusCode, badRequestEx?.Message ?? "An unhandled exception occurred while processing the request.");
    })
   .ExcludeFromDescription();

app.MapGet("/throw/{statusCode?}", (int? statusCode) =>
    {
        throw statusCode switch
        {
            >= 400 and < 500 => new BadHttpRequestException(
                $"{statusCode} {ReasonPhrases.GetReasonPhrase(statusCode.Value)}",
                statusCode.Value),
            _ => throw new Exception("uh oh")
        };
    })
   .WithTags("Examples");

// Hello World
app.MapGet("/", () => "Hello World!")
   .WithName("HelloWorld")
   .WithTags("Examples");

app.MapGet("/hello", () => new { Hello = "World" })
   .WithName("HelloWorldApi")
   .WithTags("Examples");

app.MapGet("/goodbye", () => new { Goodbye = "World" })
   .WithTags("Examples");

app.MapGet("/hellofunc", Endpoints.HelloWorldFunc)
   .WithName(nameof(Endpoints.HelloWorldFunc))
   .WithTags("Examples");

// Working with raw JSON
app.MapGet("/jsonraw", () => JsonDocument.Parse("{ \"Id\": 123, \"Name\": \"Example\" }"))
   .WithName("RawJsonOutput")
   .WithTags("Examples");

app.MapPost("/jsonraw", (JsonElement json) => $"Thanks for the JSON:\r\n{json}")
   .WithName("RawJsonInput")
   .WithTags("Examples");

// Example HTML output from custom IResult
app.MapGet("/html", (HttpContext context) => Results.Extensions.Html(
@$"<!doctype html>
<html>
<head><title>miniHTML</title></head>
<body>
<h1>Hello World</h1>
<p>The time on the server is {DateTime.Now:O}</p>
</body>
</html>"))
   .ExcludeFromDescription();

// Example file output from custom IResult
app.MapGet("/htmlfile", (HttpContext context) => Results.Extensions.FromFile("Files\\example.html"))
   .ExcludeFromDescription();

// Example file output
app.MapGet("/getfile", (HttpContext context, IWebHostEnvironment env) =>
    Results.File(env.ContentRootFileProvider.GetFileInfo("Files\\example.html").PhysicalPath, "text/html"))
   .ExcludeFromDescription();

// Parameter optionality
app.MapGet("/optionality/{value?}", (string? value, int? number) =>
    {
        var sb = new StringBuilder();
        if (value is not null)
        {
            sb.AppendLine($"You provided a value for '{nameof(value)}' of '{value}', thanks!");
        }
        else
        {
            sb.AppendLine($"You didn't provide a value for '{nameof(value)}', but that's OK!");
        }
        if (number != null)
        {
            sb.AppendLine($"You provided a value for '{nameof(number)}' of '{number}', thanks!");
        }
        else
        {
            sb.AppendLine($"You didn't provide a value for '{nameof(number)}', but that's OK!");
        }
        return sb.ToString();
    })
    .WithTags("Examples");

// Custom parameter binding via [TargetType].TryParse()
app.MapGet("/point", (Point point) => $"Point: {point}")
    .WithTags("Examples");

// Custom parameter binding via [TargetType].BindAsync()
app.MapGet("/paged", (PagingData paging) =>
    $"ToString: {paging}\r\nToQueryString: {paging.ToQueryString()}")
    .WithTags("Examples");

// Example of a wrapper generic type the can bind its generic argument
app.MapGet("/wrapped/{id}", (Wrapped<int> id) =>
    $"Successfully parsed {id.Value} as Wrapped<int>!")
    .WithTags("Examples");

// Example of bind logic coming from static methods defined on inherited/implemented interface
app.MapPost("/bind-via-interface", (ExampleInput input) =>
    $"Successfully bound {input.StringProperty} as ExampleInput!")
    .WithTags("Examples")
    .Accepts<ExampleInput>("application/json");

// An example extensible binder system that allows for parameter binders to be configured in DI
app.MapPost("/model", (Bind<Todo> model) =>
    {
        Todo? todo = model;
        return Results.Extensions.Ok(todo);
    })
    .WithTags("Examples");

app.MapPost("/model-nobinder", (Bind<NoBinder> model) =>
    {
        NoBinder? value = model;
        return Results.Extensions.Ok(value);
    })
    .WithTags("Examples");

app.MapPost("/suppress-defaults", (SuppressDefaultResponse<Todo?> todo, HttpContext httpContext) =>
    {
        if (todo.Exception != null)
        {
            // There was an exception during binding, handle it however you like
            throw todo.Exception;
        }

        if (todo.StatusCode != 200)
        {
            // The default logic would have auto-responded, do what you like instead
            throw new BadHttpRequestException("Your request was bad and you should feel bad", todo.StatusCode);
        }

        return Results.Extensions.Ok(todo.Value);
    })
    .WithTags("Examples");

app.MapPost("/suppress-binding", async Task<Results<BadRequest, Ok<Todo>, PlainText, UnprocessableEntity>> (SuppressBinding<Todo?> todo, HttpContext httpContext) =>
    {
        try
        {
            // Manually invoke the default binding logic
            var (boundValue, statusCode) = await DefaultBinder<Todo>.GetValueAsync(httpContext);

            if (statusCode != 200)
            {
                // The default binding resulted in a default response, e.g. 400
                // We can respond how we like instead
                return Results.Extensions.BadRequest($"Issue with default binding, status code returned was {statusCode}");
            }

            return boundValue switch
            {
                object => Results.Extensions.Ok(boundValue),
                _ => Results.Extensions.PlainText("Bound value was null")
            };
        }
        catch (Exception ex)
        {
            // Exception occurred during default binding!
            return Results.Extensions.UnprocessableEntity(ex.ToString());
        }
    })
    .WithTags("Examples");

// Using MVC's model binding logic via a generic wrapping shim
app.MapGet("/paged2", (ModelBinder<PagedData> paging) =>
    $"model: {paging.Model}, valid: {paging.ModelState.IsValid}")
    .WithTags("Examples");

// Overriding/mutating response defaults using middleware
app.UseMutateResponse();

app.MapGet("/mutate-test/{id}", (int? id) =>
    {
        // Request this via /mutate-test/foo will return 400 by default
        return $"Id of '{id}' was bound from request successfully!";
    })
    .MutateResponse(404, "The ID specified was not in the correct format. Please don't do that.")
    .WithTags("Examples");

// Todos API
app.MapGet("/todos/sample", () => new[] {
        new Todo { Id = 1, Title = "Do this" },
        new Todo { Id = 2, Title = "Do this too" }
    })
   .WithTags("Examples", "TodoApi");

app.MapGet("/todos", async (TodoDb db) => await db.Todos.ToListAsync())
   .WithName("GetAllTodos")
   .WithTags("TodoApi");

app.MapGet("/todos/incompleted", async (TodoDb db) => await db.Todos.Where(t => !t.IsComplete).ToListAsync())
   .WithName("GetIncompletedTodos")
   .WithTags("TodoApi");

app.MapGet("/todos/completed", async (TodoDb db) => await db.Todos.Where(t => t.IsComplete).ToListAsync())
   .WithName("GetCompletedTodos")
   .WithTags("TodoApi");

app.MapGet("/todos/{id}", async Task<Results<Ok<Todo>, NotFound>> (int id, TodoDb db) =>
    {
        return await db.Todos.FindAsync(id)
            is Todo todo
                ? Results.Extensions.Ok(todo)
                : Results.Extensions.NotFound();
    })
    .WithName("GetTodoById")
    .WithTags("TodoApi");

app.MapPost("/todos", async Task<Results<ValidationProblem, Created<Todo>>> (Todo todo, TodoDb db) =>
    {
        if (!MiniValidator.TryValidate(todo, out var errors))
            return Results.Extensions.ValidationProblem(errors);

        db.Todos.Add(todo);
        await db.SaveChangesAsync();

        return Results.Extensions.Created($"/todo/{todo.Id}", todo);
    })
    .WithName("AddTodo")
    .WithTags("TodoApi");

// Example of a custom DTO base type that could use abstract 
app.MapPost("/todos/dto", Results<ValidationProblem, Created<Todo>> (CreateTodoInput input, TodoDb db) =>
    {
        if (!MiniValidator.TryValidate(input, out var errors))
            return Results.Extensions.ValidationProblem(errors);

        // Process the DTO here
        var newTodo = new Todo { Id = 1, Title = input.Title };

        return Results.Extensions.Created($"/todo/{newTodo.Id}", newTodo);
    })
    .WithName("AddTodoViaDto")
    .WithTags("TodoApi");

// Example of a custom wrapper type that performs validation
app.MapPost("/todos/validated-wrapper", async Task<Results<ValidationProblem, Created<Todo>>> (Validated<Todo> inputTodo, TodoDb db) =>
    {
        var (todo, isValid) = inputTodo;
        if (!isValid || todo == null)
            return Results.Extensions.ValidationProblem(inputTodo.Errors);

        db.Todos.Add(todo);
        await db.SaveChangesAsync();

        return Results.Extensions.Created($"/todo/{todo.Id}", todo);
    })
    .WithName("AddTodo_ValidatedWrapper")
    .WithTags("TodoApi")
    .Accepts<Todo>("application/json");

// Example of adding an endpoint via a local function MethodGroup with attributes to describe it
app.MapPost("/todos-local-func", AddTodoFunc);

// EndpointName set automatically to name of method
[Mvc.ProducesResponseType(typeof(Mvc.ValidationProblemDetails), StatusCodes.Status400BadRequest)]
[Mvc.ProducesResponseType(typeof(Todo), StatusCodes.Status201Created)]
[EndpointName(nameof(AddTodoFunc))]
[Tags("TodoApi")]
async Task<IResult> AddTodoFunc(Todo todo, TodoDb db)
{
    if (!MiniValidator.TryValidate(todo, out var errors))
        return Results.ValidationProblem(errors);

    db.Todos.Add(todo);
    await db.SaveChangesAsync();

    return Results.Created($"/todos/{todo.Id}", todo);
}

// Example of manually supporting more than JSON for input/output
app.MapPost("/todos/xmlorjson", async Task<Results<UnsupportedMediaType, ValidationProblem, CreatedJsonOrXml<Todo>>> (HttpRequest request, TodoDb db) =>
    {
        string contentType = request.Headers.ContentType;

        var todo = contentType switch
        {
            "application/json" => await request.ReadFromJsonAsync<Todo>(),
            "application/xml" => await request.ReadFromXmlAsync<Todo>(request.ContentLength),
            _ => null,
        };

        if (todo is null)
            return Results.Extensions.UnsupportedMediaType();

        if (!MiniValidator.TryValidate(todo, out var errors))
            return Results.Extensions.ValidationProblem(errors);

        db.Todos.Add(todo);
        await db.SaveChangesAsync();

        return Results.Extensions.CreatedJsonOrXml(todo, contentType);
    })
    .WithName("AddTodoXmlOrJson")
    .WithTags("TodoApi")
    .Accepts<Todo>("application/json", "application/xml");

// Example of manually supporting file upload (comment out RequiresAntiforgery() line to allow POST from browser)
app.MapGet("/todos/fromfile", (HttpContext httpContext, IAntiforgery antiforgery) =>
    {
        var tokenSet = antiforgery.GetTokens(httpContext);

        return tokenSet;
    })
    .WithName("AddTodosFromFile_GetAntiXsrfToken")
    .WithTags("TodoApi");

app.MapPost("/todos/fromfile", async Task<Results<ValidationProblem, Created<List<Todo>>>> (JsonFormFile<List<Todo>> todosFile, TodoDb db) =>
    {
        var todos = todosFile.Value;

        if (!(todos?.Count > 0))
            return Results.Extensions.ValidationProblem(new () { { nameof(todosFile), new[] { "The uploaded file contained no todos." } } });

        var todoCount = 0;
        foreach (var todo in todos)
        {
            if (!MiniValidator.TryValidate(todo, out var errors))
                return Results.Extensions.ValidationProblem(errors.ToDictionary(entry => $"[{todoCount}].{entry.Key}", entry => entry.Value));

            db.Todos.Add(todo);
            todoCount++;
        }

        await db.SaveChangesAsync();

        return Results.Extensions.Created(string.Join(';', todos.Select(t => $"/todo/{t.Id}")), todos);
    })
    .WithName("AddTodosFromFile")
    .WithTags("TodoApi");

app.MapPut("/todos/{id}", async Task<Results<ValidationProblem, NoContent, NotFound>> (int id, Todo inputTodo, TodoDb db) =>
    {
        if (!MiniValidator.TryValidate(inputTodo, out var errors))
            return Results.Extensions.ValidationProblem(errors);

        if (await db.Todos.FindAsync(id) is Todo todo)
        {
            todo.Title = inputTodo.Title;
            todo.IsComplete = inputTodo.IsComplete;
            await db.SaveChangesAsync();
            return Results.Extensions.NoContent();
        }
        else
        {
            return Results.Extensions.NotFound();
        }
    })
    .WithName("UpdateTodo")
    .WithTags("TodoApi");

app.MapPut("/todos/{id}/mark-complete", async Task<Results<NoContent, NotFound>> (int id, TodoDb db) =>
    {
        if (await db.Todos.FindAsync(id) is Todo todo)
        {
            todo.IsComplete = true;
            await db.SaveChangesAsync();
            return Results.Extensions.NoContent();
        }
        else
        {
            return Results.Extensions.NotFound();
        }
    })
    .WithName("CompleteTodo")
    .WithTags("TodoApi");

app.MapPut("/todos/{id}/mark-incomplete", async Task<Results<NoContent, NotFound>> (int id, TodoDb db) =>
    {
        if (await db.Todos.FindAsync(id) is Todo todo)
        {
            todo.IsComplete = false;
            await db.SaveChangesAsync();
            return Results.Extensions.NoContent();
        }
        else
        {
            return Results.Extensions.NotFound();
        }
    })
    .WithName("UncompleteTodo")
    .WithTags("TodoApi");

app.MapDelete("/todos/{id}", async Task<Results<Ok<Todo>, NotFound>> (int id, TodoDb db) =>
    {
        if (await db.Todos.FindAsync(id) is Todo todo)
        {
            db.Todos.Remove(todo);
            await db.SaveChangesAsync();
            return Results.Extensions.Ok(todo);
        }

        return Results.Extensions.NotFound();
    })
    .WithName("DeleteTodo")
    .WithTags("TodoApi");

app.MapDelete("/todos/delete-all", async Task<Ok<int>> (TodoDb db) =>
    {
        var rowCount = await db.Database.ExecuteSqlRawAsync("DELETE FROM Todos");

        return Results.Extensions.Ok(rowCount);
    })
    .WithName("DeleteAllTodos")
    .WithTags("TodoApi");

app.Run();

async Task EnsureDb(IServiceProvider services, ILogger logger)
{
    using var db = services.CreateScope().ServiceProvider.GetRequiredService<TodoDb>();
    if (db.Database.IsRelational())
    {
        logger.LogInformation("Ensuring database exists and is up to date at connection string '{connectionString}'", connectionString);
        await db.Database.MigrateAsync();
    }
}

public class Todo
{
    public int Id { get; set; }
    [Required] public string? Title { get; set; }
    public bool IsComplete { get; set; }
}

public class TodoBinder : IParameterBinder<Todo>
{
    public async ValueTask<Todo?> BindAsync(HttpContext context, ParameterInfo parameter)
    {
        if (!context.Request.HasJsonContentType())
        {
            throw new BadHttpRequestException(
                "Request content type was not a recognized JSON content type.",
                StatusCodes.Status415UnsupportedMediaType);
        }

        var todo = await context.Request.ReadFromJsonAsync<Todo>(context.RequestAborted);

        if (todo is not null) todo.Title += $" [Bound from {nameof(TodoBinder)}]";

        return todo;
    }
}

public class NoBinder
{
    public string? Name { get; set; } = $"Default value set by {nameof(NoBinder)}";
}

public class ExampleInput : IInterfaceBinder<ExampleInput>
{
    public string? StringProperty { get; set; }
}

public class TodoDb : DbContext
{
    public TodoDb(DbContextOptions<TodoDb> options)
        : base(options) { }

    public DbSet<Todo> Todos => Set<Todo>();
}

// Make the implicit Program class public so test projects can access it
public partial class Program { }