using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using MinimalApiPlayground.ModelBinding;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Todos") ?? "Data Source=todos.db;Cache=Shared";

// Customize the JSON serialization options used by minimal with following line
//builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(o => o.SerializerOptions.IncludeFields = true);

builder.Services.AddAntiforgery();
builder.Services.AddSqlite<TodoDb>(connectionString);
builder.Services.AddDatabaseDeveloperPageExceptionFilter();
builder.Services.AddProblemDetailsDeveloperPageExceptionFilter();
builder.Services.AddParameterBinder<TodoBinder, Todo>();

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
app.MapGet("/error", (HttpContext context) =>
    context.Features.Get<IExceptionHandlerFeature>()?.Error switch
    {
        BadHttpRequestException ex => Results.Problem(ex.Message, statusCode: ex.StatusCode),
        _ => Results.Problem("Internal server error")
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
   .WithName("HelloWorldApi")
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

app.MapGet("/parse/{id}", (Parseable<int> id) =>
    $"Successfully parsed {id.Value} as Parseable<int>!")
    .WithTags("Examples");

// Custom parameter binding via [TargetType].BindAsync()
app.MapGet("/paged", (PagingData paging) =>
    $"ToString: {paging}\r\nToQueryString: {paging.ToQueryString()}")
    .WithTags("Examples");

app.MapGet("/wrapped/{id}", (Wrapped<int> id) =>
    $"Successfully parsed {id.Value} as Wrapped<int>!")
    .WithTags("Examples");

app.MapPost("/model", (Model<Todo> model) =>
    {
        Todo? todo = model;
        return Results.Ok(todo);
    })
    .WithTags("Examples")
    .Accepts<Todo>("application/json");

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
   .WithTags("TodoApi")
   .Produces<List<Todo>>();

app.MapGet("/todos/incompleted", async (TodoDb db) => await db.Todos.Where(t => !t.IsComplete).ToListAsync())
   .WithName("GetIncompletedTodos")
   .WithTags("TodoApi")
   .Produces<List<Todo>>();

app.MapGet("/todos/completed", async (TodoDb db) => await db.Todos.Where(t => t.IsComplete).ToListAsync())
   .WithName("GetCompletedTodos")
   .WithTags("TodoApi")
   .Produces<List<Todo>>();

app.MapGet("/todos/{id}", async (int id, TodoDb db) =>
    {
        return await db.Todos.FindAsync(id)
            is Todo todo
                ? Results.Ok(todo)
                : Results.NotFound();
    })
    .WithName("GetTodoById")
    .WithTags("TodoApi")
    .Produces<Todo>()
    .Produces(StatusCodes.Status404NotFound);

app.MapPost("/todos", async (Todo todo, TodoDb db) =>
    {
        if (!MiniValidation.TryValidate(todo, out var errors))
            return Results.ValidationProblem(errors);

        db.Todos.Add(todo);
        await db.SaveChangesAsync();

        return Results.Created($"/todo/{todo.Id}", todo);
    })
    .WithName("AddTodo")
    .WithTags("TodoApi")
    .ProducesValidationProblem()
    .Produces<Todo>(StatusCodes.Status201Created);

// Example of a custom wrapper type that performs validation
app.MapPost("/todos/validated-wrapper", async (Validated<Todo> inputTodo, TodoDb db) =>
    {
        var (todo, isValid) = inputTodo;
        if (!isValid)
            return Results.ValidationProblem(inputTodo.Errors);

        db.Todos.Add(todo);
        await db.SaveChangesAsync();

        return Results.Created($"/todo/{todo.Id}", todo);
    })
    .WithName("AddTodo_ValidatedWrapper")
    .WithTags("TodoApi")
    .Accepts<Todo>("application/json")
    .ProducesValidationProblem()
    .Produces<Todo>(StatusCodes.Status201Created);

// Example of adding an endpoint via a local function MethodGroup with attributes to describe it
app.MapPost("/todos-local-func", AddTodoFunc);

// EndpointName set automatically to name of method
[ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(Todo), StatusCodes.Status201Created)]
[EndpointName(nameof(AddTodoFunc))]
[Tags("TodoApi")]
async Task<IResult> AddTodoFunc(Todo todo, TodoDb db)
{
    if (!MiniValidation.TryValidate(todo, out var errors))
        return Results.ValidationProblem(errors);

    db.Todos.Add(todo);
    await db.SaveChangesAsync();

    return Results.Created($"/todos/{todo.Id}", todo);
}

// Example of manually supporting more than JSON for input/output
app.MapPost("/todos/xmlorjson", async (HttpRequest request, TodoDb db) =>
    {
        string contentType = request.Headers.ContentType;

        var todo = contentType switch
        {
            "application/json" => await request.Body.ReadAsJsonAsync<Todo>(),
            "application/xml" => await request.Body.ReadAsXmlAsync<Todo>(request.ContentLength),
            _ => null,
        };

        if (todo is null)
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);

        if (!MiniValidation.TryValidate(todo, out var errors))
            return Results.ValidationProblem(errors);

        db.Todos.Add(todo);
        await db.SaveChangesAsync();

        return Results.Extensions.CreatedWithContentType(todo, contentType);
    })
    .WithName("AddTodoXmlOrJson")
    .WithTags("TodoApi")
    .Accepts<Todo>("application/json", "application/xml")
    .Produces(StatusCodes.Status415UnsupportedMediaType)
    .ProducesValidationProblem()
    .Produces<Todo>(StatusCodes.Status201Created, "application/json", "application/xml");

// Example of manually supporting file upload (comment out RequiresAntiforgery() line to allow POST from browser)
app.MapGet("/todos/fromfile", (HttpContext httpContext, IAntiforgery antiforgery) =>
    {
        var tokenSet = antiforgery.GetTokens(httpContext);

        return tokenSet;
    })
    .WithName("AddTodosFromFile_GetAntiXsrfToken")
    .WithTags("TodoApi")
    .Produces<AntiforgeryTokenSet>();

app.MapPost("/todos/fromfile", async (JsonFormFile<List<Todo>> todosFile, TodoDb db) =>
    {
        var todos = todosFile.Value;

        if (!(todos?.Count > 0))
            return Results.BadRequest();

        var todoCount = 0;
        foreach (var todo in todos)
        {
            if (!MiniValidation.TryValidate(todo, out var errors))
                return Results.ValidationProblem(errors.ToDictionary(entry => $"[{todoCount}].{entry.Key}", entry => entry.Value));

            db.Todos.Add(todo);
            todoCount++;
        }

        await db.SaveChangesAsync();

        return Results.Created(string.Join(';', todos.Select(t => $"/todo/{t.Id}")), todos);
    })
    .WithName("AddTodosFromFile")
    .WithTags("TodoApi")
    .AcceptsFormFile("todosFile")
    //.RequiresAntiforgery()
    .Produces(StatusCodes.Status400BadRequest)
    .Produces(StatusCodes.Status415UnsupportedMediaType)
    .ProducesValidationProblem()
    .Produces<List<Todo>>(StatusCodes.Status201Created);

app.MapPut("/todos/{id}", async (int id, Todo inputTodo, TodoDb db) =>
    {
        if (!MiniValidation.TryValidate(inputTodo, out var errors))
            return Results.ValidationProblem(errors);

        if (await db.Todos.FindAsync(id) is Todo todo)
        {
            todo.Title = inputTodo.Title;
            todo.IsComplete = inputTodo.IsComplete;
            await db.SaveChangesAsync();
            return Results.NoContent();
        }
        else
        {
            return Results.NotFound();
        }
    })
    .WithName("UpdateTodo")
    .WithTags("TodoApi")
    .ProducesValidationProblem()
    .Produces(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status404NotFound);

app.MapPut("/todos/{id}/mark-complete", async (int id, TodoDb db) =>
    {
        if (await db.Todos.FindAsync(id) is Todo todo)
        {
            todo.IsComplete = true;
            await db.SaveChangesAsync();
            return Results.NoContent();
        }
        else
        {
            return Results.NotFound();
        }
    })
    .WithName("CompleteTodo")
    .WithTags("TodoApi")
    .Produces(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status404NotFound);

app.MapPut("/todos/{id}/mark-incomplete", async (int id, TodoDb db) =>
    {
        if (await db.Todos.FindAsync(id) is Todo todo)
        {
            todo.IsComplete = false;
            await db.SaveChangesAsync();
            return Results.NoContent();
        }
        else
        {
            return Results.NotFound();
        }
    })
    .WithName("UncompleteTodo")
    .WithTags("TodoApi")
    .Produces(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status404NotFound);

app.MapDelete("/todos/{id}", async (int id, TodoDb db) =>
    {
        if (await db.Todos.FindAsync(id) is Todo todo)
        {
            db.Todos.Remove(todo);
            await db.SaveChangesAsync();
            return Results.Ok(todo);
        }

        return Results.NotFound();
    })
    .WithName("DeleteTodo")
    .WithTags("TodoApi")
    .Produces<Todo>()
    .Produces(StatusCodes.Status404NotFound);

app.MapDelete("/todos/delete-all", async (TodoDb db) =>
    {
        var rowCount = await db.Database.ExecuteSqlRawAsync("DELETE FROM Todos");

        return Results.Ok(rowCount);
    })
    .WithName("DeleteAllTodos")
    .WithTags("TodoApi")
    .Produces<int>();

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

class TodoDb : DbContext
{
    public TodoDb(DbContextOptions<TodoDb> options)
        : base(options) { }

    public DbSet<Todo> Todos => Set<Todo>();
}
