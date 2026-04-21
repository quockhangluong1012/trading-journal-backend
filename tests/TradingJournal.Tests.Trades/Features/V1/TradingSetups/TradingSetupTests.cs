using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using FluentValidation.TestHelper;
using Moq;
using TradingJournal.Modules.Trades.Common.Enum;
using TradingJournal.Modules.Trades.Domain;
using TradingJournal.Modules.Trades.Features.V1.TradingSetups;
using TradingJournal.Modules.Trades.Infrastructure;
using TradingJournal.Tests.Trades.Helpers;

namespace TradingJournal.Tests.Trades.Features.V1.TradingSetups;

public class CreateTradingSetupValidatorTests
{
    private static readonly CreateTradingSetup.Validator _validator = new();

    [Fact]
    public void Should_Have_Error_When_Name_Is_Empty()
    {
        var request = new CreateTradingSetup.Request("", "Execution plan", TradingSetupFixture.CreateNodes(), TradingSetupFixture.CreateEdges());

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Should_Have_Error_When_Nodes_Are_Empty()
    {
        var request = new CreateTradingSetup.Request("London breakout", "Execution plan", [], TradingSetupFixture.CreateEdges());

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Nodes);
    }

    [Fact]
    public void Should_Have_Error_When_Edge_References_Missing_Node()
    {
        var request = new CreateTradingSetup.Request(
            "London breakout",
            "Execution plan",
            TradingSetupFixture.CreateNodes(),
            [new TradingSetupEdgeDto("edge-missing", "setup-node-start", "setup-node-missing", "If valid")]);

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Edges);
    }
}

public class CreateTradingSetupHandlerTests
{
    private readonly Mock<ITradeDbContext> _dbMock;
    private readonly CreateTradingSetup.Handler _handler;

    public CreateTradingSetupHandlerTests()
    {
        _dbMock = new Mock<ITradeDbContext>();
        _handler = new CreateTradingSetup.Handler(_dbMock.Object);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_UserId_Is_Zero()
    {
        var request = new CreateTradingSetup.Request(
            "London breakout",
            "Execution plan",
            TradingSetupFixture.CreateNodes(),
            TradingSetupFixture.CreateEdges());

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_Returns_Success_When_Request_Is_Valid()
    {
        _dbMock.Setup(x => x.TradingSetups)
            .Returns(DbSetMockHelper.CreateMockDbSet(new List<TradingSetup>().AsQueryable()).Object);
        _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var request = new CreateTradingSetup.Request(
            "London breakout",
            "Execution plan",
            TradingSetupFixture.CreateNodes(),
            TradingSetupFixture.CreateEdges(),
            7);

        var result = await _handler.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        _dbMock.Verify(x => x.TradingSetups.AddAsync(It.IsAny<TradingSetup>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}

public class GetTradingSetupsHandlerTests
{
    private readonly Mock<ITradeDbContext> _dbMock;
    private readonly GetTradingSetups.Handler _handler;

    public GetTradingSetupsHandlerTests()
    {
        _dbMock = new Mock<ITradeDbContext>();
        _handler = new GetTradingSetups.Handler(_dbMock.Object);
    }

    [Fact]
    public async Task Handle_Filters_Setups_By_UserId()
    {
        DateTime createdAt = new(2026, 4, 21, 13, 30, 0, DateTimeKind.Utc);
        var setups = new List<TradingSetup>
        {
            TradingSetupFixture.CreateEntity(id: 1, createdBy: 7, name: "London breakout", createdDate: createdAt),
            TradingSetupFixture.CreateEntity(id: 2, createdBy: 8, name: "New York reversal"),
        };

        _dbMock.Setup(x => x.TradingSetups)
            .Returns(DbSetMockHelper.CreateMockDbSet(setups.AsQueryable()).Object);

        var result = await _handler.Handle(new GetTradingSetups.Request(7), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal("London breakout", result.Value.Single().Name);
        Assert.Equal(1, result.Value.Single().StepCount);
        Assert.Equal(createdAt, result.Value.Single().CreatedAt);
    }

    [Fact]
    public async Task Handle_Excludes_Disabled_Setups()
    {
        var disabledSetup = TradingSetupFixture.CreateEntity(id: 2, createdBy: 7, name: "Disabled setup");
        disabledSetup.IsDisabled = true;

        var setups = new List<TradingSetup>
        {
            TradingSetupFixture.CreateEntity(id: 1, createdBy: 7, name: "London breakout"),
            disabledSetup,
        };

        _dbMock.Setup(x => x.TradingSetups)
            .Returns(DbSetMockHelper.CreateMockDbSet(setups.AsQueryable()).Object);

        var result = await _handler.Handle(new GetTradingSetups.Request(7), CancellationToken.None);

        Assert.True(result.IsSuccess);
        TradingSetupViewModel setup = Assert.Single(result.Value);
        Assert.Equal(1, setup.Id);
        Assert.Equal("London breakout", setup.Name);
    }
}

public class GetTradingSetupsSqlTranslationTests
{
    [Fact]
    public void BuildQuery_Translates_With_SqlServer_Provider()
    {
        var options = new DbContextOptionsBuilder<TradeDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=TradingJournalTests;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        using var context = new TradeDbContext(options, new HttpContextAccessor());

        string sql = GetTradingSetups.BuildQuery(context.TradingSetups.AsNoTracking(), 7).ToQueryString();

        Assert.Contains("COUNT(", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ORDER BY", sql, StringComparison.OrdinalIgnoreCase);
    }
}

public class GetTradingSetupDetailValidatorTests
{
    private static readonly GetTradingSetupDetail.Validator _validator = new();

    [Fact]
    public void Should_Have_Error_When_Id_Is_Zero()
    {
        var result = _validator.TestValidate(new GetTradingSetupDetail.Request(0, 7));

        result.ShouldHaveValidationErrorFor(x => x.Id);
    }
}

public class GetTradingSetupDetailHandlerTests
{
    private readonly Mock<ITradeDbContext> _dbMock;
    private readonly GetTradingSetupDetail.Handler _handler;

    public GetTradingSetupDetailHandlerTests()
    {
        _dbMock = new Mock<ITradeDbContext>();
        _handler = new GetTradingSetupDetail.Handler(_dbMock.Object);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_Setup_Does_Not_Belong_To_User()
    {
        var setups = new List<TradingSetup>
        {
            TradingSetupFixture.CreateEntity(id: 1, createdBy: 9),
        };

        _dbMock.Setup(x => x.TradingSetups)
            .Returns(DbSetMockHelper.CreateMockDbSet(setups.AsQueryable()).Object);

        var result = await _handler.Handle(new GetTradingSetupDetail.Request(1, 7), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_Returns_Detail_With_Diagram_When_Setup_Is_Found()
    {
        DateTime createdAt = new(2026, 4, 21, 13, 30, 0, DateTimeKind.Utc);
        var setups = new List<TradingSetup>
        {
            TradingSetupFixture.CreateEntity(id: 1, createdBy: 7, createdDate: createdAt),
        };

        _dbMock.Setup(x => x.TradingSetups)
            .Returns(DbSetMockHelper.CreateMockDbSet(setups.AsQueryable()).Object);

        var result = await _handler.Handle(new GetTradingSetupDetail.Request(1, 7), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Nodes.Count);
        Assert.Equal(2, result.Value.Edges.Count);
        Assert.Equal(createdAt, result.Value.CreatedAt);
    }
}

public class UpdateTradingSetupValidatorTests
{
    private static readonly UpdateTradingSetup.Validator _validator = new();

    [Fact]
    public void Should_Have_Error_When_Id_Is_Zero()
    {
        var result = _validator.TestValidate(new UpdateTradingSetup.Request(0, "Updated", "", TradingSetupFixture.CreateNodes(), TradingSetupFixture.CreateEdges(), 7));

        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    [Fact]
    public void Should_Have_Error_When_Edge_References_Missing_Node()
    {
        var request = new UpdateTradingSetup.Request(
            1,
            "Updated",
            "",
            TradingSetupFixture.CreateNodes(),
            [new TradingSetupEdgeDto("edge-missing", "setup-node-start", "setup-node-missing", null)],
            7);

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Edges);
    }
}

public class UpdateTradingSetupHandlerTests
{
    private readonly Mock<ITradeDbContext> _dbMock;
    private readonly UpdateTradingSetup.Handler _handler;

    public UpdateTradingSetupHandlerTests()
    {
        _dbMock = new Mock<ITradeDbContext>();
        _handler = new UpdateTradingSetup.Handler(_dbMock.Object);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_Setup_Does_Not_Exist()
    {
        _dbMock.Setup(x => x.TradingSetups)
            .Returns(DbSetMockHelper.CreateMockDbSet(new List<TradingSetup>().AsQueryable()).Object);

        var result = await _handler.Handle(
            new UpdateTradingSetup.Request(99, "Updated", "Execution plan", TradingSetupFixture.CreateNodes(), TradingSetupFixture.CreateEdges(), 7),
            CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_Updates_Setup_When_Request_Is_Valid()
    {
        var setup = TradingSetupFixture.CreateEntity(id: 1, createdBy: 7, name: "Old name");
        var stepsDbSet = DbSetMockHelper.CreateMockDbSet(setup.Steps.AsQueryable());
        var connectionsDbSet = DbSetMockHelper.CreateMockDbSet(setup.Connections.AsQueryable());

        _dbMock.Setup(x => x.TradingSetups)
            .Returns(DbSetMockHelper.CreateMockDbSet(new List<TradingSetup> { setup }.AsQueryable()).Object);
        _dbMock.Setup(x => x.SetupSteps).Returns(stepsDbSet.Object);
        _dbMock.Setup(x => x.SetupConnections).Returns(connectionsDbSet.Object);
        _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var updatedNodes = TradingSetupFixture.CreateNodes();
        updatedNodes.Add(new TradingSetupNodeDto("setup-node-second-step", "step", 700, 200, "Scale in", "Confirm momentum"));

        var updatedEdges = TradingSetupFixture.CreateEdges();
        updatedEdges.Add(new TradingSetupEdgeDto("edge-third", "setup-node-step", "setup-node-second-step", "Momentum confirms"));

        var result = await _handler.Handle(
            new UpdateTradingSetup.Request(1, "Updated name", "Execution plan", updatedNodes, updatedEdges, 7),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Updated name", setup.Name);
        Assert.Equal(2, setup.Steps.Count(step => step.NodeType is not "start" and not "end"));
    }
}

public class DeleteTradingSetupValidatorTests
{
    private static readonly DeleteTradingSetup.Validator _validator = new();

    [Fact]
    public void Should_Have_Error_When_Id_Is_Zero()
    {
        var result = _validator.TestValidate(new DeleteTradingSetup.Request(0, 7));

        result.ShouldHaveValidationErrorFor(x => x.Id);
    }
}

public class DeleteTradingSetupHandlerTests
{
    private readonly Mock<ITradeDbContext> _dbMock;
    private readonly DeleteTradingSetup.Handler _handler;

    public DeleteTradingSetupHandlerTests()
    {
        _dbMock = new Mock<ITradeDbContext>();
        _handler = new DeleteTradingSetup.Handler(_dbMock.Object);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_Setup_Does_Not_Exist()
    {
        _dbMock.Setup(x => x.TradingSetups)
            .Returns(DbSetMockHelper.CreateMockDbSet(new List<TradingSetup>().AsQueryable()).Object);

        var result = await _handler.Handle(new DeleteTradingSetup.Request(1, 7), CancellationToken.None);

        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task Handle_Removes_Setup_When_It_Belongs_To_User()
    {
        var setup = TradingSetupFixture.CreateEntity(id: 1, createdBy: 7);
        var dbSetMock = DbSetMockHelper.CreateMockDbSet(new List<TradingSetup> { setup }.AsQueryable());

        _dbMock.Setup(x => x.TradingSetups).Returns(dbSetMock.Object);
        _dbMock.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _handler.Handle(new DeleteTradingSetup.Request(1, 7), CancellationToken.None);

        Assert.True(result.IsSuccess);
        dbSetMock.Verify(x => x.Remove(It.Is<TradingSetup>(entity => entity.Id == 1)), Times.Once);
    }
}

internal static class TradingSetupFixture
{
    public static List<TradingSetupNodeDto> CreateNodes() =>
    [
        new("setup-node-start", "start", 100, 220, "Start", "Prepare for the session"),
        new("setup-node-step", "step", 380, 220, "Validate context", "Check bias, liquidity, and timing"),
        new("setup-node-end", "end", 660, 220, "Execute or stand aside", "Commit only if all conditions align"),
    ];

    public static List<TradingSetupEdgeDto> CreateEdges() =>
    [
        new("edge-first", "setup-node-start", "setup-node-step", null),
        new("edge-second", "setup-node-step", "setup-node-end", "All conditions align"),
    ];

    public static TradingSetup CreateEntity(
        int id = 1,
        int createdBy = 1,
        string name = "London breakout",
        string? description = "Execution plan",
        DateTime? createdDate = null)
    {
        var nodes = CreateNodes();
        var edges = CreateEdges();
        var steps = nodes.Select((node, index) => new SetupStep
        {
            Id = index + 1,
            StepNumber = index + 1,
            Label = node.Title,
            Description = node.Notes,
            NodeType = node.Kind,
            PositionX = node.X,
            PositionY = node.Y,
            Color = null,
            CreatedBy = createdBy,
        }).ToList();

        var stepIdByNodeId = nodes
            .Zip(steps, (node, step) => new { NodeId = node.Id, StepId = step.Id })
            .ToDictionary(item => item.NodeId, item => item.StepId);

        var connections = edges.Select((edge, index) => new SetupConnection
        {
            Id = index + 1,
            SourceStepId = stepIdByNodeId[edge.Source],
            TargetStepId = stepIdByNodeId[edge.Target],
            Label = edge.Label,
            IsAnimated = false,
            Color = null,
            CreatedBy = createdBy,
        }).ToList();

        return new TradingSetup
        {
            Id = id,
            Name = name,
            Model = "flowchart",
            Description = description,
            Notes = null,
            Status = SetupStatus.Active,
            CreatedDate = createdDate ?? new DateTime(2026, 4, 21, 13, 30, 0, DateTimeKind.Utc),
            CreatedBy = createdBy,
            Steps = steps,
            Connections = connections,
        };
    }
}