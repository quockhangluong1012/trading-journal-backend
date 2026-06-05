using TradingJournal.Tests.Setups.Helpers;

namespace TradingJournal.Tests.Setups.Features.V1;

public class CreateTradingSetupValidatorTests
{
    private static readonly CreateTradingSetup.Validator _validator = new();

    private static TradingSetupNodeDto Node(string id, string kind = "step", string title = "Title")
        => new(id, kind, 0, 0, title, null);

    private static CreateTradingSetup.Request Valid() => new(
        "Breakout",
        "desc",
        [Node("start", "start", "Start"), Node("end", "end", "End")],
        [new TradingSetupEdgeDto("e1", "start", "end", null)],
        5);

    [Fact]
    public void Name_Is_Required()
    {
        TestValidationResult<CreateTradingSetup.Request> result = _validator.TestValidate(Valid() with { Name = "" });
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Empty_Nodes_Surface_Diagram_Validation_Error()
    {
        TestValidationResult<CreateTradingSetup.Request> result =
            _validator.TestValidate(Valid() with { Nodes = [], Edges = [] });
        result.ShouldHaveValidationErrorFor("Nodes");
    }

    [Fact]
    public void Valid_Request_Has_No_Errors()
    {
        TestValidationResult<CreateTradingSetup.Request> result = _validator.TestValidate(Valid());
        result.ShouldNotHaveAnyValidationErrors();
    }
}

public class CreateTradingSetupHandlerTests
{
    private const int UserId = 5;

    private static (CreateTradingSetup.Handler handler, Mock<DbSet<TradingSetup>> set, Mock<ISetupDbContext> context, Mock<ICacheRepository> cache) Arrange()
    {
        Mock<DbSet<TradingSetup>> set = DbSetMockHelper.CreateMockDbSet(new List<TradingSetup>());
        var context = new Mock<ISetupDbContext>();
        context.Setup(c => c.TradingSetups).Returns(set.Object);
        context.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var cache = new Mock<ICacheRepository>();
        cache.Setup(c => c.RemoveCache(It.IsAny<string>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        return (new CreateTradingSetup.Handler(context.Object, cache.Object), set, context, cache);
    }

    private static CreateTradingSetup.Request Request(int userId) => new(
        "Breakout",
        "desc",
        [
            new TradingSetupNodeDto("start", "start", 0, 0, "Start", null),
            new TradingSetupNodeDto("work", "step", 10, 10, "Do work", null),
            new TradingSetupNodeDto("end", "end", 20, 20, "End", null),
        ],
        [
            new TradingSetupEdgeDto("e1", "start", "work", null),
            new TradingSetupEdgeDto("e2", "work", "end", null),
        ],
        userId);

    [Fact]
    public async Task Handle_Returns_Failure_When_UserId_Invalid()
    {
        (CreateTradingSetup.Handler handler, _, _, _) = Arrange();

        Result<int> result = await handler.Handle(Request(0), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_Adds_Setup_With_Steps_And_Connections_On_Success()
    {
        (CreateTradingSetup.Handler handler, Mock<DbSet<TradingSetup>> set, Mock<ISetupDbContext> context, Mock<ICacheRepository> cache) = Arrange();

        TradingSetup? added = null;
        set.Setup(s => s.AddAsync(It.IsAny<TradingSetup>(), It.IsAny<CancellationToken>()))
            .Callback((TradingSetup s, CancellationToken _) => added = s)
            .Returns((TradingSetup s, CancellationToken _) =>
                ValueTask.FromResult((Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<TradingSetup>)null!));

        Result<int> result = await handler.Handle(Request(UserId), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(added);
        Assert.Equal("Breakout", added!.Name);
        Assert.Equal(SetupStatus.Active, added.Status);
        Assert.Equal(3, added.Steps.Count);
        Assert.Equal(2, added.Connections.Count);
        context.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        cache.Verify(c => c.RemoveCache(CacheKeys.SetupsForUser(UserId), It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class UpdateTradingSetupValidatorTests
{
    private static readonly UpdateTradingSetup.Validator _validator = new();

    private static UpdateTradingSetup.Request Valid() => new(
        1,
        "Breakout",
        "desc",
        [new TradingSetupNodeDto("start", "start", 0, 0, "Start", null), new TradingSetupNodeDto("end", "end", 1, 1, "End", null)],
        [new TradingSetupEdgeDto("e1", "start", "end", null)],
        5);

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Id_Must_Be_Positive(int id)
    {
        TestValidationResult<UpdateTradingSetup.Request> result = _validator.TestValidate(Valid() with { Id = id });
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    [Fact]
    public void Name_Is_Required()
    {
        TestValidationResult<UpdateTradingSetup.Request> result = _validator.TestValidate(Valid() with { Name = "" });
        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Edge_To_Missing_Node_Surfaces_Diagram_Error()
    {
        TestValidationResult<UpdateTradingSetup.Request> result = _validator.TestValidate(
            Valid() with { Edges = [new TradingSetupEdgeDto("e1", "start", "ghost", null)] });
        result.ShouldHaveValidationErrorFor("Edges");
    }

    [Fact]
    public void Valid_Request_Has_No_Errors()
    {
        TestValidationResult<UpdateTradingSetup.Request> result = _validator.TestValidate(Valid());
        result.ShouldNotHaveAnyValidationErrors();
    }
}
