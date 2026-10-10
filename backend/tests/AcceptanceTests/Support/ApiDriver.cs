namespace AcceptanceTests.Support;

/// <summary>One instance per scenario (Reqnroll context injection), disposed at the end of the scenario.</summary>
public sealed class ApiDriver : IDisposable
{
    private readonly HttpClient _client = TestEnvironment.Factory.CreateClient();
    private HttpResponseMessage? _response;

    public HttpResponseMessage Response =>
        _response ?? throw new InvalidOperationException("No request has been sent yet.");

    public void Dispose()
    {
        _response?.Dispose();
        _client.Dispose();
    }

    public async Task GetAsync(string path)
    {
        _response?.Dispose();
        _response = await _client.GetAsync(new Uri(path, UriKind.Relative));
    }
}
