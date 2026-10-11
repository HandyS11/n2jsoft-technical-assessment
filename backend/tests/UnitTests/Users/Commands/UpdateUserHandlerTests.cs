using Application.Exceptions;
using Application.Users;
using Application.Users.Commands;
using Domain.Entities;
using Domain.ValueObjects;
using NSubstitute;

namespace UnitTests.Users.Commands;

public sealed class UpdateUserHandlerTests
{
    private static readonly Address Paris = new("12 rue de Rivoli", "75001", "Paris");
    private static readonly Address Lyon = new("3 place Bellecour", "69002", "Lyon");
    private readonly IUserStore _userStore = Substitute.For<IUserStore>();

    [Fact]
    public async Task Replaces_the_details_of_the_user_then_saves()
    {
        var user = User.Create("Juste", "Leblanc", Paris, 10);
        _userStore.FindAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        await CreateHandler().HandleAsync(
            new UpdateUserCommand(user.Id, "Marc", "Assin", Lyon, 3),
            TestContext.Current.CancellationToken);

        user.FirstName.ShouldBe("Marc");
        user.LastName.ShouldBe("Assin");
        user.Address.ShouldBe(Lyon);
        user.MonthlyExpenseQuota.ShouldBe(3);
        await _userStore.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Replaces_the_details_when_the_expected_version_is_the_stored_one()
    {
        var user = User.Create("Juste", "Leblanc", Paris, 10);
        _userStore.FindAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        await CreateHandler().HandleAsync(
            new UpdateUserCommand(user.Id, "Marc", "Assin", Lyon, 3, ExpectedVersion: user.Version),
            TestContext.Current.CancellationToken);

        user.FirstName.ShouldBe("Marc");
        await _userStore.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Throws_a_conflict_when_the_user_changed_since_the_expected_version_without_saving()
    {
        var user = User.Create("Juste", "Leblanc", Paris, 10);
        _userStore.FindAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var exception = await Should.ThrowAsync<ConcurrencyConflictException>(() => CreateHandler().HandleAsync(
            new UpdateUserCommand(user.Id, "Marc", "Assin", Lyon, 3, ExpectedVersion: user.Version + 1),
            TestContext.Current.CancellationToken));

        exception.Message.ShouldBe($"User '{user.Id}' was changed by another request.");
        user.FirstName.ShouldBe("Juste");
        await _userStore.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Throws_not_found_for_an_unknown_user_without_saving()
    {
        var id = Guid.NewGuid();

        var exception = await Should.ThrowAsync<NotFoundException>(() => CreateHandler().HandleAsync(
            new UpdateUserCommand(id, "Marc", "Assin", Lyon, 3),
            TestContext.Current.CancellationToken));

        exception.Message.ShouldBe($"User '{id}' was not found.");
        await _userStore.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    private UpdateUserHandler CreateHandler()
    {
        return new UpdateUserHandler(_userStore);
    }
}
