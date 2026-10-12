using Domain.Exceptions;

namespace UnitTests.Errors;

/// <summary>A rule no feature has: the error handling tests stay independent of the concrete business rules.</summary>
public sealed class TestRuleViolationException()
    : BusinessRuleViolationException("test.rule", "The test rule was violated.");
