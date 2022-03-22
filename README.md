# Minimal API Playground
A place I'm trying out the new ASP.NET Core minimal APIs for hosting and HTTP APIs.

## Dependencies
Code in this repo depends on the very latest bits. If you want to try it out, [grab the latest .NET 6 SDK installer](https://dotnet.microsoft.com/download/dotnet/6.0).

### MinimalValidation
First-class support for validation as part of the new minimal APIs unfortunately did not land in .NET 6. However it's fairly straightforward to wire up the validation features found in `System.ComponentModel.Validation` through the use of a helper library ([like the example this repo uses](https://github.com/DamianEdwards/MiniValidation)), or by using an existing validation library like [FluentValidation](https://fluentvalidation.net/).

## Projects

### Todo.Dapper
[This project](src/Todo.Dapper) implements a simple Todos API including OpenAPI (Swagger) documentation and UI, and uses the [Dapper library](https://dapperlib.github.io/Dapper/) to perist data to a SQLite database.

There are some simple tests for this project in the [tests/Todo.Dapper.Tests](tests/Todo.Dapper.Tests) project.

### Todo.EFCore
[This project](src/Todo.EFCore) implements a simple Todos API including OpenAPI (Swagger) documentation and UI, and uses  using [Entity Framework Core](https://docs.microsoft.com/en-us/ef/core/) to perist data to a SQLite database.

### MinimalApiPlayground
This project contains numerous examples of ways to use and extend the new minimal APIs in ASP.NET Core 6 to build HTTP APIs.

While the `Program.cs` file in the project root is where the APIs are registered and implemented, much of the custom code is in the `Properties` directory. I keep it there as almost all .NET projects have a `Properties` directory and I wanted to avoid additional directories in the project to avoid any implication that additional special directories are required. Ultimately it's just code and be placed anywhere in the project.

The project includes examples of the following and more:
- Returning strings and objects directly from APIs
- Implenting APIs using inline anonymous lambdas, local functions, or methods 
- Using the in-framework `Results` helper class to return common results
- Returning custom `IResult` objects
- Inferred parameter binding from route data, querystring, request body as JSON, DI container services, and HTTP request objects
- Parameter optionality inference from parameters nullability
- Custom parameter binding from querystring or route data values via `TryParse`
- Custom async parameter binding from the request via `BindAsync`
- An example extensible parameter binding object model `IParameterBinder` that enables creating binders for types you don't own
- Using MVC `ModelBinder` implementations via a custom binding shim
- Handling file uploads via a custom `BindAsync` implementation
- Handling media types other than JSON by working directly against the incoming `HttpRequest` and returning a custom `IResult` implementation
- Handling input validation using the [`MinimalValidation` ](https://github.com/DamianEdwards/MinimalValidation)library
- Configuring error handling using `UseExceptionHandler`
- Mutating responses from APIs via custom middleware
- Using endpoint metadata to customize OpenAPI (Swagger) API descriptions
- An experimental middleware for handling cross-site request forgery concerns using the framework's included `IAntiforgery` functionality
- Running the framework's default inferred parameter binding logic on-demand
- A custom set of extensions to gather metadata from the return types and parameter types in endpoint route handlers and use it to auto-describe complex route handlers to `ApiExplorer` without the need to manually annotate the route handlers with attributes or chained metadata method calls
