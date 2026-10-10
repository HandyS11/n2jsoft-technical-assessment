using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace UnitTests.Errors;

internal static class ProblemDetailsTestContext
{
    public static DefaultHttpContext Create(IServiceProvider services)
    {
        return new DefaultHttpContext
        {
            RequestServices = services,
            Response =
            {
                Body = new MemoryStream()
            },
        };
    }

    public static async Task<JsonElement> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var document = await JsonDocument.ParseAsync(
            context.Response.Body,
            cancellationToken: TestContext.Current.CancellationToken);
        return document.RootElement.Clone();
    }
}
