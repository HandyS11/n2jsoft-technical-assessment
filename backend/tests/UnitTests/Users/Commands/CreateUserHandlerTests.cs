using Application.Users;
using Application.Users.Commands;
using Domain.Entities;
using Domain.ValueObjects;
using NSubstitute;

namespace UnitTests.Users.Commands;

public sealed class CreateUserHandlerTests
{
    private static readonly Address Bordeaux = new("5 rue Victor Hugo", "33000", "Bordeaux");
    private readonly IUserStore _userStore = Substitute.For<IUserStore>();

    [Fact]
    public async Task Adds_an_active_user_with_the_given_details_then_saves()
    {
        User? added = null;
        _userStore.Add(Arg.Do<User>(user => added = user));

        await CreateHandler().HandleAsync(
            new CreateUserCommand("Jeanne", "Martin", Bordeaux, 8),
            TestContext.Current.CancellationToken);

        added.ShouldNotBeNull();
        added.FirstName.ShouldBe("Jeanne");
        added.LastName.ShouldBe("Martin");
        added.Address.ShouldBe(Bordeaux);
        added.MonthlyExpenseQuota.ShouldBe(8);
        added.IsActive.ShouldBeTrue();
        Received.InOrder(() =>
        {
            _userStore.Add(added);
            _ = _userStore.SaveChangesAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task Returns_the_id_of_the_new_user()
    {
        User? added = null;
        _userStore.Add(Arg.Do<User>(user => added = user));

        var id = await CreateHandler().HandleAsync(
            new CreateUserCommand("Jeanne", "Martin", Bordeaux, 8),
            TestContext.Current.CancellationToken);

        id.ShouldBe(added.ShouldNotBeNull().Id);
    }

    [Fact]
    public async Task Rejects_invalid_details_without_saving()
    {
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => CreateHandler().HandleAsync(
            new CreateUserCommand("Jeanne", "Martin", Bordeaux, 0),
            TestContext.Current.CancellationToken));

        _userStore.DidNotReceive().Add(Arg.Any<User>());
        await _userStore.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private CreateUserHandler CreateHandler()
    {
        return new CreateUserHandler(_userStore);
    }
}
