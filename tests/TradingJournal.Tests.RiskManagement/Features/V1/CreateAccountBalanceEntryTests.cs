using TradingJournal.Tests.RiskManagement.Helpers;

namespace TradingJournal.Tests.RiskManagement.Features.V1;

public class CreateAccountBalanceEntryValidatorTests
{
    private static readonly CreateAccountBalanceEntry.Validator _validator = new();

    private static CreateAccountBalanceEntry.Command Valid() =>
        new(BalanceEntryType.Deposit, 1000m, "note", DateTime.UtcNow.Date, 1);

    [Fact]
    public void Amount_Must_Be_Positive()
    {
        TestValidationResult<CreateAccountBalanceEntry.Command> result =
            _validator.TestValidate(Valid() with { Amount = 0m });
        result.ShouldHaveValidationErrorFor(x => x.Amount);
    }

    [Fact]
    public void EntryType_Must_Be_In_Enum()
    {
        TestValidationResult<CreateAccountBalanceEntry.Command> result =
            _validator.TestValidate(Valid() with { EntryType = (BalanceEntryType)999 });
        result.ShouldHaveValidationErrorFor(x => x.EntryType);
    }

    [Fact]
    public void Valid_Command_Has_No_Errors()
    {
        TestValidationResult<CreateAccountBalanceEntry.Command> result = _validator.TestValidate(Valid());
        result.ShouldNotHaveAnyValidationErrors();
    }
}

public class CreateAccountBalanceEntryHandlerTests
{
    private const int UserId = 21;

    [Fact]
    public async Task Handle_Deposit_With_No_History_Sets_Balance_And_Creates_Config()
    {
        var entries = new List<AccountBalanceEntry>();
        Mock<DbSet<AccountBalanceEntry>> entriesMock = DbSetMockHelper.CreateMockDbSet(entries);
        AccountBalanceEntry? capturedEntry = null;
        entriesMock.Setup(d => d.Add(It.IsAny<AccountBalanceEntry>())).Callback<AccountBalanceEntry>(e => capturedEntry = e);

        var configs = new List<RiskConfig>();
        Mock<DbSet<RiskConfig>> configsMock = DbSetMockHelper.CreateMockDbSet(configs);
        RiskConfig? capturedConfig = null;
        configsMock.Setup(d => d.Add(It.IsAny<RiskConfig>())).Callback<RiskConfig>(c => capturedConfig = c);

        var context = new Mock<IRiskDbContext>();
        context.Setup(c => c.AccountBalanceEntries).Returns(entriesMock.Object);
        context.Setup(c => c.RiskConfigs).Returns(configsMock.Object);
        context.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new CreateAccountBalanceEntry.Handler(context.Object);
        var command = new CreateAccountBalanceEntry.Command(BalanceEntryType.Deposit, 1000m, "seed", DateTime.UtcNow.Date, UserId);

        Result<int> result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedEntry);
        Assert.Equal(1000m, capturedEntry!.BalanceAfter);
        Assert.NotNull(capturedConfig);
        Assert.Equal(1000m, capturedConfig!.AccountBalance);
        context.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Withdrawal_Subtracts_From_Latest_Balance_And_Updates_Existing_Config()
    {
        var prior = new AccountBalanceEntry
        {
            Id = 1,
            CreatedBy = UserId,
            EntryType = BalanceEntryType.Deposit,
            Amount = 1000m,
            BalanceAfter = 1000m,
            EntryDate = DateTime.UtcNow.Date.AddDays(-1),
        };
        var entries = new List<AccountBalanceEntry> { prior };
        Mock<DbSet<AccountBalanceEntry>> entriesMock = DbSetMockHelper.CreateMockDbSet(entries);
        AccountBalanceEntry? capturedEntry = null;
        entriesMock.Setup(d => d.Add(It.IsAny<AccountBalanceEntry>())).Callback<AccountBalanceEntry>(e => capturedEntry = e);

        var existingConfig = new RiskConfig { CreatedBy = UserId, AccountBalance = 1000m };
        var configs = new List<RiskConfig> { existingConfig };
        Mock<DbSet<RiskConfig>> configsMock = DbSetMockHelper.CreateMockDbSet(configs);

        var context = new Mock<IRiskDbContext>();
        context.Setup(c => c.AccountBalanceEntries).Returns(entriesMock.Object);
        context.Setup(c => c.RiskConfigs).Returns(configsMock.Object);
        context.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new CreateAccountBalanceEntry.Handler(context.Object);
        var command = new CreateAccountBalanceEntry.Command(BalanceEntryType.Withdrawal, 300m, null, DateTime.UtcNow.Date, UserId);

        Result<int> result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(700m, capturedEntry!.BalanceAfter);
        Assert.Equal(700m, existingConfig.AccountBalance);
        configsMock.Verify(d => d.Add(It.IsAny<RiskConfig>()), Times.Never);
    }
}
