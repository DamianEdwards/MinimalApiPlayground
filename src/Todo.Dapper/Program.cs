using System.ComponentModel.DataAnnotations;
using Microsoft.Data.Sqlite;
using Dapper;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("TodoDb") ?? "Data Source=todos.db;Cache=Shared";
builder.Services.AddScoped(_ => new SqliteConnection(connectionString));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

await EnsureDb(app.Services, app.Logger);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
}

app.MapGet("/error", () => Results.Problem("An error occurred.", statusCode: 500))
   .ExcludeFromDescription();

app.MapSwagger();
app.UseSwaggerUI();

app.MapGet("/", () => "Hello World!")
   .WithName("Hello");

app.MapGet("/hello", () => new { Hello = "World" })
   .WithName("HelloObject");

app.MapGet("/todos", async (SqliteConnection db) =>
    await db.QueryAsync<Todo>("SELECT * FROM Todos"))
   .WithName("GetAllTodos");

app.MapGet("/todos/complete", async (SqliteConnection db) =>
    await db.QueryAsync<Todo>("SELECT * FROM Todos WHERE IsComplete = true"))
   .WithName("GetCompleteTodos");

app.MapGet("/todos/incomplete", async (SqliteConnection db) =>
    await db.QueryAsync<Todo>("SELECT * FROM Todos WHERE IsComplete = false"))
   .WithName("GetIncompleteTodos");

app.MapGet("/todos/{id}", async (int id, SqliteConnection db) =>
    await db.QuerySingleOrDefaultAsync<Todo>("SELECT * FROM Todos WHERE Id = @id", new { id })
        is Todo todo
            ? Results.Ok(todo)
            : Results.NotFound())
    .WithName("GetTodoById")
    .Produces<Todo>(StatusCodes.Status200OK)
    .Produces(StatusCodes.Status404NotFound);

app.MapPost("/todos", async (Todo todo, SqliteConnection db) =>
    {
        if (!MinimalValidation.TryValidate(todo, out var errors))
            return Results.ValidationProblem(errors);

        var newTodo = await db.QuerySingleAsync<Todo>(
            "INSERT INTO Todos(Title, IsComplete) Values(@Title, @IsComplete) RETURNING * ", todo);

        return Results.Created($"/todos/{newTodo.Id}", newTodo);
    })
    .WithName("CreateTodo")
    .ProducesValidationProblem()
    .Produces<Todo>(StatusCodes.Status201Created);

app.MapPut("/todos/{id}", async (int id, Todo todo, SqliteConnection db) =>
    {
        todo.Id = id;
        if (!MinimalValidation.TryValidate(todo, out var errors))
            return Results.ValidationProblem(errors);

        return await db.ExecuteAsync("UPDATE Todos SET Title = @Title, IsComplete = @IsComplete WHERE Id = @Id", todo) == 1
            ? Results.NoContent()
            : Results.NotFound();
    })
    .WithName("UpdateTodo")
    .ProducesValidationProblem()
    .Produces(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status404NotFound);

app.MapPut("/todos/{id}/mark-complete", async (int id, SqliteConnection db) =>
    await db.ExecuteAsync("UPDATE Todos SET IsComplete = true WHERE Id = @Id", new { id }) == 1
        ? Results.NoContent()
        : Results.NotFound())
    .WithName("MarkComplete")
    .Produces(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status404NotFound);

app.MapPut("/todos/{id}/mark-incomplete", async (int id, SqliteConnection db) =>
    await db.ExecuteAsync("UPDATE Todos SET IsComplete = false WHERE Id = @Id", new { id }) == 1
        ? Results.NoContent()
        : Results.NotFound())
    .WithName("MarkIncomplete")
    .Produces(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status404NotFound);

app.MapDelete("/todos/{id}", async (int id, SqliteConnection db) =>
    await db.ExecuteAsync("DELETE FROM Todos WHERE Id = @id", new { id }) == 1
        ? Results.NoContent()
        : Results.NotFound())
    .WithName("DeleteTodo")
    .Produces(StatusCodes.Status204NoContent)
    .Produces(StatusCodes.Status404NotFound);

app.MapDelete("/todos/delete-all", async (SqliteConnection db) => Results.Ok(await db.ExecuteAsync("DELETE FROM Todos")))
    .WithName("DeleteAll")
    .Produces<int>(StatusCodes.Status200OK);

app.Run();

async Task EnsureDb(IServiceProvider services, ILogger logger)
{
    logger.LogInformation("Ensuring database exists at connection string '{connectionString}'", connectionString);

    using var db = services.CreateScope().ServiceProvider.GetRequiredService<SqliteConnection>();
    var sql = $@"CREATE TABLE IF NOT EXISTS Todos (
                  {nameof(Todo.Id)} INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL,
                  {nameof(Todo.Title)} TEXT NOT NULL,
                  {nameof(Todo.IsComplete)} INTEGER DEFAULT 0 NOT NULL CHECK({nameof(Todo.IsComplete)} IN (0, 1))
                 );";
    await db.ExecuteAsync(sql);
}

public class Todo
{
    public int Id { get; set; }
    [Required]
    public string? Title { get; set; }
    public bool IsComplete { get; set; }
}
