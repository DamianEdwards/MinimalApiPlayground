static class ResultHelpers
{
    public static IResult Ok() => new OkResult();
    public static IResult Ok(object body) => new JsonResult(body);
    public static IResult NoContent() => new NoContentResult();
    public static IResult CreatedAt(string url, object value = null) => new CreatedAtResult(url, value);
    public static IResult BadRequest() => new BadRequestResult();

    public static IResult BadRequest(IDictionary<string, string[]> errors)
    {
        var problem = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest
        };
        return new ProblemDetailsResult(problem);
    }

    public static IResult NotFound() => new NotFoundResult();
    public static IResult Redirect(string url) => new RedirectResult(url);
    public static IResult Redirect(string url, bool permanent) => new RedirectResult(url, permanent);
    public static IResult StatusCode(int statusCode) => new StatusCodeResult(statusCode);

    class CreatedAtResult : IResult
    {
        private readonly string _url;
        private readonly object _value;

        public CreatedAtResult(string url, object value)
        {
            _url = url;
            _value = value;
        }

        public async Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.StatusCode = StatusCodes.Status201Created;
            httpContext.Response.Headers.Add("Location", _url);

            if (_value is object)
            {
                await httpContext.Response.WriteAsJsonAsync(_value, _value.GetType());
            }
        }
    }

    class ProblemDetailsResult : IResult
    {
        private readonly ProblemDetails _problem;

        public ProblemDetailsResult(ProblemDetails problem)
        {
            _problem = problem ?? new ProblemDetails { Status = StatusCodes.Status400BadRequest };
        }

        public async Task ExecuteAsync(HttpContext httpContext)
        {
            if (_problem.Status.HasValue)
            {
                httpContext.Response.StatusCode = _problem.Status.Value;
            }
            await httpContext.Response.WriteAsJsonAsync(_problem, _problem.GetType());
        }
    }
}