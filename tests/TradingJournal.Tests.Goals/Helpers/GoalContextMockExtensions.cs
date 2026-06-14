using TradingJournal.Modules.Goals.Infrastructure;

namespace TradingJournal.Tests.Goals.Helpers;

public static class GoalContextMockExtensions
{
    /// <summary>
    /// Wires <see cref="IGoalDbContext.ExecuteInTransactionAsync{TResult}"/> on a
    /// mock to simply invoke the supplied operation, so handlers that wrap their
    /// writes in a transaction can be unit-tested without a real database. Set up
    /// once per closed result type the handler returns.
    /// </summary>
    public static Mock<IGoalDbContext> SetupTransactionPassthrough<TResult>(this Mock<IGoalDbContext> context)
    {
        context
            .Setup(c => c.ExecuteInTransactionAsync(
                It.IsAny<Func<CancellationToken, Task<TResult>>>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<CancellationToken, Task<TResult>>, CancellationToken>((operation, ct) => operation(ct));
        return context;
    }
}
