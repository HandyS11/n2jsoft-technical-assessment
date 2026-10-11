using System.Globalization;
using Microsoft.Net.Http.Headers;

namespace WebApi.Endpoints;

/// <summary>A resource's version (Postgres xmin) as a strong ETag ("\"7\""), and back from If-Match.</summary>
internal static class EntityTags
{
    public static string Of(uint version)
    {
        return new EntityTagHeaderValue($"\"{version}\"").ToString();
    }

    /// <summary>Null for no If-Match or "*" (any version); a 400 for a value that is not one of our ETags.</summary>
    public static uint? ExpectedVersionOf(string? ifMatch)
    {
        if (string.IsNullOrEmpty(ifMatch) || ifMatch == "*")
        {
            return null;
        }

        if (EntityTagHeaderValue.TryParse(ifMatch, out var entityTag)
            && !entityTag.IsWeak
            && uint.TryParse(entityTag.Tag.AsSpan(1, entityTag.Tag.Length - 2), CultureInfo.InvariantCulture,
                out var version))
        {
            return version;
        }

        throw new BadHttpRequestException("If-Match must be \"*\" or an ETag returned by the API.");
    }
}
