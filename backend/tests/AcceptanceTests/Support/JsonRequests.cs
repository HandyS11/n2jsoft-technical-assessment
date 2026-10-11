using System.Text.Json.Nodes;

namespace AcceptanceTests.Support;

public static class JsonRequests
{
    /// <summary>Replaces the field at <paramref name="path" /> ("address.city") with the parsed JSON value.</summary>
    public static JsonObject With(JsonObject request, string path, string json)
    {
        var segments = path.Split('.');
        var parent = segments[..^1].Aggregate(request, (node, segment) => node[segment]!.AsObject());
        parent[segments[^1]] = JsonNode.Parse(json);
        return request;
    }
}
