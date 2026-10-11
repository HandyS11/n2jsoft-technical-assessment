using Domain.Entities;
using Domain.Exceptions;
using Domain.ValueObjects;

namespace UnitTests.Entities;

public sealed class ExpenseReportTests
{
    private static readonly Address Paris = new("12 rue de Rivoli", "75001", "Paris");
    private static readonly ReportMonth October2025 = new(2025, 10);

    [Fact]
    public void Create_assigns_the_user_and_the_month()
    {
        var user = User.Create("Juste", "Leblanc", Paris, 10);

        var report = ExpenseReport.Create(user, October2025);

        report.Id.ShouldNotBe(Guid.Empty);
        report.UserId.ShouldBe(user.Id);
        report.Year.ShouldBe(2025);
        report.Month.ShouldBe(10);
        report.Period.ShouldBe(October2025);
    }

    [Fact]
    public void Create_titles_the_report_with_the_user_name_then_the_month()
    {
        var report = ExpenseReport.Create(User.Create("Juste", "Leblanc", Paris, 10), October2025);

        report.Title.ShouldBe("Juste Leblanc - Octobre 2025");
    }

    [Fact]
    public void Create_gives_every_report_its_own_id()
    {
        var user = User.Create("Juste", "Leblanc", Paris, 10);

        ExpenseReport.Create(user, October2025).Id.ShouldNotBe(ExpenseReport.Create(user, October2025).Id);
    }

    [Fact]
    public void The_title_fits_its_column_for_the_longest_names_and_month()
    {
        var name = new string('a', User.NameMaxLength);

        var report = ExpenseReport.Create(User.Create(name, name, Paris, 10), new ReportMonth(2025, 9));

        report.Title.Length.ShouldBeLessThanOrEqualTo(ExpenseReport.TitleMaxLength);
    }

    [Fact]
    public void The_title_does_not_follow_a_later_rename_of_the_user()
    {
        var user = User.Create("Juste", "Leblanc", Paris, 10);
        var report = ExpenseReport.Create(user, October2025);

        user.UpdateDetails("Justine", "Leblanc", Paris, 10);

        report.Title.ShouldBe("Juste Leblanc - Octobre 2025");
    }

    [Fact]
    public void Create_refuses_an_inactive_user()
    {
        var user = User.Create("Juste", "Leblanc", Paris, 10);
        user.Deactivate();

        Should.Throw<InactiveUserException>(() => ExpenseReport.Create(user, October2025))
            .Code.ShouldBe("expense_report.inactive_user");
    }
}
