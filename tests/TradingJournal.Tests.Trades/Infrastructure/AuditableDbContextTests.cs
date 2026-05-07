using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TradingJournal.Shared.Abstractions;
using TradingJournal.Shared.Audit;
using TradingJournal.Shared.Infrastructure;

namespace TradingJournal.Tests.Trades.Infrastructure;

public sealed class AuditableDbContextTests
{
    [Fact]
    public async Task SaveChangesAsync_PersistsAuditWithGeneratedEntityId_BeforeReturning()
    {
        // Arrange
        DelayedAuditLogStore auditLogStore = new(TimeSpan.FromMilliseconds(100));
        using ServiceProvider services = new ServiceCollection()
            .AddSingleton<IAuditLogStore>(auditLogStore)
            .BuildServiceProvider();

        IHttpContextAccessor httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                RequestServices = services
            }
        };

        DbContextOptions<TestAuditableDbContext> options = new DbContextOptionsBuilder<TestAuditableDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using TestAuditableDbContext context = new(options, httpContextAccessor);
        TestAuditableEntity entity = new() { Name = "audit-test" };
        context.Entities.Add(entity);

        // Act
        await context.SaveChangesAsync();

        // Assert
        Assert.True(entity.Id > 0);
        Assert.Single(auditLogStore.SavedEntries);
        Assert.Equal(entity.Id.ToString(), auditLogStore.SavedEntries[0].EntityId);
        Assert.Equal("Create", auditLogStore.SavedEntries[0].Action);
    }

    [Fact]
    public async Task SaveChangesAsync_PersistsDeleteAuditWithExistingEntityId()
    {
        // Arrange
        DelayedAuditLogStore auditLogStore = new(TimeSpan.FromMilliseconds(50));
        using ServiceProvider services = new ServiceCollection()
            .AddSingleton<IAuditLogStore>(auditLogStore)
            .BuildServiceProvider();

        IHttpContextAccessor httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                RequestServices = services
            }
        };

        DbContextOptions<TestAuditableDbContext> options = new DbContextOptionsBuilder<TestAuditableDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using TestAuditableDbContext context = new(options, httpContextAccessor);
        TestAuditableEntity entity = new() { Name = "delete-audit-test" };
        context.Entities.Add(entity);
        await context.SaveChangesAsync();
        auditLogStore.Reset();

        // Act
        context.Entities.Remove(entity);
        await context.SaveChangesAsync();

        // Assert
        Assert.Single(auditLogStore.SavedEntries);
        Assert.Equal(entity.Id.ToString(), auditLogStore.SavedEntries[0].EntityId);
        Assert.Equal("Delete", auditLogStore.SavedEntries[0].Action);
    }

    private sealed class TestAuditableDbContext(
        DbContextOptions<TestAuditableDbContext> options,
        IHttpContextAccessor httpContextAccessor)
        : AuditableDbContext(options, httpContextAccessor)
    {
        public DbSet<TestAuditableEntity> Entities => Set<TestAuditableEntity>();
    }

    private sealed class TestAuditableEntity : EntityBase<int>
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class DelayedAuditLogStore(TimeSpan delay) : IAuditLogStore
    {
        public List<AuditEntry> SavedEntries { get; } = [];

        public void Reset()
        {
            SavedEntries.Clear();
        }

        public async Task SaveAsync(IReadOnlyList<AuditEntry> entries, CancellationToken ct = default)
        {
            await Task.Delay(delay, ct);
            SavedEntries.AddRange(entries);
        }

        public Task<IReadOnlyList<AuditEntry>> QueryAsync(
            string? entityName,
            string? entityId,
            DateTime? from,
            DateTime? to,
            int maxResults = 100,
            CancellationToken ct = default)
        {
            return Task.FromResult<IReadOnlyList<AuditEntry>>(SavedEntries);
        }
    }
}