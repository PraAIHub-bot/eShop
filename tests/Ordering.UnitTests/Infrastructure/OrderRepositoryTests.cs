using Microsoft.EntityFrameworkCore;
using eShop.Ordering.Domain.AggregatesModel.OrderAggregate;
using eShop.Ordering.Infrastructure;
using eShop.Ordering.Infrastructure.Repositories;

namespace eShop.Ordering.UnitTests.Infrastructure;

[TestClass]
public class OrderRepositoryTests
{
    private static OrderingContext CreateContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<OrderingContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new OrderingContext(options);
    }

    private static Order BuildOrder()
    {
        var address = new Address("street", "city", "state", "country", "zipcode");
        return new Order(
            userId: "userId",
            userName: "userName",
            address: address,
            cardTypeId: 1,
            cardNumber: "4111111111111111",
            cardSecurityNumber: "123",
            cardHolderName: "Card Holder",
            cardExpiration: DateTime.UtcNow.AddYears(1));
    }

    [TestMethod]
    public void Constructor_ThrowsOnNullContext()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => _ = new OrderRepository(null!));
    }

    [TestMethod]
    public async Task Add_PersistsOrderAndAssignsId()
    {
        await using var context = CreateContext();
        var repo = new OrderRepository(context);
        var order = BuildOrder();

        var added = repo.Add(order);
        await context.SaveChangesAsync();

        Assert.AreSame(order, added);
        Assert.AreNotEqual(0, added.Id);
        Assert.AreEqual(1, await context.Orders.CountAsync());
    }

    [TestMethod]
    public async Task GetAsync_ReturnsTrackedOrderWithItems()
    {
        var dbName = Guid.NewGuid().ToString();

        int orderId;
        await using (var seedContext = CreateContext(dbName))
        {
            var order = BuildOrder();
            order.AddOrderItem(productId: 42, productName: "Widget", unitPrice: 9.99m, discount: 0m, pictureUrl: null!, units: 2);
            seedContext.Orders.Add(order);
            await seedContext.SaveChangesAsync();
            orderId = order.Id;
        }

        await using var context = CreateContext(dbName);
        var repo = new OrderRepository(context);

        var found = await repo.GetAsync(orderId);

        Assert.IsNotNull(found);
        Assert.AreEqual(orderId, found.Id);
        Assert.AreEqual(1, found.OrderItems.Count);
        Assert.AreEqual(42, found.OrderItems.Single().ProductId);
    }

    [TestMethod]
    public async Task GetAsync_ReturnsNullWhenOrderNotFound()
    {
        await using var context = CreateContext();
        var repo = new OrderRepository(context);

        var found = await repo.GetAsync(orderId: 9999);

        Assert.IsNull(found);
    }

    [TestMethod]
    public async Task Update_MarksOrderAsModified()
    {
        await using var context = CreateContext();
        var repo = new OrderRepository(context);
        var order = BuildOrder();
        repo.Add(order);
        await context.SaveChangesAsync();

        order.SetAwaitingValidationStatus();
        repo.Update(order);

        Assert.AreEqual(EntityState.Modified, context.Entry(order).State);
        var saved = await context.SaveChangesAsync();
        Assert.AreEqual(1, saved);
    }

    [TestMethod]
    public void UnitOfWork_ReturnsUnderlyingContext()
    {
        using var context = CreateContext();
        var repo = new OrderRepository(context);

        Assert.AreSame(context, repo.UnitOfWork);
    }
}
