namespace Application.Paging;

/// <summary>A slice of a sorted list; <see cref="Next" /> asks for the following one, and is null after the last.</summary>
public sealed record CursorPage<T>(IReadOnlyList<T> Items, string? Next);
