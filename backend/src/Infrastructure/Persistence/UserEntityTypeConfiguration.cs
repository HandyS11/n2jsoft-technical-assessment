using Domain.Entities;
using Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence;

internal sealed class UserEntityTypeConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        // Id is get-only, so EF Core does not discover it as the key by convention.
        builder.HasKey(user => user.Id);
        builder.Property(user => user.FirstName).HasMaxLength(User.NameMaxLength);
        builder.Property(user => user.LastName).HasMaxLength(User.NameMaxLength);
        builder.ComplexProperty(user => user.Address, address =>
        {
            address.Property(a => a.Street).HasMaxLength(Address.StreetMaxLength);
            address.Property(a => a.PostalCode).HasMaxLength(Address.PostalCodeLength).IsFixedLength();
            address.Property(a => a.City).HasMaxLength(Address.CityMaxLength);
        });
        // Npgsql maps a uint row version to the xmin system column, which Postgres changes on every update.
        builder.Property(user => user.Version).IsRowVersion();
        // A deleted user is invisible to every query, so every read and update of one answers "not found".
        builder.HasQueryFilter(user => user.DeletedAt == null);
    }
}
