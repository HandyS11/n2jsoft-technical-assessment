using System.ComponentModel.DataAnnotations;

namespace WebApi.Endpoints.Requests;

/// <summary>
/// <see cref="StringLengthAttribute" />, measured once trimmed like the domain does: the spaces around a text, which
/// the domain drops, do not make it too long. Still a <see cref="StringLengthAttribute" />, so the OpenAPI document
/// shows the limit.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class TrimmedStringLengthAttribute(int maximumLength) : StringLengthAttribute(maximumLength)
{
    public override bool IsValid(object? value)
    {
        return base.IsValid(value is string text ? text.Trim() : value);
    }
}
