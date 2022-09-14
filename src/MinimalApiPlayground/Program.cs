using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using MinimalApis.Extensions.Binding;
using MinimalApis.Extensions.Results;
using MiniValidation;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("Todos") ?? "Data Source=todos.db;Cache=Shared";

// Customize the JSON serialization options used by minimal with following line
//builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(o => o.SerializerOptions.IncludeFields = true);

// Add database services
builder.Services.AddSqlite<TodoDb>(connectionString);
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Add services required to support using MVC's model binders
builder.Services.AddMvcCore();

// Enable & configure JSON Problem Details error responses
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
        context.ProblemDetails.Extensions["requestId"] = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;
});

// Add services for AuthN/AuthZ, this will also auto-add the required middleware in .NET 7
// Use the command line tool `dotnet user-jwts` to manage development-time JWTs for this app
builder.Services.AddAuthentication().AddJwtBearer();
builder.Services.AddAuthorizationBuilder();

// Add Anti-CSRF/XSRF services
builder.Services.AddAntiforgery();

// Add a custom parameter binder (from MinimalApis.Extensions)
builder.Services.AddParameterBinder<TodoBinder, Todo>();

var app = builder.Build();

await EnsureDb(app.Services, app.Logger);

if (!app.Environment.IsDevelopment())
{
    // Error handling
    app.UseExceptionHandler(new ExceptionHandlerOptions
    {
        AllowStatusCode404Response = true,
        ExceptionHandler = async (HttpContext context) =>
        {
            // The default exception handler always responds with status code 500 so we're overriding here to
            // allow pass-through status codes from BadHttpRequestException.
            // GitHub issue to support this in framework: https://github.com/dotnet/aspnetcore/issues/43831
            var exceptionHandlerFeature = context.Features.Get<IExceptionHandlerFeature>();

            if (exceptionHandlerFeature?.Error is BadHttpRequestException badRequestEx)
            {
                context.Response.StatusCode = badRequestEx.StatusCode;
            }

            if (context.Request.AcceptsJson()
                && context.RequestServices.GetRequiredService<IProblemDetailsService>() is { } problemDetailsService)
            {
                // Write as JSON problem details
                await problemDetailsService.WriteAsync(new()
                {
                    HttpContext = context,
                    AdditionalMetadata = exceptionHandlerFeature?.Endpoint?.Metadata,
                    ProblemDetails = { Status = context.Response.StatusCode }
                });
            }
            else
            {
                context.Response.ContentType = "text/plain";
                var message = ReasonPhrases.GetReasonPhrase(context.Response.StatusCode) switch
                {
                    { Length: > 0 } reasonPhrase => reasonPhrase,
                    _ => "An error occurred"
                };
                await context.Response.WriteAsync(message + "\r\n");
                await context.Response.WriteAsync($"Request ID: {Activity.Current?.Id ?? context.TraceIdentifier}");
            }
        }
    });
}

app.UseAntiforgery();

app.UseStatusCodePages();

app.UseAuthentication();
app.UseAuthorization();

var examples = app.MapGroup("/")
    .WithTags("Examples")
    .WithOpenApi();

// Add an endpoint that forces an exception to be thrown when requested
examples.MapGet("/throw/{statusCode?}", (int? statusCode) =>
    {
        throw statusCode switch
        {
            >= 400 and < 500 => new BadHttpRequestException(
                $"{statusCode} {ReasonPhrases.GetReasonPhrase(statusCode.Value)}",
                statusCode.Value),
            null => new Exception("uh oh"),
            _ => new Exception($"Staus code {statusCode}")
        };
    });

// Hello World
examples.MapGet("/", () => "Hello World!")
   .WithName("HelloWorld");

examples.MapGet("/hello", () => new { Hello = "World" })
   .WithName("HelloWorldApi");

examples.MapGet("/goodbye", () => new { Goodbye = "World" });

examples.MapGet("/hellofunc", Endpoints.HelloWorldFunc)
   .WithName(nameof(Endpoints.HelloWorldFunc));

// Working with raw JSON
examples.MapGet("/jsonraw", () => JsonDocument.Parse("{ \"Id\": 123, \"Name\": \"Example\" }"))
   .WithName("RawJsonOutput");

examples.MapPost("/jsonraw", (JsonElement json) => $"Thanks for the JSON:\r\n{json}")
   .WithName("RawJsonInput");

// Example HTML output from custom IResult
app.MapGet("/html", (HttpContext context) =>
    Results.Extensions.Html(
        $"""
            <!doctype html>
            <html>
              <head><title>miniHTML</title></head>
              <body>
                <h1>Hello World</h1>
                <p>The time on the server is {DateTime.Now:O}</p>
              </body>
            </html>
        """))
   .ExcludeFromDescription();

// Getting user information
examples.MapGet("/user/from-context", (HttpContext httpContext) =>
        new { message = $"Hello {httpContext.User?.Identity?.Name ?? "guest"}!" })
    .WithName("GetUserFromHttpContext");

examples.MapGet("/user/from-principal", (ClaimsPrincipal user) =>
        new { message = $"Hello {user?.Identity?.Name ?? "guest"}!" })
    .WithName("GetUserFromPrincipal");

// Protecting APIs with authentication/authorization
examples.MapGet("/secret", (ClaimsPrincipal user) =>
        $"Shhh {user.Identity?.Name ?? throw new BadHttpRequestException("Who are you?")}, it's a secret")
    .Produces(200, typeof(string), "text/plain")
    .RequireAuthorization(); // Uses AuthorizationOptions.DefaultPolicy

examples.MapGet("/secret/role", (ClaimsPrincipal user) => $"You are have the 'SecretReader' role")
    .RequireAuthorization(policy => policy.RequireAuthenticatedUser().RequireRole("SecretReader"));

examples.MapGet("/secret/scope", (ClaimsPrincipal user) => $"You have the 'admin' scope claim")
    .RequireAuthorization(policy => policy.RequireAuthenticatedUser().RequireClaim("scope", "admin"));

// Example file output from custom IResult
examples.MapGet("/htmlfile", (HttpContext context) => Results.Extensions.FromFile("Files\\example.html"))
   .ExcludeFromDescription();

// Example file output
app.MapGet("/getfile", (HttpContext context, IWebHostEnvironment env) =>
    Results.File(env.ContentRootFileProvider.GetFileInfo("Files\\example.html").PhysicalPath!, "text/html"))
   .ExcludeFromDescription();

// Example file upload
examples.MapPost("/fromform", (IFormFileCollection formFiles)
    => $"Thanks for {formFiles.Count} {(formFiles.Count == 1 ? "file" : "files")}!");

// Parameter optionality
examples.MapGet("/optionality/{value?}", (string? value, int? number) =>
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
    });

// Custom parameter binding via [TargetType].TryParse()
examples.MapGet("/point", (Point point) => $"Point: {point}");

// Custom parameter binding via [TargetType].BindAsync()
examples.MapGet("/paged", (PagingData paging) =>
    $"ToString: {paging}\r\nToQueryString: {paging.ToQueryString()}");

// Example of a wrapper generic type the can bind its generic argument
examples.MapGet("/wrapped/{id}", (Wrapped<int> id) =>
    $"Successfully parsed {id.Value} as Wrapped<int>!");

// Example of bind logic coming from static methods defined on inherited/implemented interface
examples.MapPost("/bind-via-interface", (ExampleInput input) =>
    $"Successfully bound {input.StringProperty} as ExampleInput!")
    .Accepts<ExampleInput>("application/json");

// Example of using a custom binder to get the request body as a delegate parameter
examples.MapPost("/bind-request-body/as-string", (Body<string> body) => $"Received: {body}");
examples.MapPost("/bind-request-body/as-bytes", (Body<byte[]> body) => $"Received {body.Value.Length} bytes")
    .Accepts<string>("text/plain");
examples.MapPost("/bind-request-body/as-rom", (Body<ReadOnlyMemory<byte>> body) => $"Received {body.Value.Length} bytes")
    .Accepts<byte[]>("application/octet-stream");

// An example extensible binder system that allows for parameter binders to be configured in DI
examples.MapPost("/model", (Bind<Todo> model) =>
    {
        Todo? todo = model;
        return TypedResults.Ok(todo);
    });

examples.MapPost("/model-nobinder", (Bind<NoBinder> model) =>
    {
        NoBinder? value = model;
        return TypedResults.Ok(value);
    });

// Example of using a custom wrapper type that suppresses the default response in cases of binding errors
examples.MapPost("/suppress-defaults", (SuppressDefaultResponse<Todo?> todo, HttpContext httpContext) =>
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

        return TypedResults.Ok(todo.Value);
    });

// Example of using a custom wrapper type that suppresses the default parameter binding logic
examples.MapPost("/suppress-binding",
    async Task<Results<BadRequest<string>, Ok<Todo>, PlainText, UnprocessableEntity<string>>> (
        SuppressBinding<Todo?> todo,
        HttpContext httpContext) =>
    {
        try
        {
            // Manually invoke the default binding logic
            var (boundValue, statusCode) = await DefaultBinder<Todo>.GetValueAsync(httpContext);

            if (statusCode != 200)
            {
                // The default binding resulted in a default response, e.g. 400
                // We can respond how we like instead
                return TypedResults.BadRequest($"Issue with default binding, status code returned was {statusCode}");
            }

            return boundValue switch
            {
                object => TypedResults.Ok(boundValue),
                _ => Results.Extensions.PlainText("Bound value was null")
            };
        }
        catch (Exception ex)
        {
            // Exception occurred during default binding!
            return TypedResults.UnprocessableEntity(ex.ToString());
        }
    });

// Using MVC's model binding logic via a generic wrapping shim
examples.MapGet("/paged2", (ModelBinder<PagedData> paging) =>
    $"model: {paging.Model}, valid: {paging.ModelState.IsValid}");

// Overriding/mutating response defaults using middleware
app.UseMutateResponse();

examples.MapGet("/mutate-test/{id}", (int? id) =>
    {
        // Request this via /mutate-test/foo will return 400 by default
        return $"Id of '{id}' was bound from request successfully!";
    })
    .MutateResponse(404, "The ID specified was not in the correct format. Please don't do that.");

// Todos API
var todos = app.MapGroup("/todos").WithTags("TodoApi");

todos.MapGet("/sample", () => new[] {
        new Todo { Id = 1, Title = "Do this" },
        new Todo { Id = 2, Title = "Do this too" }
    })
   .WithTags("Examples");

todos.MapGet("", async (TodoDb db) => await db.Todos.ToListAsync())
   .WithName("GetAllTodos");

todos.MapGet("/incompleted", async (TodoDb db) => await db.Todos.Where(t => !t.IsComplete).ToListAsync())
   .WithName("GetIncompletedTodos");

todos.MapGet("/completed", async (TodoDb db) => await db.Todos.Where(t => t.IsComplete).ToListAsync())
   .WithName("GetCompletedTodos");

todos.MapGet("/{id}", async Task<Results<Ok<Todo>, NotFound>> (int id, TodoDb db) =>
    {
        return await db.Todos.FindAsync(id)
            is Todo todo
                ? TypedResults.Ok(todo)
                : TypedResults.NotFound();
    })
    .WithName("GetTodoById");

todos.MapPost("/", async Task<Results<ValidationProblem, Created<Todo>>> (Todo todo, TodoDb db) =>
    {
        if (!MiniValidator.TryValidate(todo, out var errors))
            return TypedResults.ValidationProblem(errors);

        db.Todos.Add(todo);
        await db.SaveChangesAsync();

        return TypedResults.Created($"/todo/{todo.Id}", todo);
    })
    .WithName("AddTodo");

// Example of a custom DTO base type that could use a shared abstract static method to implement binding
todos.MapPost("/dto", Results<ValidationProblem, Created<Todo>> (CreateTodoInput input, TodoDb db) =>
    {
        if (!MiniValidator.TryValidate(input, out var errors))
            return TypedResults.ValidationProblem(errors);

        // Process the DTO here
        var newTodo = new Todo { Id = 1, Title = input.Title };

        return TypedResults.Created($"/todo/{newTodo.Id}", newTodo);
    })
    .WithName("AddTodoViaDto");

// Example of a custom wrapper type that performs validation
todos.MapPost("/validated-wrapper",
    async Task<Results<ValidationProblem, Created<Todo>>> (Validated<Todo> inputTodo, TodoDb db) =>
    {
        var (todo, isValid) = inputTodo;
        if (!isValid || todo == null)
            return TypedResults.ValidationProblem(inputTodo.Errors);

        db.Todos.Add(todo);
        await db.SaveChangesAsync();

        return TypedResults.Created($"/todo/{todo.Id}", todo);
    })
    .WithName("AddTodo_ValidatedWrapper")
    .Accepts<Todo>("application/json");

// Example of adding an endpoint via a local function MethodGroup with attributes to describe it
todos.MapPost("/todos-local-func", AddTodoFunc);

// EndpointName set automatically to name of method
[Mvc.ProducesResponseType(typeof(Mvc.ValidationProblemDetails), StatusCodes.Status400BadRequest)]
[Mvc.ProducesResponseType(typeof(Todo), StatusCodes.Status201Created)]
[EndpointName(nameof(AddTodoFunc))]
async Task<IResult> AddTodoFunc(Todo todo, TodoDb db)
{
    if (!MiniValidator.TryValidate(todo, out var errors))
        return Results.ValidationProblem(errors);

    db.Todos.Add(todo);
    await db.SaveChangesAsync();

    return Results.Created($"/todos/{todo.Id}", todo);
}

// Example of manually supporting more than JSON for input/output
todos.MapPost("/xmlorjson",
    async Task<Results<UnsupportedMediaType, ValidationProblem, CreatedJsonOrXml<Todo>>> (HttpRequest request, TodoDb db) =>
    {
        string? contentType = request.Headers.ContentType;

        var todo = contentType switch
        {
            "application/json" => await request.ReadFromJsonAsync<Todo>(),
            "application/xml" => await request.ReadFromXmlAsync<Todo>(request.ContentLength),
            _ => null,
        };

        if (todo is null)
            return Results.Extensions.UnsupportedMediaType();

        if (!MiniValidator.TryValidate(todo, out var errors))
            return TypedResults.ValidationProblem(errors);

        db.Todos.Add(todo);
        await db.SaveChangesAsync();

        return Results.Extensions.CreatedJsonOrXml(todo, contentType!);
    })
    .WithName("AddTodoXmlOrJson")
    .Accepts<Todo>("application/json", "application/xml");

// Example of manually supporting file upload (comment out RequiresAntiforgery() line to allow POST from browser)
todos.MapGet("/fromfile", (HttpContext httpContext, IAntiforgery antiforgery) =>
    {
        var tokenSet = antiforgery.GetTokens(httpContext);

        return tokenSet;
    })
    .WithName("AddTodosFromFile_GetAntiXsrfToken");

todos.MapPost("/fromfile",
    async Task<Results<ValidationProblem, Created<List<Todo>>>> (JsonFormFile<List<Todo>> todosFile, TodoDb db) =>
    {
        var todos = todosFile.Value;

        if (!(todos?.Count > 0))
            return TypedResults.ValidationProblem(new Dictionary<string, string[]>()
            {
                { nameof(todosFile), new[] { "The uploaded file contained no todos." } }
            });

        var todoCount = 0;
        foreach (var todo in todos)
        {
            if (!MiniValidator.TryValidate(todo, out var errors))
                return TypedResults.ValidationProblem(
                    errors.ToDictionary(entry => $"[{todoCount}].{entry.Key}", entry => entry.Value));

            db.Todos.Add(todo);
            todoCount++;
        }

        await db.SaveChangesAsync();

        return TypedResults.Created(string.Join(';', todos.Select(t => $"/todo/{t.Id}")), todos);
    })
    .WithName("AddTodosFromFile");

todos.MapPut("/{id}", async Task<Results<ValidationProblem, NoContent, NotFound>> (int id, Todo inputTodo, TodoDb db) =>
    {
        if (!MiniValidator.TryValidate(inputTodo, out var errors))
            return TypedResults.ValidationProblem(errors);

        if (await db.Todos.FindAsync(id) is Todo todo)
        {
            todo.Title = inputTodo.Title;
            todo.IsComplete = inputTodo.IsComplete;
            await db.SaveChangesAsync();
            return TypedResults.NoContent();
        }
        else
        {
            return TypedResults.NotFound();
        }
    })
    .WithName("UpdateTodo");

todos.MapPut("/{id}/mark-complete", async Task<Results<NoContent, NotFound>> (int id, TodoDb db) =>
    {
        if (await db.Todos.FindAsync(id) is Todo todo)
        {
            todo.IsComplete = true;
            await db.SaveChangesAsync();
            return TypedResults.NoContent();
        }
        else
        {
            return TypedResults.NotFound();
        }
    })
    .WithName("CompleteTodo");

todos.MapPut("/{id}/mark-incomplete", async Task<Results<NoContent, NotFound>> (int id, TodoDb db) =>
    {
        if (await db.Todos.FindAsync(id) is Todo todo)
        {
            todo.IsComplete = false;
            await db.SaveChangesAsync();
            return TypedResults.NoContent();
        }
        else
        {
            return TypedResults.NotFound();
        }
    })
    .WithName("UncompleteTodo");

todos.MapDelete("/{id}", async Task<Results<Ok<Todo>, NotFound>> (int id, TodoDb db) =>
    {
        if (await db.Todos.FindAsync(id) is Todo todo)
        {
            db.Todos.Remove(todo);
            await db.SaveChangesAsync();
            return TypedResults.Ok(todo);
        }

        return TypedResults.NotFound();
    })
    .WithName("DeleteTodo");

todos.MapDelete("/delete-all", async Task<Ok<int>> (TodoDb db) =>
    {
        var rowCount = await db.Database.ExecuteSqlRawAsync("DELETE FROM Todos");

        return TypedResults.Ok(rowCount);
    })
    .WithName("DeleteAllTodos");

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

    public DbSet<Todo> Todos { get; set; } = default!;
}

// Make the implicit Program class public so test projects can access it
public partial class Program { }
