using static ResultHelpers;

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

app.MapGet("/", () => new { Hello = "World" });
app.MapGet("/throw", () => { throw new Exception("uh oh"); });
app.MapGet("/error", () => "An error occurred. This should probably be formatted as Problem Details.");

app.MapGet("/todos/sample", () => new[] {
    new Todo("Do this") { Id = 1 },
    new Todo("Do this too") { Id = 2 }
});

app.MapGet("/todos", async (TodoDb db) => await db.Todos.ToListAsync());

app.MapGet("/todos/{id}", async (int id, TodoDb db) =>
{
    return await db.Todos.FindAsync(id) is Todo todo
        ? Ok(todo) : NotFound();
});

app.MapPost("/todos", async (Todo todo, TodoDb db) =>
{
    if (!todo.TryValidate(out ValidationErrors errors)) return BadRequest(errors);

    db.Todos.Add(todo);
    await db.SaveChangesAsync();

    return CreatedAt($"/todos/{todo.Id}", todo);
});

app.MapPut("/todos/{id}", async (int id, Todo inputTodo, TodoDb db) =>
{
    if (!inputTodo.TryValidate(out ValidationErrors errors)) return BadRequest(errors);

    var todo = await db.Todos.FindAsync(id);

    if (todo is null) return NotFound();

    todo.Title = inputTodo.Title;
    todo.IsComplete = inputTodo.IsComplete;

    await db.SaveChangesAsync();

    return NoContent();
});

app.MapDelete("/todos/{id}", async (int id, TodoDb db) =>
{
    if (await db.Todos.FindAsync(id) is Todo todo)
    {
        db.Todos.Remove(todo);
        await db.SaveChangesAsync();
        return Ok(todo);
    }

    return NotFound();
});

app.Run();

class Todo
{
    public Todo(string title)
    {
        Title = title;
    }

    public int Id { get; set; }
    [Required] public string Title { get; set; }
    public bool IsComplete { get; set; }
};

class TodoDb : DbContext
{
    public TodoDb(DbContextOptions<TodoDb> options)
        : base(options) { }

    public DbSet<Todo> Todos { get; set; }
}
