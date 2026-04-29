using Microsoft.EntityFrameworkCore;
using eShop.Ordering.Domain.AggregatesModel.BuyerAggregate;
using eShop.Ordering.Infrastructure;
using eShop.Ordering.Infrastructure.Repositories;

namespace eShop.Ordering.UnitTests.Infrastructure;

[TestClass]
public class BuyerRepositoryTests
{
    private static OrderingContext CreateContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<OrderingContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new OrderingContext(options);
    }

    [TestMethod]
    public void Constructor_ThrowsOnNullContext()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => _ = new BuyerRepository(null!));
    }

    [TestMethod]
    public async Task Add_TransientBuyerIsPersisted()
    {
        await using var context = CreateContext();
        var repo = new BuyerRepository(context);
        var buyer = new Buyer(identity: Guid.NewGuid().ToString(), name: "Alice");

        var added = repo.Add(buyer);
        await context.SaveChangesAsync();

        Assert.AreSame(buyer, added);
        Assert.AreNotEqual(0, added.Id);
        Assert.AreEqual(1, await context.Buyers.CountAsync());
    }

    [TestMethod]
    public async Task FindAsync_ReturnsBuyerByIdentity()
    {
        var dbName = Guid.NewGuid().ToString();
        var identity = Guid.NewGuid().ToString();
        int buyerId;

        await using (var seed = CreateContext(dbName))
        {
            var buyer = new Buyer(identity, "Bob");
            seed.Buyers.Add(buyer);
            await seed.SaveChangesAsync();
            buyerId = buyer.Id;
        }

        await using var context = CreateContext(dbName);
        var repo = new BuyerRepository(context);

        var found = await repo.FindAsync(identity);

        Assert.IsNotNull(found);
        Assert.AreEqual(buyerId, found.Id);
        Assert.AreEqual(identity, found.IdentityGuid);
    }

    [TestMethod]
    public async Task FindAsync_ReturnsNullWhenIdentityUnknown()
    {
        await using var context = CreateContext();
        var repo = new BuyerRepository(context);

        var found = await repo.FindAsync("does-not-exist");

        Assert.IsNull(found);
    }

    [TestMethod]
    public async Task FindByIdAsync_ReturnsBuyerById()
    {
        var dbName = Guid.NewGuid().ToString();
        int buyerId;

        await using (var seed = CreateContext(dbName))
        {
            var buyer = new Buyer(Guid.NewGuid().ToString(), "Carol");
            seed.Buyers.Add(buyer);
            await seed.SaveChangesAsync();
            buyerId = buyer.Id;
        }

        await using var context = CreateContext(dbName);
        var repo = new BuyerRepository(context);

        var found = await repo.FindByIdAsync(buyerId);

        Assert.IsNotNull(found);
        Assert.AreEqual(buyerId, found.Id);
        Assert.AreEqual("Carol", found.Name);
    }

    [TestMethod]
    public async Task Update_AttachesAndPersistsModifications()
    {
        var dbName = Guid.NewGuid().ToString();
        var identity = Guid.NewGuid().ToString();
        int buyerId;

        await using (var seed = CreateContext(dbName))
        {
            var buyer = new Buyer(identity, "Initial");
            seed.Buyers.Add(buyer);
            await seed.SaveChangesAsync();
            buyerId = buyer.Id;
        }

        await using var context = CreateContext(dbName);
        var repo = new BuyerRepository(context);
        var existing = await repo.FindByIdAsync(buyerId);
        Assert.IsNotNull(existing);
        existing.VerifyOrAddPaymentMethod(
            cardTypeId: 1,
            alias: "alias",
            cardNumber: "4111111111111111",
            securityNumber: "123",
            cardHolderName: "Holder",
            expiration: DateTime.UtcNow.AddYears(1),
            orderId: 1);

        var updated = repo.Update(existing);
        await context.SaveChangesAsync();

        Assert.AreSame(existing, updated);
        await using var verify = CreateContext(dbName);
        var reloaded = await verify.Buyers.Include(b => b.PaymentMethods).SingleAsync(b => b.Id == buyerId);
        Assert.AreEqual(1, reloaded.PaymentMethods.Count());
    }
}
