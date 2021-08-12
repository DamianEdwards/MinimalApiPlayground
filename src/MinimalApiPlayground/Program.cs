using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Todos") ?? "Data Source=todos.db";

builder.Services.AddSqlite<TodoDb>(connectionString)
                .AddDatabaseDeveloperPageExceptionFilter();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseRouting();
}

app.MapGet("/throw", () => { throw new Exception("uh oh"); })
   .ExcludeFromDescription();

app.MapGet("/error", () => Results.Problem("An error occurred.", statusCode: 500))
   .ExcludeFromDescription();

app.MapGet("/", () => "Hello World!")
   .WithName("HelloWorldApi")
   .WithTags("Examples");

app.MapGet("/hello", () => new { Hello = "World" })
   .WithTags("Examples");

app.MapGet("/goodbye", () => new { Goodbye = "World" })
   .WithTags("Examples");

app.MapGet("/html", (HttpContext context) => AppResults.Html(
@$"<!doctype html>
<html>
<head><title>miniHTML</title></head>
<body>
<h1>Hello World</h1>
<p>The time on the server is {DateTime.Now.ToString("O")}</p>
</body>
</html>"))
   .ExcludeFromDescription();

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
        if (!MinimalValidation.TryValidate(todo, out var errors))
            return Results.ValidationProblem(errors);

        db.Todos.Add(todo);
        await db.SaveChangesAsync();

        return Results.Created($"/todo/{todo.Id}", todo); ;
    })
    .WithName("AddTodo")
    .WithTags("TodoApi")
    .ProducesValidationProblem()
    .Produces<Todo>(StatusCodes.Status201Created);

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
        {
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);
        }

        if (!MinimalValidation.TryValidate(todo, out var errors))
            return Results.ValidationProblem(errors);

        db.Todos.Add(todo);
        await db.SaveChangesAsync();

        return AppResults.Created(todo, contentType);
    })
    .WithName("AddTodoXmlOrJson")
    .WithTags("TodoApi")
    .Accepts<Todo>("application/json", "application/xml")
    .Produces(StatusCodes.Status415UnsupportedMediaType)
    .ProducesValidationProblem()
    .Produces<Todo>(StatusCodes.Status201Created, "application/json", "application/xml");

// Example of manually supporting file upload
app.MapPost("/todos/fromfile", async (HttpRequest request, TodoDb db) =>
    {
        if (!request.HasFormContentType)
            return Results.BadRequest();

        var form = await request.ReadFormAsync();
        if (form.Files.Count != 1)
        {
            return Results.BadRequest();
        }

        var file = form.Files[0];
        if (file.ContentType != "application/json")
            return Results.StatusCode(StatusCodes.Status415UnsupportedMediaType);

        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var todo = await JsonSerializer.DeserializeAsync<Todo>(file.OpenReadStream(), jsonOptions);

        if (todo is not Todo)
            return Results.BadRequest();

        if (!MinimalValidation.TryValidate(todo, out var errors))
            return Results.ValidationProblem(errors);

        db.Todos.Add(todo);
        await db.SaveChangesAsync();

        return Results.Created($"/todo/{todo.Id}", todo);
    })
    .WithName("AddTodoFromFile")
    .WithTags("TodoApi")
    .AcceptsFormFile("todofile")
    .ProducesValidationProblem()
    .Produces<Todo>(StatusCodes.Status201Created);

// Example of adding an endpoint via a MethodGroup with attributes to describe it
app.MapPost("/todos-local-func", AddTodoFunc);

// EndpointName set automatically to name of method (waiting on PR https://github.com/dotnet/aspnetcore/pull/35069)
[ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
[ProducesResponseType(typeof(Todo), StatusCodes.Status201Created)]
[TagsAttribute("TodoApi")]
async Task<IResult> AddTodoFunc(Todo todo, TodoDb db)
{
    if (!MinimalValidation.TryValidate(todo, out var errors))
        return Results.ValidationProblem(errors);

    db.Todos.Add(todo);
    await db.SaveChangesAsync();

    return Results.Created($"/todos/{todo.Id}", todo);
}

app.MapPut("/todos/{id}", async (int id, Todo inputTodo, TodoDb db) =>
    {
        if (!MinimalValidation.TryValidate(inputTodo, out var errors))
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

public class Todo
{
    public int Id { get; set; }
    [Required] public string? Title { get; set; }
    public bool IsComplete { get; set; }
}

class TodoDb : DbContext
{
    public TodoDb(DbContextOptions<TodoDb> options)
        : base(options) { }

    public DbSet<Todo> Todos => Set<Todo>();
}
