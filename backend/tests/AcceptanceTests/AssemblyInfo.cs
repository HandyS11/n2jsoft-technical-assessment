using Xunit.Sdk;
using Xunit.v3;

// Scenarios share one database that is reset before each scenario: they must not run concurrently.
[assembly: Parallelization(Mode = ParallelMode.None)]
