using System.Reflection;
using Application.Users.Queries;
using Domain.Entities;

namespace UnitTests.Architecture;

public sealed class DependencyRuleTests
{
    private static readonly Assembly DomainAssembly = typeof(User).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(IUserQueries).Assembly;

    [Theory]
    [InlineData("Application")]
    [InlineData("Infrastructure")]
    [InlineData("WebApi")]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("Microsoft.AspNetCore")]
    public void Domain_does_not_depend_on(string forbidden)
    {
        ReferencedAssemblyNames(DomainAssembly)
            .ShouldNotContain(name => DependsOn(name, forbidden), $"Domain must not reference {forbidden}");
    }

    [Theory]
    [InlineData("Infrastructure")]
    [InlineData("WebApi")]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("Microsoft.AspNetCore")]
    public void Application_does_not_depend_on(string forbidden)
    {
        ReferencedAssemblyNames(ApplicationAssembly)
            .ShouldNotContain(name => DependsOn(name, forbidden), $"Application must not reference {forbidden}");
    }

    private static string[] ReferencedAssemblyNames(Assembly assembly)
    {
        return [.. assembly.GetReferencedAssemblies().Select(name => name.Name ?? string.Empty)];
    }

    /// <summary>"Microsoft.AspNetCore" also forbids "Microsoft.AspNetCore.Http.Abstractions" and the like.</summary>
    private static bool DependsOn(string assemblyName, string forbidden)
    {
        return assemblyName == forbidden || assemblyName.StartsWith(forbidden + ".", StringComparison.Ordinal);
    }
}
