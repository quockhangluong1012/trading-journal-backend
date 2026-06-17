using Microsoft.EntityFrameworkCore.ChangeTracking;
using Moq;
using TradingJournal.Modules.Backtest.Domain;
using TradingJournal.Modules.Backtest.Features.V1.DrawingTemplates;
using TradingJournal.Modules.Backtest.Infrastructure;
using TradingJournal.Tests.Backtest.Helpers;

namespace TradingJournal.Tests.Backtest.Features.V1.DrawingTemplates;

public sealed class DrawingTemplateTests
{
    [Fact]
    public async Task CreateTemplate_PersistsTemplate_ForCurrentUser()
    {
        var context = new Mock<IBacktestDbContext>();
        var dbSet = DbSetMockHelper.CreateMockDbSet(new List<ChartDrawingTemplate>().AsQueryable());
        ChartDrawingTemplate? added = null;

        dbSet.Setup(x => x.AddAsync(It.IsAny<ChartDrawingTemplate>(), It.IsAny<CancellationToken>()))
            .Callback((ChartDrawingTemplate entity, CancellationToken _) => added = entity)
            .Returns(new ValueTask<EntityEntry<ChartDrawingTemplate>>((EntityEntry<ChartDrawingTemplate>)null!));

        context.SetupGet(x => x.ChartDrawingTemplates).Returns(dbSet.Object);
        context.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new CreateDrawingTemplate.Handler(context.Object);
        var result = await handler.Handle(
            new CreateDrawingTemplate.Request(
                "  FVG zone  ",
                """{"strokeColor":"#f97316","strokeWidth":3}""",
                "rectangle",
                "FVG")
            {
                UserId = 42
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("FVG zone", added?.Name);
        Assert.Equal("""{"strokeColor":"#f97316","strokeWidth":3}""", added?.StyleJson);
        Assert.Equal("rectangle", added?.Tool);
        Assert.Equal("FVG", added?.Text);
        context.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetTemplates_ReturnsOnlyCurrentUsersActiveTemplates()
    {
        var templates = new List<ChartDrawingTemplate>
        {
            new()
            {
                Id = 1,
                CreatedBy = 42,
                Name = "Execution",
                StyleJson = """{"strokeColor":"#059669"}""",
                Tool = "trend-line",
                Text = "Entry",
                CreatedDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new()
            {
                Id = 2,
                CreatedBy = 7,
                Name = "Other user",
                StyleJson = """{"strokeColor":"#2563eb"}""",
                CreatedDate = new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc)
            },
            new()
            {
                Id = 3,
                CreatedBy = 42,
                Name = "Deleted",
                StyleJson = """{"strokeColor":"#e11d48"}""",
                IsDisabled = true,
                CreatedDate = new DateTime(2026, 6, 3, 0, 0, 0, DateTimeKind.Utc)
            }
        };

        var context = new Mock<IBacktestDbContext>();
        context.SetupGet(x => x.ChartDrawingTemplates)
            .Returns(DbSetMockHelper.CreateMockDbSet(templates.AsQueryable()).Object);

        var handler = new GetDrawingTemplates.Handler(context.Object);
        var result = await handler.Handle(new GetDrawingTemplates.Request { UserId = 42 }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var template = Assert.Single(result.Value);
        Assert.Equal(1, template.Id);
        Assert.Equal("custom-1", template.ClientId);
        Assert.Equal("Execution", template.Label);
        Assert.Equal("""{"strokeColor":"#059669"}""", template.StyleJson);
        Assert.Equal("trend-line", template.Tool);
        Assert.Equal("Entry", template.Text);
    }

    [Fact]
    public async Task DeleteTemplate_SoftDeletesOnlyCurrentUsersTemplate()
    {
        var owned = new ChartDrawingTemplate
        {
            Id = 5,
            CreatedBy = 42,
            Name = "Supply zone",
            StyleJson = """{"strokeColor":"#e11d48"}"""
        };

        var templates = new List<ChartDrawingTemplate>
        {
            owned,
            new()
            {
                Id = 6,
                CreatedBy = 7,
                Name = "Other user",
                StyleJson = """{"strokeColor":"#2563eb"}"""
            }
        };

        var context = new Mock<IBacktestDbContext>();
        context.SetupGet(x => x.ChartDrawingTemplates)
            .Returns(DbSetMockHelper.CreateMockDbSet(templates.AsQueryable()).Object);
        context.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var handler = new DeleteDrawingTemplate.Handler(context.Object);
        var result = await handler.Handle(new DeleteDrawingTemplate.Request(5) { UserId = 42 }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(owned.IsDisabled);
        context.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
