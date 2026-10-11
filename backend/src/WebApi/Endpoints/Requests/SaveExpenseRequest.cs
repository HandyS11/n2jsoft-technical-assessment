using System.ComponentModel.DataAnnotations;
using Domain.Entities;

namespace WebApi.Endpoints.Requests;

/// <summary>
/// The body of an expense creation or update; the date is written "2025-10-15". Nullable, so that a missing date or
/// amount answers 400 instead of binding to 0001-01-01 or 0. A third decimal is refused by the domain, also with a
/// 400.
/// </summary>
public sealed record SaveExpenseRequest(
    [property: Required] DateOnly? Date,
    [property: Required]
    [property: StringLength(Expense.DescriptionMaxLength)]
    string Description,
    [property: Required]
    [property: Range(typeof(decimal), "0.01", "999999.99", ParseLimitsInInvariantCulture = true,
        ConvertValueInInvariantCulture = true)]
    decimal? Amount,
    [property: Required] BillingAddressRequest BillingAddress);
