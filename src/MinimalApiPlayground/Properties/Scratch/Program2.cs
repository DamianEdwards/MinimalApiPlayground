using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using MiniValidation;

static class Program2
{
    public static void Main2(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        var connectionString = builder.Configuration.GetConnectionString("Todos") ?? "Data Source=todos.db";

        builder.Services.AddSqlite<TodoDb>(connectionString);
        builder.Services.AddDatabaseDeveloperPageExceptionFilter();

        var app = builder.Build();

        // Using route patterns defined on an anonymous object with supporting result methods
        var routes = new
        {
            Todos = "/todos",
            GetTodoById = "/todos/{id}",
        };

        app.MapGet(routes.GetTodoById, async (int id, TodoDb db) =>
            {
                return await db.Todos.FindAsync(id) is Todo todo
                    ? Results.Ok(todo) : Results.NotFound();
            })
            .WithName(nameof(routes.GetTodoById));

        app.MapPost(routes.Todos, async (Todo todo, TodoDb db) =>
        {
            if (!MiniValidator.TryValidate(todo, out var errors)) return Results.BadRequest(errors);

            db.Todos.Add(todo);
            await db.SaveChangesAsync();

            return Results.CreatedAtRoute(nameof(routes.GetTodoById), new { id = todo.Id }, todo);
        });

        app.MapPost("/todolist", (TodoList list) =>
        {
            if (!MiniValidator.TryValidate(list, out var errors))
                return Results.ValidationProblem(errors);

            return Results.Ok();
        })
        .WithName("AddTodoList")
        .Produces(StatusCodes.Status200OK)
        .Produces<HttpValidationProblemDetails>();

        app.MapPost("/todocycle", (TodoList list) =>
        {
            if (!MiniValidator.TryValidate(list, out var errors))
                return Results.ValidationProblem(errors);

            return Results.Ok();
        })
        .WithName("AddTodoList")
        .Produces(StatusCodes.Status200OK)
        .Produces<HttpValidationProblemDetails>();
    }

    class TodoList
    {
        [Required] public string? Title { get; set; }
        public ICollection<Todo>? Todos { get; set; }
    }
}