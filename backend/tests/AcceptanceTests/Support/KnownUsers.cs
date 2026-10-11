namespace AcceptanceTests.Support;

/// <summary>
/// The ids of the users a scenario arranged or created, by full name ("Juste Leblanc"). One instance per scenario
/// (Reqnroll context injection), shared by the step classes.
/// </summary>
public sealed class KnownUsers
{
    private readonly Dictionary<string, Guid> _ids = [];

    public void Add(string name, Guid id)
    {
        _ids[name] = id;
    }

    public Guid IdOf(string name)
    {
        return _ids.TryGetValue(name, out var id)
            ? id
            : throw new InvalidOperationException($"No user named {name} was arranged or created in this scenario.");
    }
}
