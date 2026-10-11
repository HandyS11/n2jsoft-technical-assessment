using Application.Exceptions;
using Domain.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using WebApi.Errors;

namespace UnitTests.Errors;

public sealed class ApiExceptionHandlerTests : IDisposable
{
    /// <summary>AddOptions() is required: AddProblemDetails() alone does not register IOptions&lt;&gt;.</summary>
    private readonly ServiceProvider _services = new ServiceCollection()
        .AddOptions()
        .AddProblemDetails()
        .BuildServiceProvider();

    public void Dispose()
    {
        _services.Dispose();
    }

    [Fact]
    public async Task NotFoundException_is_written_as_a_404_problem_naming_the_resource()
    {
        var context = ProblemDetailsTestContext.Create(_services);

        var handled = await CreateHandler().TryHandleAsync(
            context,
            new NotFoundException("User", 42),
            TestContext.Current.CancellationToken);

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
        context.Response.ContentType.ShouldBe("application/problem+json");
        var body = await ProblemDetailsTestContext.ReadBodyAsync(context);
        body.GetProperty("status").GetInt32().ShouldBe(404);
        body.GetProperty("detail").GetString().ShouldBe("User '42' was not found.");
    }

    [Fact]
    public async Task NotFoundException_keeps_its_404_when_the_client_does_not_accept_json()
    {
        var context = ProblemDetailsTestContext.Create(_services);
        context.Request.Headers.Accept = "application/xml";

        var handled = await CreateHandler().TryHandleAsync(
            context,
            new NotFoundException("User", 42),
            TestContext.Current.CancellationToken);

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task BusinessRuleViolationException_is_written_as_a_422_problem_with_its_code()
    {
        var context = ProblemDetailsTestContext.Create(_services);

        var handled = await CreateHandler().TryHandleAsync(
            context,
            new TestRuleViolationException(),
            TestContext.Current.CancellationToken);

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status422UnprocessableEntity);
        var body = await ProblemDetailsTestContext.ReadBodyAsync(context);
        body.GetProperty("status").GetInt32().ShouldBe(422);
        body.GetProperty("code").GetString().ShouldBe("test.rule");
        body.GetProperty("detail").GetString().ShouldBe("The test rule was violated.");
    }

    [Fact]
    public async Task InvalidValueException_is_written_as_a_400_validation_problem_keyed_by_the_parameter()
    {
        var context = ProblemDetailsTestContext.Create(_services);

        var handled = await CreateHandler().TryHandleAsync(
            context,
            new InvalidValueException("street", "The value must not be blank."),
            TestContext.Current.CancellationToken);

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        var body = await ProblemDetailsTestContext.ReadBodyAsync(context);
        body.GetProperty("status").GetInt32().ShouldBe(400);
        body.GetProperty("errors").GetProperty("street")[0].GetString().ShouldBe("The value must not be blank.");
    }

    [Fact]
    public async Task BadHttpRequestException_is_written_with_its_own_status_code()
    {
        var context = ProblemDetailsTestContext.Create(_services);

        var handled = await CreateHandler().TryHandleAsync(
            context,
            new BadHttpRequestException("Failed to read parameter.", StatusCodes.Status400BadRequest),
            TestContext.Current.CancellationToken);

        handled.ShouldBeTrue();
        context.Response.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        var body = await ProblemDetailsTestContext.ReadBodyAsync(context);
        body.GetProperty("status").GetInt32().ShouldBe(400);
    }

    [Fact]
    public async Task Any_other_exception_is_left_to_the_default_500_handling()
    {
        var context = ProblemDetailsTestContext.Create(_services);

        var handled = await CreateHandler().TryHandleAsync(
            context,
            new InvalidOperationException("boom"),
            TestContext.Current.CancellationToken);

        handled.ShouldBeFalse();
        context.Response.Body.Length.ShouldBe(0);
    }

    private ApiExceptionHandler CreateHandler()
    {
        return new ApiExceptionHandler(_services.GetRequiredService<IProblemDetailsService>());
    }
}
