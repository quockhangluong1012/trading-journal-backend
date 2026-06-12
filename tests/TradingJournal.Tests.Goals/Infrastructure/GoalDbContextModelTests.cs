using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Metadata;

namespace TradingJournal.Tests.Goals.Infrastructure;

public sealed class GoalDbContextModelTests
{
    private const string ConnectionString =
        "Server=(localdb)\\mssqllocaldb;Database=GoalModelTests;Trusted_Connection=True;TrustServerCertificate=True;";

    [Theory]
    [InlineData(typeof(Goal), nameof(Goal.StartValue))]
    [InlineData(typeof(Goal), nameof(Goal.CurrentValue))]
    [InlineData(typeof(Goal), nameof(Goal.TargetValue))]
    [InlineData(typeof(GoalMilestone), nameof(GoalMilestone.CurrentValue))]
    [InlineData(typeof(GoalTask), nameof(GoalTask.CurrentValue))]
    [InlineData(typeof(GoalProgressEntry), nameof(GoalProgressEntry.CurrentValue))]
    public void MetricValues_UseDecimal18_4(Type entityType, string propertyName)
    {
        IProperty property = GetContext().Model.FindEntityType(entityType)!.FindProperty(propertyName)!;

        Assert.Equal("decimal(18,4)", property.GetColumnType());
        Assert.Equal(18, property.GetPrecision());
        Assert.Equal(4, property.GetScale());
    }

    [Theory]
    [InlineData(typeof(Goal), "Goals")]
    [InlineData(typeof(GoalMilestone), "GoalMilestones")]
    [InlineData(typeof(GoalTask), "GoalTasks")]
    [InlineData(typeof(GoalProgressEntry), "GoalProgressEntries")]
    public void Entities_UseGoalsSchema(Type entityType, string tableName)
    {
        IEntityType metadata = GetContext().Model.FindEntityType(entityType)!;

        Assert.Equal(tableName, metadata.GetTableName());
        Assert.Equal("Goals", metadata.GetSchema());
    }

    [Fact]
    public void MilestoneTaskRelationship_DoesNotCreateSecondCascadePath()
    {
        IEntityType task = GetContext().Model.FindEntityType(typeof(GoalTask))!;
        IForeignKey milestoneForeignKey = task.GetForeignKeys()
            .Single(key => key.PrincipalEntityType.ClrType == typeof(GoalMilestone));

        Assert.Equal(DeleteBehavior.NoAction, milestoneForeignKey.DeleteBehavior);
    }

    private static GoalDbContext GetContext()
    {
        DbContextOptions<GoalDbContext> options = new DbContextOptionsBuilder<GoalDbContext>()
            .UseSqlServer(ConnectionString)
            .Options;

        return new GoalDbContext(options, new HttpContextAccessor());
    }
}
