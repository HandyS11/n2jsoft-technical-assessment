namespace Infrastructure.Persistence;

/// <summary>The names of the soft-delete query filters, so that a query can lift one and keep the others.</summary>
internal static class QueryFilters
{
    public const string NotDeletedUsers = nameof(NotDeletedUsers);
    public const string NotDeletedExpenses = nameof(NotDeletedExpenses);
}
