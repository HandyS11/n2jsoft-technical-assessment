namespace Application.Paging;

/// <summary>A 1-based page of a sorted list.</summary>
public sealed record PageRequest(int Page, int PageSize)
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    /// <summary>The highest page whose <see cref="Skip" /> still fits in an <see cref="int" />.</summary>
    public const int MaxPage = int.MaxValue / MaxPageSize;

    public int Skip => (Page - 1) * PageSize;
}
