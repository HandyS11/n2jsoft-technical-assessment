namespace Domain.Entities;

public sealed class User
{
    public Guid Id { get; set; }
    public required string FirstName { get; init; }
    public required string LastName { get; init; }
}
