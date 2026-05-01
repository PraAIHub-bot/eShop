using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using eShop.Ordering.API.Application.Queries;
using eShop.Ordering.Domain.AggregatesModel.OrderAggregate;
using eShop.Ordering.Infrastructure;
using eShop.Ordering.Infrastructure.Repositories;

namespace eShop.Ordering.UnitTests.Infrastructure;

[TestClass]
public class DiRegistrationTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();

        services.AddDbContext<OrderingContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString())
                   .ConfigureWarnings(w => w.Ignore(
                       Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)));

        services.AddScoped<IOrderQueries, OrderQueries>();
        services.AddScoped<IBuyerRepository, BuyerRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IRequestManager, RequestManager>();

        return services.BuildServiceProvider(validateScopes: true);
    }

    [TestMethod]
    public void BuyerRepository_resolves_as_scoped_via_di()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var repo = scope.ServiceProvider.GetRequiredService<IBuyerRepository>();

        Assert.IsInstanceOfType(repo, typeof(BuyerRepository));
    }

    [TestMethod]
    public void OrderRepository_resolves_as_scoped_via_di()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var repo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();

        Assert.IsInstanceOfType(repo, typeof(OrderRepository));
    }

    [TestMethod]
    public void RequestManager_resolves_as_scoped_via_di()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var manager = scope.ServiceProvider.GetRequiredService<IRequestManager>();

        Assert.IsInstanceOfType(manager, typeof(RequestManager));
    }

    [TestMethod]
    public void OrderQueries_resolves_as_scoped_via_di()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var queries = scope.ServiceProvider.GetRequiredService<IOrderQueries>();

        Assert.IsInstanceOfType(queries, typeof(OrderQueries));
    }

    [TestMethod]
    public void OrderingContext_resolves_as_scoped_via_di()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<OrderingContext>();

        Assert.IsNotNull(context);
    }

    [TestMethod]
    public void Repository_unit_of_work_is_the_resolved_dbcontext()
    {
        using var provider = BuildProvider();
        using var scope = provider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<OrderingContext>();
        var orderRepo = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
        var buyerRepo = scope.ServiceProvider.GetRequiredService<IBuyerRepository>();

        Assert.AreSame(context, orderRepo.UnitOfWork);
        Assert.AreSame(context, buyerRepo.UnitOfWork);
    }
}
