using System.ComponentModel.DataAnnotations;
using Microsoft.Data.Sqlite;
using Dapper;

var builder = WebApplication.CreateBuilder(args);

var connectionString = "Data Source=todos.db";
builder.Services.AddScoped<SqliteConnection>(_ => new SqliteConnection(connectionString));

var app = builder.Build();

await EnsureDb(connectionString);

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.MapGet("/", () => "Hello World!");
app.MapGet("/hello", () => new { Hello = "World" });

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
        return Results.ValidationProblem(errors);

    var newTodo = await db.QuerySingleAsync<Todo>(
        "INSERT INTO Todos(Title, IsComplete) Values(@Title, @IsComplete) RETURNING * ", todo);

    return Results.Created($"/todos/{newTodo.Id}", newTodo);
});

app.MapPut("/todos/{id}", async (int id, Todo todo, SqliteConnection db) =>
{
    todo.Id = id;
    if (!MinimalValidation.TryValidate(todo, out var errors))
        return Results.ValidationProblem(errors);

    return await db.ExecuteAsync("UPDATE Todos SET Title = @Title, IsComplete = @IsComplete WHERE Id = @Id", todo) == 1
        ? Results.NoContent()
        : Results.NotFound();
});

app.MapPut("/todos/{id}/mark-complete", async (int id, SqliteConnection db) =>
    await db.ExecuteAsync("UPDATE Todos SET IsComplete = true WHERE Id = @Id", new { id }) == 1
        ? Results.NoContent()
        : Results.NotFound());

app.MapPut("/todos/{id}/mark-incomplete", async (int id, SqliteConnection db) =>
    await db.ExecuteAsync("UPDATE Todos SET IsComplete = false WHERE Id = @Id", new { id }) == 1
        ? Results.NoContent()
        : Results.NotFound());

app.MapDelete("/todos/{id}", async (int id, SqliteConnection db) =>
    await db.ExecuteAsync("DELETE FROM Todos WHERE Id = @id", new { id }) == 1
        ? Results.NoContent()
        : Results.NotFound());

app.MapDelete("/todos/delete-all", async (SqliteConnection db) =>
    Results.Ok(await db.ExecuteAsync("DELETE FROM Todos")));

app.Run();

Task EnsureDb(string connectionString)
{
    using var db = new SqliteConnection(connectionString);
    var sql = $@"CREATE TABLE IF NOT EXISTS Todos (
                  {nameof(Todo.Id)} INTEGER PRIMARY KEY AUTOINCREMENT,
                  {nameof(Todo.Title)} TEXT NOT NULL,
                  {nameof(Todo.IsComplete)} INTEGER DEFAULT 0 NOT NULL CHECK({nameof(Todo.IsComplete)} IN (0, 1))
                 );";
    return db.ExecuteAsync(sql);
}

class Todo
{
    public int Id { get; set; }
    [Required]
    public string? Title { get; set; }
    public bool IsComplete { get; set; }
}
