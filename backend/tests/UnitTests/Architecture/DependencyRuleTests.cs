using System.Reflection;
using Application.Users;
using Domain.Entities;
using Infrastructure;

namespace UnitTests.Architecture;

public sealed class DependencyRuleTests
{
    private static readonly Assembly DomainAssembly = typeof(User).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(IUserStore).Assembly;
    private static readonly Assembly InfrastructureAssembly = typeof(DependencyInjectionExtensions).Assembly;

    [Theory]
    [InlineData("Application")]
    [InlineData("Infrastructure")]
    [InlineData("WebApi")]
    [InlineData("Microsoft.EntityFrameworkCore")]
    [InlineData("Microsoft.AspNetCore")]
    [InlineData("Npgsql")]
    [InlineData("Aspire")]
    [InlineData("OpenTelemetry")]
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
    [InlineData("Npgsql")]
    [InlineData("Aspire")]
    [InlineData("OpenTelemetry")]
    public void Application_does_not_depend_on(string forbidden)
    {
        ReferencedAssemblyNames(ApplicationAssembly)
            .ShouldNotContain(name => DependsOn(name, forbidden), $"Application must not reference {forbidden}");
    }

    [Theory]
    [InlineData("WebApi")]
    [InlineData("Microsoft.AspNetCore")]
    public void Infrastructure_does_not_depend_on(string forbidden)
    {
        ReferencedAssemblyNames(InfrastructureAssembly)
            .ShouldNotContain(name => DependsOn(name, forbidden), $"Infrastructure must not reference {forbidden}");
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
