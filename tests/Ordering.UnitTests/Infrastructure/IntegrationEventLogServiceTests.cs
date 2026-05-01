using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using eShop.EventBus.Events;
using eShop.IntegrationEventLogEF;
using eShop.IntegrationEventLogEF.Services;

namespace eShop.Ordering.UnitTests.Infrastructure;

[TestClass]
public class IntegrationEventLogServiceTests
{
    public class TestIntegrationEventLogContext : DbContext
    {
        public TestIntegrationEventLogContext(DbContextOptions<TestIntegrationEventLogContext> options)
            : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.UseIntegrationEventLogs();
        }
    }

    public record TestSampleIntegrationEvent(string Payload) : IntegrationEvent;

    private static (SqliteConnection Connection, TestIntegrationEventLogContext Context) NewContext()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<TestIntegrationEventLogContext>()
            .UseSqlite(connection)
            .Options;
        var context = new TestIntegrationEventLogContext(options);
        context.Database.EnsureCreated();
        return (connection, context);
    }

    [TestMethod]
    public async Task SaveEventAsync_PersistsEntryWithNotPublishedState()
    {
        var (connection, context) = NewContext();
        try
        {
            var service = new IntegrationEventLogService<TestIntegrationEventLogContext>(context);
            var integrationEvent = new TestSampleIntegrationEvent("hello");

            await using (var transaction = await context.Database.BeginTransactionAsync())
            {
                await service.SaveEventAsync(integrationEvent, transaction);
                await transaction.CommitAsync();
            }

            var entry = await context.Set<IntegrationEventLogEntry>().SingleAsync();
            Assert.AreEqual(integrationEvent.Id, entry.EventId);
            Assert.AreEqual(EventStateEnum.NotPublished, entry.State);
            Assert.AreEqual(0, entry.TimesSent);
            Assert.IsTrue(entry.EventTypeName.Contains(nameof(TestSampleIntegrationEvent)));
            Assert.IsTrue(entry.Content.Contains("hello"));
        }
        finally
        {
            context.Dispose();
            connection.Dispose();
        }
    }

    [TestMethod]
    public async Task SaveEventAsync_ThrowsWhenTransactionIsNull()
    {
        var (connection, context) = NewContext();
        try
        {
            var service = new IntegrationEventLogService<TestIntegrationEventLogContext>(context);
            var integrationEvent = new TestSampleIntegrationEvent("payload");

            await Assert.ThrowsExactlyAsync<ArgumentNullException>(
                () => service.SaveEventAsync(integrationEvent, null!));
        }
        finally
        {
            context.Dispose();
            connection.Dispose();
        }
    }

    [TestMethod]
    public async Task MarkEventAsInProgressAsync_SetsInProgressAndIncrementsTimesSent()
    {
        var (connection, context) = NewContext();
        try
        {
            var service = new IntegrationEventLogService<TestIntegrationEventLogContext>(context);
            var integrationEvent = new TestSampleIntegrationEvent("p");

            await using (var tx = await context.Database.BeginTransactionAsync())
            {
                await service.SaveEventAsync(integrationEvent, tx);
                await tx.CommitAsync();
            }

            await service.MarkEventAsInProgressAsync(integrationEvent.Id);

            var entry = await context.Set<IntegrationEventLogEntry>().SingleAsync(e => e.EventId == integrationEvent.Id);
            Assert.AreEqual(EventStateEnum.InProgress, entry.State);
            Assert.AreEqual(1, entry.TimesSent);

            await service.MarkEventAsInProgressAsync(integrationEvent.Id);
            await context.Entry(entry).ReloadAsync();
            Assert.AreEqual(2, entry.TimesSent);
        }
        finally
        {
            context.Dispose();
            connection.Dispose();
        }
    }

    [TestMethod]
    public async Task MarkEventAsPublishedAsync_TransitionsStateToPublished()
    {
        var (connection, context) = NewContext();
        try
        {
            var service = new IntegrationEventLogService<TestIntegrationEventLogContext>(context);
            var integrationEvent = new TestSampleIntegrationEvent("p");

            await using (var tx = await context.Database.BeginTransactionAsync())
            {
                await service.SaveEventAsync(integrationEvent, tx);
                await tx.CommitAsync();
            }

            await service.MarkEventAsInProgressAsync(integrationEvent.Id);
            await service.MarkEventAsPublishedAsync(integrationEvent.Id);

            var entry = await context.Set<IntegrationEventLogEntry>().SingleAsync(e => e.EventId == integrationEvent.Id);
            Assert.AreEqual(EventStateEnum.Published, entry.State);
            Assert.AreEqual(1, entry.TimesSent);
        }
        finally
        {
            context.Dispose();
            connection.Dispose();
        }
    }
}
