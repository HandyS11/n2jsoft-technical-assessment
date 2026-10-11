namespace AcceptanceTests.Support;

/// <summary>
/// The ids of the expense reports a scenario arranged or created, by title ("Juste Leblanc - Octobre 2025"). One
/// instance per scenario (Reqnroll context injection), shared by the step classes.
/// </summary>
public sealed class KnownExpenseReports
{
    private readonly Dictionary<string, Guid> _ids = [];

    public void Add(string title, Guid id)
    {
        _ids[title] = id;
    }

    public Guid IdOf(string title)
    {
        return _ids.TryGetValue(title, out var id)
            ? id
            : throw new InvalidOperationException(
                $"No expense report titled {title} was arranged or created in this scenario.");
    }
}
