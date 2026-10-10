using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using WebApi.Errors;

namespace UnitTests.Errors;

public sealed class ErrorHandlingExtensionsTests
{
    [Fact]
    public async Task Unhandled_exception_detail_is_exposed_in_Development()
    {
        var body = await WriteUnhandledExceptionProblemAsync(Environments.Development);

        body.GetProperty("detail").GetString().ShouldNotBeNull().ShouldContain("Database unreachable");
    }

    [Fact]
    public async Task Unhandled_exception_detail_is_hidden_outside_Development()
    {
        var body = await WriteUnhandledExceptionProblemAsync(Environments.Production);

        body.TryGetProperty("detail", out _).ShouldBeFalse();
        body.ToString().ShouldNotContain("Database unreachable");
    }

    /// <summary>
    /// Mirrors what the exception handler middleware writes when no IExceptionHandler handles the exception.
    /// </summary>
    private static async Task<JsonElement> WriteUnhandledExceptionProblemAsync(string environmentName)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns(environmentName);
        await using var services = new ServiceCollection()
            .AddOptions()
            .AddErrorHandling(environment)
            .BuildServiceProvider();
        var context = ProblemDetailsTestContext.Create(services);
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var written = await services.GetRequiredService<IProblemDetailsService>().TryWriteAsync(
            new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails =
                {
                    Status = StatusCodes.Status500InternalServerError
                },
                Exception = new InvalidOperationException("Database unreachable"),
            });

        written.ShouldBeTrue();
        return await ProblemDetailsTestContext.ReadBodyAsync(context);
    }
}
