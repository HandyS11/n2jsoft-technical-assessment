using Domain.Exceptions;

namespace UnitTests.Errors;

/// <summary>Nothing throws a business rule violation before slice 2: this stands in for the concrete rules.</summary>
public sealed class TestRuleViolationException()
    : BusinessRuleViolationException("test.rule", "The test rule was violated.");
