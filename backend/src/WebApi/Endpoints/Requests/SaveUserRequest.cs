using System.ComponentModel.DataAnnotations;
using Domain.Entities;

namespace WebApi.Endpoints.Requests;

/// <summary>The body of a user creation or update. [Required] also rejects text made only of spaces.</summary>
public sealed record SaveUserRequest(
    [property: Required]
    [property: StringLength(User.NameMaxLength)]
    string FirstName,
    [property: Required]
    [property: StringLength(User.NameMaxLength)]
    string LastName,
    [property: Required] AddressRequest Address,
    [property: Range(1, int.MaxValue)] int MonthlyExpenseQuota);
