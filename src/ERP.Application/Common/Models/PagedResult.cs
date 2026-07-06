namespace ERP.Application.Common.Models;

/// <summary>A page of results plus the metadata a client needs to page through the rest.</summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}

/// <summary>Common paging inputs, clamped to sane bounds by consumers.</summary>
public abstract record PagedQuery
{
    private const int MaxPageSize = 200;
    private int _pageSize = 25;
    private int _page = 1;

    public int Page
    {
        get => _page;
        init => _page = value < 1 ? 1 : value;
    }

    public int PageSize
    {
        get => _pageSize;
        init => _pageSize = value is < 1 or > MaxPageSize ? 25 : value;
    }

    /// <summary>Optional free-text filter; interpreted per query.</summary>
    public string? Search { get; init; }
}
