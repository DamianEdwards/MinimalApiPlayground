
using Microsoft.AspNetCore.Mvc;

public class PagedData
{
    [FromQuery]
    public int PageIndex { get; set; }

    [FromQuery]
    public int PageSize { get; set; }

    [FromQuery]
    public string? SortBy { get; set; }

    [FromQuery(Name = "sortDir")]
    public string? SortDirection { get; set; }

    public override string ToString() =>
        $"{nameof(SortBy)}:{SortBy}, {nameof(SortDirection)}:{SortDirection}, {nameof(PageIndex)}:{PageIndex}, {nameof(PageSize)}:{PageSize}";
}
