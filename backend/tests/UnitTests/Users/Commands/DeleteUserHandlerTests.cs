using Application.Exceptions;
using Application.Users;
using Application.Users.Commands;
using Domain.Entities;
using Domain.ValueObjects;
using NSubstitute;

namespace UnitTests.Users.Commands;

public sealed class DeleteUserHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 11, 9, 30, 0, TimeSpan.Zero);
    private readonly TimeProvider _timeProvider = Substitute.For<TimeProvider>();
    private readonly IUserStore _userStore = Substitute.For<IUserStore>();

    public DeleteUserHandlerTests()
    {
        _timeProvider.GetUtcNow().Returns(Now);
    }

    [Fact]
    public async Task Marks_the_user_as_deleted_now_then_saves()
    {
        var user = User.Create("Juste", "Leblanc", new Address("12 rue de Rivoli", "75001", "Paris"), 10);
        _userStore.FindAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        await CreateHandler().HandleAsync(new DeleteUserCommand(user.Id), TestContext.Current.CancellationToken);

        user.DeletedAt.ShouldBe(Now);
        await _userStore.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Throws_not_found_for_an_unknown_user_without_saving()
    {
        await Should.ThrowAsync<NotFoundException>(() => CreateHandler().HandleAsync(
            new DeleteUserCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken));

        await _userStore.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private DeleteUserHandler CreateHandler()
    {
        return new DeleteUserHandler(_userStore, _timeProvider);
    }
}
