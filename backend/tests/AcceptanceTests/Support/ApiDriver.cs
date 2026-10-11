using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AcceptanceTests.Support;

/// <summary>One instance per scenario (Reqnroll context injection), disposed at the end of the scenario.</summary>
public sealed class ApiDriver : IDisposable
{
    private HttpClient _client = TestEnvironment.Factory.CreateClient();
    private HttpResponseMessage? _response;

    public HttpResponseMessage Response =>
        _response ?? throw new InvalidOperationException("No request has been sent yet.");

    public void Dispose()
    {
        _response?.Dispose();
        _client.Dispose();
    }

    /// <summary>Sends the next requests to another host of the API. The caller keeps owning the factory.</summary>
    public void UseFactory(WebApplicationFactory<Program> factory)
    {
        _client.Dispose();
        _client = factory.CreateClient();
    }

    public Task GetAsync(string path)
    {
        return SendAsync(HttpMethod.Get, path);
    }

    public async Task SendAsync(HttpMethod method, string path, JsonNode? body = null, string? ifMatch = null)
    {
        using var request = new HttpRequestMessage(method, new Uri(path, UriKind.Relative));
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        if (ifMatch is not null)
        {
            // Unvalidated: some scenarios send a malformed value on purpose.
            request.Headers.TryAddWithoutValidation("If-Match", ifMatch);
        }

        _response?.Dispose();
        _response = await _client.SendAsync(request);
    }

    /// <summary>Several Then steps read the same response: the buffered string can be read again, the stream cannot.</summary>
    public async Task<T> ReadJsonAsync<T>()
        where T : class
    {
        var json = await Response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json, JsonSerializerOptions.Web).ShouldNotBeNull();
    }
}
