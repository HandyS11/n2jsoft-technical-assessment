using Application.Exceptions;
using Application.Users;
using Application.Users.Commands;
using Domain.Entities;
using Domain.ValueObjects;
using NSubstitute;

namespace UnitTests.Users.Commands;

public sealed class SetUserActivationHandlerTests
{
    private readonly User _user =
        User.Create("Juste", "Leblanc", new Address("12 rue de Rivoli", "75001", "Paris"), 10);

    private readonly IUserStore _userStore = Substitute.For<IUserStore>();

    public SetUserActivationHandlerTests()
    {
        _userStore.FindAsync(_user.Id, Arg.Any<CancellationToken>()).Returns(_user);
    }

    [Fact]
    public async Task Deactivates_the_user_then_saves()
    {
        await CreateHandler().HandleAsync(
            new SetUserActivationCommand(_user.Id, IsActive: false),
            TestContext.Current.CancellationToken);

        _user.IsActive.ShouldBeFalse();
        await _userStore.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Activates_the_user_then_saves()
    {
        _user.Deactivate();

        await CreateHandler().HandleAsync(
            new SetUserActivationCommand(_user.Id, IsActive: true),
            TestContext.Current.CancellationToken);

        _user.IsActive.ShouldBeTrue();
        await _userStore.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Throws_not_found_for_an_unknown_user_without_saving()
    {
        await Should.ThrowAsync<NotFoundException>(() => CreateHandler().HandleAsync(
            new SetUserActivationCommand(Guid.NewGuid(), IsActive: false),
            TestContext.Current.CancellationToken));

        await _userStore.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private SetUserActivationHandler CreateHandler()
    {
        return new SetUserActivationHandler(_userStore);
    }
}
