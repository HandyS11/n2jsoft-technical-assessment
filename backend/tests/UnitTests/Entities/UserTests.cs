using Domain.Entities;
using Domain.Exceptions;
using Domain.ValueObjects;

namespace UnitTests.Entities;

public sealed class UserTests
{
    private static readonly Address Paris = new("12 rue de Rivoli", "75001", "Paris");
    private static readonly Address Lyon = new("3 place Bellecour", "69002", "Lyon");
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Create_starts_an_active_user_with_the_given_details()
    {
        var user = User.Create("Juste", "Leblanc", Paris, 10);

        user.Id.ShouldNotBe(Guid.Empty);
        user.FirstName.ShouldBe("Juste");
        user.LastName.ShouldBe("Leblanc");
        user.Address.ShouldBe(Paris);
        user.MonthlyExpenseQuota.ShouldBe(10);
        user.IsActive.ShouldBeTrue();
        user.DeletedAt.ShouldBeNull();
    }

    [Fact]
    public void Create_gives_every_user_its_own_id()
    {
        User.Create("Juste", "Leblanc", Paris, 10).Id.ShouldNotBe(User.Create("Juste", "Leblanc", Paris, 10).Id);
    }

    [Fact]
    public void Create_trims_the_names()
    {
        var user = User.Create("  Juste ", " Leblanc  ", Paris, 10);

        user.FirstName.ShouldBe("Juste");
        user.LastName.ShouldBe("Leblanc");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_a_blank_first_name(string firstName)
    {
        Should.Throw<InvalidValueException>(() => User.Create(firstName, "Leblanc", Paris, 10))
            .ParameterName.ShouldBe("firstName");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_a_blank_last_name(string lastName)
    {
        Should.Throw<InvalidValueException>(() => User.Create("Juste", lastName, Paris, 10))
            .ParameterName.ShouldBe("lastName");
    }

    [Fact]
    public void Create_accepts_names_of_100_characters()
    {
        var name = new string('a', User.NameMaxLength);

        var user = User.Create(name, name, Paris, 10);

        user.FirstName.ShouldBe(name);
        user.LastName.ShouldBe(name);
    }

    [Fact]
    public void Create_rejects_a_name_longer_than_100_characters()
    {
        var name = new string('a', User.NameMaxLength + 1);

        Should.Throw<InvalidValueException>(() => User.Create(name, "Leblanc", Paris, 10)).ParameterName
            .ShouldBe("firstName");
    }

    [Fact]
    public void Create_accepts_a_monthly_quota_of_1()
    {
        User.Create("Juste", "Leblanc", Paris, 1).MonthlyExpenseQuota.ShouldBe(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_rejects_a_monthly_quota_below_1(int monthlyExpenseQuota)
    {
        Should.Throw<InvalidValueException>(() => User.Create("Juste", "Leblanc", Paris, monthlyExpenseQuota))
            .ParameterName.ShouldBe("monthlyExpenseQuota");
    }

    [Fact]
    public void UpdateDetails_replaces_the_details()
    {
        var user = User.Create("Juste", "Leblanc", Paris, 10);

        user.UpdateDetails(" Marc ", "Assin", Lyon, 3);

        user.FirstName.ShouldBe("Marc");
        user.LastName.ShouldBe("Assin");
        user.Address.ShouldBe(Lyon);
        user.MonthlyExpenseQuota.ShouldBe(3);
    }

    [Fact]
    public void UpdateDetails_keeps_the_status()
    {
        var user = User.Create("Juste", "Leblanc", Paris, 10);
        user.Deactivate();

        user.UpdateDetails("Juste", "Leblanc", Lyon, 10);

        user.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void UpdateDetails_rejects_invalid_details_and_keeps_the_previous_ones()
    {
        var user = User.Create("Juste", "Leblanc", Paris, 10);

        Should.Throw<InvalidValueException>(() => user.UpdateDetails("Marc", " ", Lyon, 3)).ParameterName
            .ShouldBe("lastName");

        user.FirstName.ShouldBe("Juste");
        user.LastName.ShouldBe("Leblanc");
        user.Address.ShouldBe(Paris);
        user.MonthlyExpenseQuota.ShouldBe(10);
    }

    [Fact]
    public void Deactivate_makes_the_user_inactive()
    {
        var user = User.Create("Juste", "Leblanc", Paris, 10);

        user.Deactivate();

        user.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Activate_makes_an_inactive_user_active_again()
    {
        var user = User.Create("Juste", "Leblanc", Paris, 10);
        user.Deactivate();

        user.Activate();

        user.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Activating_an_active_user_changes_nothing()
    {
        var user = User.Create("Juste", "Leblanc", Paris, 10);

        user.Activate();

        user.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void Deactivating_an_inactive_user_changes_nothing()
    {
        var user = User.Create("Juste", "Leblanc", Paris, 10);
        user.Deactivate();

        user.Deactivate();

        user.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Delete_records_when_the_user_was_deleted()
    {
        var user = User.Create("Juste", "Leblanc", Paris, 10);

        user.Delete(Now);

        user.DeletedAt.ShouldBe(Now);
    }

    [Fact]
    public void Deleting_again_keeps_the_first_deletion_date()
    {
        var user = User.Create("Juste", "Leblanc", Paris, 10);
        user.Delete(Now);

        user.Delete(Now.AddDays(1));

        user.DeletedAt.ShouldBe(Now);
    }
}
