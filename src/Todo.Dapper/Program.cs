using System.ComponentModel.DataAnnotations;
using Microsoft.Data.Sqlite;
using Dapper;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddScoped<SqliteConnection>(_ => new SqliteConnection("Data Source=todos.db"));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.MapGet("/", () => "Hello World!");

app.MapGet("/todos", async (SqliteConnection db) =>
    await db.QueryAsync<Todo>("SELECT * FROM Todos"));

app.MapGet("/todos/complete", async (SqliteConnection db) =>
    await db.QueryAsync<Todo>("SELECT * FROM Todos WHERE IsComplete = true"));

app.MapGet("/todos/incomplete", async (SqliteConnection db) =>
    await db.QueryAsync<Todo>("SELECT * FROM Todos WHERE IsComplete = false"));

app.MapGet("/todos/{id}", async (int id, SqliteConnection db) =>
    await db.QuerySingleOrDefaultAsync<Todo>("SELECT * FROM Todos WHERE Id = @id", new { id }) is Todo todo
        ? Results.Ok(todo)
        : Results.NotFound());

app.MapPost("/todos", async (Todo todo, SqliteConnection db) =>
{
    if (!MinimalValidation.TryValidate(todo, out var errors))
        return Results.BadRequest(errors);

    var newTodo = await db.QuerySingleAsync<Todo>("INSERT INTO Todos(Title, IsComplete) Values(@Title, @IsComplete) RETURNING *", todo);

    return Results.Created($"/todos/{newTodo.Id}", newTodo);
});

app.MapPut("/todos/{id}", async (int id, Todo inputTodo, SqliteConnection db) =>
{
    if (!MinimalValidation.TryValidate(inputTodo, out var errors))
        return Results.BadRequest(errors);

    inputTodo.Id = id;
    var rowCount = await db.ExecuteAsync("UPDATE Todos SET Title = @Title, IsComplete = @IsComplete WHERE Id = @Id", inputTodo);

    if (rowCount == 0) return
        Results.NotFound();

    return Results.NoContent();
});

app.MapDelete("/todos/{id}", async (int id, SqliteConnection db) =>
{
    var rowCount = await db.ExecuteAsync("DELETE FROM Todos WHERE Id = @id", new { id });

    if (rowCount == 0) return
        Results.NotFound();

    return Results.Ok();
});

app.MapDelete("/todos/delete-all", async (SqliteConnection db) =>
{
    var rowCount = await db.ExecuteAsync("DELETE FROM Todos");

    if (rowCount == 0) return
        Results.NotFound();

    return Results.Ok();
});

app.Run();

class Todo
{
    public int Id { get; set; }
    [Required]
    public string? Title { get; set; }
    public bool IsComplete { get; set; }
}