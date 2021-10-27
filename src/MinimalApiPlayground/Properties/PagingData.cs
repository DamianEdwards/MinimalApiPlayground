using System.Globalization;
using System.Reflection;

public struct PagingData
{
    private static readonly string _sortByKey = "sortBy";
    private static readonly string _sortDirectionKey = "sortDir";
    private static readonly string _currentPageKey = "page";

    public string? SortBy { get; init; }
    
    public SortDirection SortDirection { get; init; }

    public int CurrentPage { get; init; } = 1;

    public override string ToString() =>
        $"{nameof(SortBy)}:{SortBy}, {nameof(SortDirection)}:{SortDirection}, {nameof(CurrentPage)}:{CurrentPage}";

    public string ToQueryString()
    {
        var uri = "";
        if (SortBy is not null)
        {
            uri = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(uri, _sortByKey, SortBy);
        }
        if (SortDirection != SortDirection.Default)
        {
            uri = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(uri, _sortDirectionKey, SortDirection.ToString().ToLowerInvariant());
        }
        if (CurrentPage > 1)
        {
            uri = Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(uri, _currentPageKey, CurrentPage.ToString(CultureInfo.InvariantCulture));
        }

        return uri;
    }

    public static ValueTask<PagingData> BindAsync(HttpContext context, ParameterInfo parameter)
    {
        Enum.TryParse<SortDirection>(context.Request.Query[_sortDirectionKey], ignoreCase: true, out var sortDirection);
        int.TryParse(context.Request.Query[_currentPageKey], NumberStyles.None, CultureInfo.InvariantCulture, out var page);
        page = page == 0 ? 1 : page;

        var result = new PagingData
        {
            SortBy = context.Request.Query[_sortByKey],
            SortDirection = sortDirection,
            CurrentPage = page
        };

        return ValueTask.FromResult(result);
    }
}

public enum SortDirection
{
    Default,
    Asc,
    Desc
}