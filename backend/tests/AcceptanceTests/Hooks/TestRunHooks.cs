using AcceptanceTests.Support;
using Reqnroll;

namespace AcceptanceTests.Hooks;

[Binding]
public static class TestRunHooks
{
    [BeforeTestRun]
    public static Task StartTestEnvironmentAsync()
    {
        return TestEnvironment.StartAsync();
    }

    [BeforeScenario]
    public static Task ResetDatabaseAsync()
    {
        return TestEnvironment.ResetDatabaseAsync();
    }

    [AfterTestRun]
    public static Task StopTestEnvironmentAsync()
    {
        return TestEnvironment.StopAsync();
    }
}
