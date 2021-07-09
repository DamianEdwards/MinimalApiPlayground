using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Todos") ?? "Data Source=todos.db";

builder.Services.AddSqlite<TodoDb>(connectionString);
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

var app = builder.Build();

app.UseExceptionHandler("/error");
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.MapGet("/", () => "Hello World");
app.MapGet("/hello", () => new { Hello = "World" });

app.MapGet("/html", (HttpContext context) => AppResults.Html(
@$"<!doctype html>
<html>
<head><title>miniHTML</title></head>
<body>
<h1>Hello World</h1>
<p>The time on the server is {DateTime.Now.ToString("O")}</p>
</body>
</html>"));

app.MapGet("/throw", () => { throw new Exception("uh oh"); });

app.MapGet("/error", () => "An error occurred. This should probably be formatted as Problem Details.");

app.MapGet("/todos/sample", () => new[] {
    new Todo { Id = 1, Title = "Do this" },
    new Todo { Id = 2, Title = "Do this too" }
});

app.MapGet("/todos", async (TodoDb db) => await db.Todos.ToListAsync());

app.MapGet("/todos/incomplete", async (TodoDb db) => await db.Todos.Where(t => !t.IsComplete).ToListAsync());

app.MapGet("/todos/complete", async (TodoDb db) => await db.Todos.Where(t => t.IsComplete).ToListAsync());

app.MapGet("/todos/{id}", async (int id, TodoDb db) =>
{
    return await db.Todos.FindAsync(id) is Todo todo
        ? Results.Ok(todo) : Results.NotFound();
});

app.MapPost("/todos", async (Todo todo, TodoDb db) =>
{
    if (!MinimalValidation.TryValidate(todo, out var errors)) return Results.BadRequest(errors);

    db.Todos.Add(todo);
    await db.SaveChangesAsync();

    return Results.Created($"/todos/{todo.Id}", todo);
});

app.MapPut("/todos/{id}", async (int id, Todo inputTodo, TodoDb db) =>
{
    if (!MinimalValidation.TryValidate(inputTodo, out var errors)) return Results.BadRequest(errors);

    var todo = await db.Todos.FindAsync(id);

    if (todo is null) return Results.NotFound();

    todo.Title = inputTodo.Title;
    todo.IsComplete = inputTodo.IsComplete;

    await db.SaveChangesAsync();

    return Results.NoContent();
});

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
});

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
});

app.MapDelete("/todos/{id}", async (int id, TodoDb db) =>
{
    if (await db.Todos.FindAsync(id) is Todo todo)
    {
        db.Todos.Remove(todo);
        await db.SaveChangesAsync();
        return Results.Ok(todo);
    }

    return Results.NotFound();
});

app.MapDelete("/todos/delete-all", async (TodoDb db) =>
{
    var rowCount = await db.Database.ExecuteSqlRawAsync("DELETE FROM Todos");

    return Results.Ok(rowCount);
});

app.MapPost("/todolist", (TodoList list) =>
{
    if (!MinimalValidation.TryValidate(list, out var errors)) return Results.BadRequest(errors);

    return Results.Ok();
});

app.MapPost("/todocycle", (TodoList list) =>
{
    if (!MinimalValidation.TryValidate(list, out var errors)) return Results.BadRequest(errors);

    return Results.Ok();
});

app.Run();

class Todo
{
    public int Id { get; set; }
    [Required] public string? Title { get; set; }
    public bool IsComplete { get; set; }
}

class TodoList
{
    [Required] public string? Title { get; set; }
    public ICollection<Todo>? Todos { get; set; }
}

class TodoDb : DbContext
{
    public TodoDb(DbContextOptions<TodoDb> options)
        : base(options) { }

    public DbSet<Todo> Todos => Set<Todo>();
}
