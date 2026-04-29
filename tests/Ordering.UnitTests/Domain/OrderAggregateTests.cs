namespace eShop.Ordering.UnitTests.Domain;

using eShop.Ordering.Domain.AggregatesModel.OrderAggregate;
using eShop.Ordering.UnitTests.Domain;

[TestClass]
public class OrderAggregateTests
{
    private static Order BuildOrder() =>
        new OrderBuilder(new AddressBuilder().Build()).Build();

    [TestMethod]
    public void Order_construction_sets_submitted_status_and_initial_state()
    {
        var address = new AddressBuilder().Build();
        var cardExpiration = DateTime.UtcNow.AddYears(1);

        var before = DateTime.UtcNow;
        var order = new Order(
            "user-1",
            "userName",
            address,
            cardTypeId: 5,
            cardNumber: "1234",
            cardSecurityNumber: "123",
            cardHolderName: "holder",
            cardExpiration: cardExpiration);
        var after = DateTime.UtcNow;

        Assert.AreEqual(OrderStatus.Submitted, order.OrderStatus);
        Assert.AreSame(address, order.Address);
        Assert.IsTrue(order.OrderDate >= before && order.OrderDate <= after);
        Assert.HasCount(0, order.OrderItems);
        Assert.HasCount(1, order.DomainEvents);
    }

    [TestMethod]
    public void AddOrderItem_on_new_order_adds_single_item_with_given_units()
    {
        var order = BuildOrder();

        order.AddOrderItem(productId: 42, productName: "widget", unitPrice: 10m, discount: 0m, pictureUrl: "pic", units: 3);

        Assert.HasCount(1, order.OrderItems);
        var item = order.OrderItems.Single();
        Assert.AreEqual(42, item.ProductId);
        Assert.AreEqual(3, item.Units);
        Assert.AreEqual(10m, item.UnitPrice);
        Assert.AreEqual(30m, order.GetTotal());
    }

    [TestMethod]
    public void AddOrderItem_with_same_product_increases_quantity_instead_of_duplicating()
    {
        var order = BuildOrder();

        order.AddOrderItem(productId: 1, productName: "cup", unitPrice: 10m, discount: 0m, pictureUrl: "pic", units: 2);
        order.AddOrderItem(productId: 1, productName: "cup", unitPrice: 10m, discount: 0m, pictureUrl: "pic", units: 5);

        Assert.HasCount(1, order.OrderItems);
        Assert.AreEqual(7, order.OrderItems.Single().Units);
        Assert.AreEqual(70m, order.GetTotal());
    }

    [TestMethod]
    public void AddOrderItem_with_higher_discount_for_existing_product_replaces_discount()
    {
        var order = BuildOrder();

        order.AddOrderItem(productId: 1, productName: "cup", unitPrice: 10m, discount: 1m, pictureUrl: "pic", units: 1);
        order.AddOrderItem(productId: 1, productName: "cup", unitPrice: 10m, discount: 5m, pictureUrl: "pic", units: 1);

        var item = order.OrderItems.Single();
        Assert.AreEqual(2, item.Units);
        Assert.AreEqual(5m, item.Discount);
    }

    [TestMethod]
    public void SetCancelledStatus_from_submitted_transitions_to_cancelled()
    {
        var order = BuildOrder();

        order.SetCancelledStatus();

        Assert.AreEqual(OrderStatus.Cancelled, order.OrderStatus);
        Assert.AreEqual("The order was cancelled.", order.Description);
    }

    [TestMethod]
    public void SetCancelledStatus_from_awaiting_validation_transitions_to_cancelled()
    {
        var order = BuildOrder();
        order.SetAwaitingValidationStatus();

        order.SetCancelledStatus();

        Assert.AreEqual(OrderStatus.Cancelled, order.OrderStatus);
    }

    [TestMethod]
    public void SetCancelledStatus_from_paid_throws_domain_exception()
    {
        var order = BuildOrder();
        order.SetAwaitingValidationStatus();
        order.SetStockConfirmedStatus();
        order.SetPaidStatus();

        Assert.ThrowsExactly<OrderingDomainException>(() => order.SetCancelledStatus());
        Assert.AreEqual(OrderStatus.Paid, order.OrderStatus);
    }

    [TestMethod]
    public void SetCancelledStatus_from_shipped_throws_domain_exception()
    {
        var order = BuildOrder();
        order.SetAwaitingValidationStatus();
        order.SetStockConfirmedStatus();
        order.SetPaidStatus();
        order.SetShippedStatus();

        Assert.ThrowsExactly<OrderingDomainException>(() => order.SetCancelledStatus());
        Assert.AreEqual(OrderStatus.Shipped, order.OrderStatus);
    }
}
