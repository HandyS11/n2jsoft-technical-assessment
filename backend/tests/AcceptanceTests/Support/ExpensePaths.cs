namespace AcceptanceTests.Support;

public static class ExpensePaths
{
    /// <summary>The expenses of a report.</summary>
    public static string Of(Guid reportId)
    {
        return $"/v1/expense-reports/{reportId}/expenses";
    }

    /// <summary>One expense, through a report (the one holding it, or not).</summary>
    public static string Of(Guid reportId, Guid id)
    {
        return $"{Of(reportId)}/{id}";
    }
}
