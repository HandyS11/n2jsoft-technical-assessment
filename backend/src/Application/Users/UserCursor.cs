using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Application.Users;

/// <summary>
/// The last user read, by the keys users are sorted on. Sent to the client as an opaque string: the names it holds
/// are those at the time of the read, so renaming that user meanwhile does not move the place to continue from.
/// </summary>
public sealed record UserCursor(string LastName, string FirstName, Guid Id)
{
    public override string ToString()
    {
        return Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(this));
    }

    public static bool TryParse(string value, [NotNullWhen(true)] out UserCursor? cursor)
    {
        try
        {
            cursor = JsonSerializer.Deserialize<UserCursor>(Base64Url.DecodeFromChars(value));
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            cursor = null;
        }

        // Every key is needed: a cursor missing one was not made by this API.
        if (cursor is { LastName: not null, FirstName: not null } && cursor.Id != Guid.Empty)
        {
            return true;
        }

        cursor = null;
        return false;
    }
}
