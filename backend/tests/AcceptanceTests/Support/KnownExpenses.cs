namespace AcceptanceTests.Support;

/// <summary>
/// The expenses a scenario arranged or created, by description ("Taxi"), with the report that holds them. One instance
/// per scenario (Reqnroll context injection), shared by the step classes.
/// </summary>
public sealed class KnownExpenses
{
    private readonly Dictionary<string, (Guid ReportId, Guid Id)> _expenses = [];

    public void Add(string description, Guid reportId, Guid id)
    {
        _expenses[description] = (reportId, id);
    }

    public (Guid ReportId, Guid Id) Of(string description)
    {
        return _expenses.TryGetValue(description, out var expense)
            ? expense
            : throw new InvalidOperationException(
                $"No expense described {description} was arranged or created in this scenario.");
    }
}
