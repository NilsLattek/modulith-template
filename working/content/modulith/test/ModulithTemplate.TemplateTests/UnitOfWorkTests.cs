using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using ModulithTemplate.Features.Orders.Domain;
using ModulithTemplate.Features.Orders.Domain.Entities;
using ModulithTemplate.Features.Orders.Infrastructure.Data;

namespace ModulithTemplate.TemplateTests;

/// <summary>
/// <c>UnitOfWorkBase</c> owns a transaction only when nobody else does, and runs the one it owns
/// under the context's execution strategy.
/// </summary>
/// <remarks>
/// Both are what an inbox Consumer relies on: its context is enlisted onto a transaction it did not
/// open, and a retrying strategy (Aspire's default) refuses a transaction it did not start.
/// </remarks>
public class UnitOfWorkTests
{
    private static SomeEntity AnEntity() => SomeEntity.Create("a name", 12.34m);

    [Fact]
    public async Task Inside_an_open_transaction_the_action_is_not_committed_by_the_unit_of_work()
    {
        // Arrange
        await using var solution = SolutionUnderTest.Start();
        var cancellationToken = TestContext.Current.CancellationToken;
        using var scope = solution.CreateScope();
        var orders = scope.ServiceProvider.GetRequiredService<OrdersContext>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IOrdersUnitOfWork>();
        await using var transaction = await orders.Database.BeginTransactionAsync(cancellationToken);

        // Act
        await unitOfWork.ExecuteInTransactionAsync(
            async () =>
            {
                orders.Add(AnEntity());
                await orders.SaveChangesAsync(cancellationToken);
            },
            cancellationToken: cancellationToken);

        // The owner decides: rolling back discards what the action wrote.
        await transaction.RollbackAsync(cancellationToken);

        // Assert
        Assert.Empty(await solution.SomeEntitiesAsync(cancellationToken));
    }

    [Fact]
    public async Task Inside_an_open_transaction_a_failing_action_is_not_rolled_back_by_the_unit_of_work()
    {
        // Arrange
        await using var solution = SolutionUnderTest.Start();
        var cancellationToken = TestContext.Current.CancellationToken;
        using var scope = solution.CreateScope();
        var orders = scope.ServiceProvider.GetRequiredService<OrdersContext>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IOrdersUnitOfWork>();
        await using var transaction = await orders.Database.BeginTransactionAsync(cancellationToken);

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.ExecuteInTransactionAsync(
            async () =>
            {
                orders.Add(AnEntity());
                await orders.SaveChangesAsync(cancellationToken);
                throw new InvalidOperationException("the action failed");
            },
            cancellationToken: cancellationToken));

        // The owner decides: committing keeps what the failed action wrote.
        await transaction.CommitAsync(cancellationToken);

        // Assert
        Assert.Single(await solution.SomeEntitiesAsync(cancellationToken));
    }

    [Fact]
    public async Task Under_a_retrying_strategy_a_transient_failure_replays_the_action_and_its_writes_land_once()
    {
        // Arrange
        // The second save fails, after the first was accepted by the change tracker: a replay that
        // reused the tracker would skip the first entity's re-insert and repeat the second's.
        await using var solution = SolutionUnderTest.Start(app => app.Services.ConfigureContext<OrdersContext>(options => options
            .UseNpgsql(pg => pg.EnableRetryOnFailure(1, TimeSpan.FromMilliseconds(10), errorCodesToAdd: null))
            .AddInterceptors(new TransientFailureInjector(failingCommand: 2))));
        var cancellationToken = TestContext.Current.CancellationToken;
        using var scope = solution.CreateScope();
        var orders = scope.ServiceProvider.GetRequiredService<OrdersContext>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IOrdersUnitOfWork>();
        var attempts = 0;

        // Act
        await unitOfWork.ExecuteInTransactionAsync(
            async () =>
            {
                attempts++;
                orders.Add(SomeEntity.Create("first", 1m));
                await orders.SaveChangesAsync(cancellationToken);
                orders.Add(SomeEntity.Create("second", 2m));
                await orders.SaveChangesAsync(cancellationToken);
            },
            cancellationToken: cancellationToken);

        // Assert
        Assert.Equal(2, attempts);
        var entities = await solution.SomeEntitiesAsync(cancellationToken);
        var names = entities.Select(entity => entity.Name).Order(StringComparer.Ordinal);
        Assert.Equal(["first", "second"], names, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Under_a_retrying_strategy_a_broken_connection_replays_the_action()
    {
        // Arrange
        // 57P01 is the error a killed backend reports; Npgsql does not count it as transient itself.
        await using var solution = SolutionUnderTest.Start(app => app.Services.ConfigureContext<OrdersContext>(options => options
            .UseNpgsql(pg => pg.EnableRetryOnFailure(1, TimeSpan.FromMilliseconds(10), errorCodesToAdd: ["57P01"]))));
        var cancellationToken = TestContext.Current.CancellationToken;
        using var scope = solution.CreateScope();
        var orders = scope.ServiceProvider.GetRequiredService<OrdersContext>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IOrdersUnitOfWork>();
        var attempts = 0;

        // Act
        // A broken connection leaves its transaction unusable, so the failed attempt must end without
        // an error of its own hiding the transient one from the strategy.
        await unitOfWork.ExecuteInTransactionAsync(
            async () =>
            {
                attempts++;
                orders.Add(AnEntity());
                await orders.SaveChangesAsync(cancellationToken);

                if (attempts == 1)
                {
                    await orders.Database.ExecuteSqlRawAsync(
                        "SELECT pg_terminate_backend(pg_backend_pid())", cancellationToken);
                }
            },
            cancellationToken: cancellationToken);

        // Assert
        Assert.Equal(2, attempts);
        Assert.Single(await solution.SomeEntitiesAsync(cancellationToken));
    }
}
