using EndToEndTests.Support;

// One AppHost and one browser for the whole run; each test opens its own browser context.
[assembly: AssemblyFixture(typeof(AppFixture))]
