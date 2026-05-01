# Ordering.Domain Prune Audit (TICKET-005)

**Date:** 2026-04-29
**Scope:** `src/Ordering.Domain/Events/*.cs` and `src/Ordering.Domain/AggregatesModel/**/*.cs`
**Goal:** Identify domain events / aggregate methods / entities with zero callers in the
`src/` tree and delete them.

## Summary

| Bucket | Files considered | Deleted | Kept |
|---|---|---|---|
| Events (`src/Ordering.Domain/Events`) | 7 | 0 | 7 |
| Aggregates (`src/Ordering.Domain/AggregatesModel`) | 9 | 0 | 9 |
| Exceptions / SeedWork | 7 | 0 | 7 |
| **Total** | **23** | **0** | **23** |

**Result: zero files deleted.** Every event, aggregate, and SeedWork type in
`src/Ordering.Domain/` is reachable from `src/Ordering.API` or `src/Ordering.Infrastructure`.
The original analysis finding (`unwired/Ordering.Domain potential unused domain events
or entities`) was a false positive at the time of this audit.

The audit method, evidence per file, and one open code-quality follow-up are below.

## Method

For each candidate type `T` in `src/Ordering.Domain/`, ran:

```sh
grep -rn "T" src/
```

A type was a deletion candidate only if **no** match was found outside its own defining file.
For domain events specifically, the gate per the ticket was:

* zero `AddDomainEvent(new T(...))` callers, AND
* zero `INotificationHandler<T>` handlers.

When *either* a raiser or a handler existed, the event was kept (a missing handler is a
re-wiring concern, which is explicitly out of scope for this ticket).

## Domain Events — `src/Ordering.Domain/Events/*.cs`

All seven events have **both** an `AddDomainEvent` raiser in an aggregate **and** a
matching `INotificationHandler<T>` in `src/Ordering.API/Application/DomainEventHandlers/`.

### 1. `BuyerPaymentMethodVerifiedDomainEvent.cs`

The class declared in this file is actually named **`BuyerAndPaymentMethodVerifiedDomainEvent`**
(the filename is stale and disagrees with the class). Searching the actual class name:

```
$ grep -rn "BuyerAndPaymentMethodVerifiedDomainEvent" src/
src/Ordering.Domain/AggregatesModel/BuyerAggregate/Buyer.cs:38:    AddDomainEvent(new BuyerAndPaymentMethodVerifiedDomainEvent(this, existingPayment, orderId));
src/Ordering.Domain/AggregatesModel/BuyerAggregate/Buyer.cs:47:    AddDomainEvent(new BuyerAndPaymentMethodVerifiedDomainEvent(this, payment, orderId));
src/Ordering.API/Application/DomainEventHandlers/UpdateOrderWhenBuyerAndPaymentMethodVerifiedDomainEventHandler.cs:3:    public class UpdateOrderWhenBuyerAndPaymentMethodVerifiedDomainEventHandler : INotificationHandler<BuyerAndPaymentMethodVerifiedDomainEvent>
src/Ordering.API/Application/DomainEventHandlers/UpdateOrderWhenBuyerAndPaymentMethodVerifiedDomainEventHandler.cs:19:    public async Task Handle(BuyerAndPaymentMethodVerifiedDomainEvent domainEvent, CancellationToken cancellationToken)
```

Verdict: **kept** — 2 raisers + 1 handler.

> Code-quality follow-up (out of scope for this ticket): the file name
> `BuyerPaymentMethodVerifiedDomainEvent.cs` should be renamed to
> `BuyerAndPaymentMethodVerifiedDomainEvent.cs` to match the class name. This is a rename,
> not a deletion, and so does not belong in TICKET-005.

### 2. `OrderCancelledDomainEvent.cs`

```
$ grep -rn "OrderCancelledDomainEvent" src/
src/Ordering.Domain/AggregatesModel/OrderAggregate/Order.cs:152:    AddDomainEvent(new OrderCancelledDomainEvent(this));
src/Ordering.API/Application/DomainEventHandlers/OrderCancelledDomainEventHandler.cs:4: : INotificationHandler<OrderCancelledDomainEvent>
src/Ordering.API/Application/DomainEventHandlers/OrderCancelledDomainEventHandler.cs:23: public async Task Handle(OrderCancelledDomainEvent domainEvent, CancellationToken cancellationToken)
```

Verdict: **kept** — 1 raiser + 1 handler.

### 3. `OrderShippedDomainEvent.cs`

```
$ grep -rn "OrderShippedDomainEvent" src/
src/Ordering.Domain/AggregatesModel/OrderAggregate/Order.cs:139:    AddDomainEvent(new OrderShippedDomainEvent(this));
src/Ordering.API/Application/DomainEventHandlers/OrderShippedDomainEventHandler.cs:4: : INotificationHandler<OrderShippedDomainEvent>
src/Ordering.API/Application/DomainEventHandlers/OrderShippedDomainEventHandler.cs:23: public async Task Handle(OrderShippedDomainEvent domainEvent, CancellationToken cancellationToken)
```

Verdict: **kept** — 1 raiser + 1 handler.

### 4. `OrderStartedDomainEvent.cs`

```
$ grep -rn "OrderStartedDomainEvent" src/
src/Ordering.Domain/AggregatesModel/OrderAggregate/Order.cs:170: private void AddOrderStartedDomainEvent(...)
src/Ordering.Domain/AggregatesModel/OrderAggregate/Order.cs:173:    var orderStartedDomainEvent = new OrderStartedDomainEvent(this, userId, userName, ...);
src/Ordering.API/Application/DomainEventHandlers/ValidateOrAddBuyerAggregateWhenOrderStartedDomainEventHandler.cs:4: : INotificationHandler<OrderStartedDomainEvent>
src/Ordering.API/Application/DomainEventHandlers/ValidateOrAddBuyerAggregateWhenOrderStartedDomainEventHandler.cs:20: public async Task Handle(OrderStartedDomainEvent domainEvent, CancellationToken cancellationToken)
```

Verdict: **kept** — raised via the private helper `AddOrderStartedDomainEvent` (called from
`Order`'s constructor) + 1 handler.

### 5. `OrderStatusChangedToAwaitingValidationDomainEvent.cs`

```
$ grep -rn "OrderStatusChangedToAwaitingValidationDomainEvent" src/
src/Ordering.Domain/AggregatesModel/OrderAggregate/Order.cs:103:    AddDomainEvent(new OrderStatusChangedToAwaitingValidationDomainEvent(Id, _orderItems));
src/Ordering.API/Application/DomainEventHandlers/OrderStatusChangedToAwaitingValidationDomainEventHandler.cs:4: : INotificationHandler<OrderStatusChangedToAwaitingValidationDomainEvent>
src/Ordering.API/Application/DomainEventHandlers/OrderStatusChangedToAwaitingValidationDomainEventHandler.cs:23: public async Task Handle(OrderStatusChangedToAwaitingValidationDomainEvent domainEvent, CancellationToken cancellationToken)
```

Verdict: **kept** — 1 raiser + 1 handler.

### 6. `OrderStatusChangedToPaidDomainEvent.cs`

```
$ grep -rn "OrderStatusChangedToPaidDomainEvent" src/
src/Ordering.Domain/AggregatesModel/OrderAggregate/Order.cs:123:    AddDomainEvent(new OrderStatusChangedToPaidDomainEvent(Id, OrderItems));
src/Ordering.API/Application/DomainEventHandlers/OrderStatusChangedToPaidDomainEventHandler.cs:3: public class OrderStatusChangedToPaidDomainEventHandler : INotificationHandler<OrderStatusChangedToPaidDomainEvent>
src/Ordering.API/Application/DomainEventHandlers/OrderStatusChangedToPaidDomainEventHandler.cs:22: public async Task Handle(OrderStatusChangedToPaidDomainEvent domainEvent, CancellationToken cancellationToken)
```

Verdict: **kept** — 1 raiser + 1 handler.

### 7. `OrderStatusChangedToStockConfirmedDomainEvent.cs`

```
$ grep -rn "OrderStatusChangedToStockConfirmedDomainEvent" src/
src/Ordering.Domain/AggregatesModel/OrderAggregate/Order.cs:112:    AddDomainEvent(new OrderStatusChangedToStockConfirmedDomainEvent(Id));
src/Ordering.API/Application/DomainEventHandlers/OrderStatusChangedToStockConfirmedDomainEventHandler.cs:4: : INotificationHandler<OrderStatusChangedToStockConfirmedDomainEvent>
src/Ordering.API/Application/DomainEventHandlers/OrderStatusChangedToStockConfirmedDomainEventHandler.cs:23: public async Task Handle(OrderStatusChangedToStockConfirmedDomainEvent domainEvent, CancellationToken cancellationToken)
```

Verdict: **kept** — 1 raiser + 1 handler.

## Aggregates — `src/Ordering.Domain/AggregatesModel/**/*.cs`

### OrderAggregate

| Type / member | Where it is used | Verdict |
|---|---|---|
| `Order` (ctor) | `src/Ordering.API/Application/Commands/CreateOrderCommandHandler.cs:40` | kept |
| `Order.NewDraft()` | `src/Ordering.API/Application/Commands/CreateOrderDraftCommandHandler.cs:12` | kept |
| `Order.AddOrderItem(...)` | `CreateOrderCommandHandler.cs:44`, `CreateOrderDraftCommandHandler.cs:16` | kept |
| `Order.SetPaymentMethodVerified(...)` | `UpdateOrderWhenBuyerAndPaymentMethodVerifiedDomainEventHandler.cs:22` | kept |
| `Order.SetAwaitingValidationStatus()` | `SetAwaitingValidationOrderStatusCommandHandler.cs:27` | kept |
| `Order.SetStockConfirmedStatus()` | `SetStockConfirmedOrderStatusCommandHandler.cs:30` | kept |
| `Order.SetPaidStatus()` | `SetPaidOrderStatusCommandHandler.cs:30` | kept |
| `Order.SetShippedStatus()` | `ShipOrderCommandHandler.cs:27` | kept |
| `Order.SetCancelledStatus()` | `CancelOrderCommandHandler.cs:27` | kept |
| `Order.SetCancelledStatusWhenStockIsRejected(...)` | `SetStockRejectedOrderStatusCommandHandler.cs:30` | kept |
| `Order.GetTotal()` | `OrderQueries.cs:26`, `CreateOrderDraftCommandHandler.cs:41` | kept |
| `OrderItem` (ctor) | `Order.cs:88` (inside `AddOrderItem`) | kept |
| `OrderItem.SetNewDiscount(...)` | `Order.cs:80` | kept |
| `OrderItem.AddUnits(...)` | `Order.cs:83` | kept |
| `OrderStatus` (enum) | `OrderStatusChanged*IntegrationEvent.cs`, `Order.cs`, EF migrations | kept |
| `Address` (ctor + props) | `CreateOrderCommandHandler.cs:39`, `OrderQueries.cs:20-24`, EF migrations | kept |
| `IOrderRepository` | All `*OrderStatusCommandHandler` classes; `OrderRepository.cs` impl | kept |

### BuyerAggregate

| Type / member | Where it is used | Verdict |
|---|---|---|
| `Buyer` (ctor) | `ValidateOrAddBuyerAggregateWhenOrderStartedDomainEventHandler.cs:28` | kept |
| `Buyer.VerifyOrAddPaymentMethod(...)` | `ValidateOrAddBuyerAggregateWhenOrderStartedDomainEventHandler.cs:34` | kept |
| `PaymentMethod` (ctor) | `Buyer.cs:43` (inside `VerifyOrAddPaymentMethod`) | kept |
| `PaymentMethod.IsEqualTo(...)` | `Buyer.cs:34` | kept |
| `CardType` | `OrderingContextSeed.cs:21-23` (seed data) + EF mapping | kept |
| `IBuyerRepository` | `ValidateOrAddBuyerAggregateWhenOrderStartedDomainEventHandler.cs`, `BuyerRepository.cs` impl | kept |

## Exceptions and SeedWork

These are infrastructure for the aggregates above and are all reachable.

| Type | Where it is used | Verdict |
|---|---|---|
| `OrderingDomainException` | `Order.cs`, `OrderItem.cs`, `PaymentMethod.cs`, `ValidatorBehavior.cs`, `RequestManager.cs` | kept |
| `Entity` (base class) | Inherited by `Order`, `OrderItem`, `Buyer`, `PaymentMethod`; consumed by `MediatorExtension.cs` | kept |
| `IAggregateRoot` | Implemented by `Order`, `Buyer`; constrained by `IRepository<T>` | kept |
| `IRepository<T>` | Extended by `IOrderRepository`, `IBuyerRepository` | kept |
| `IUnitOfWork` | Extended by repository interfaces; implemented by `OrderingContext.cs` | kept |
| `ValueObject` | Inherited by `Address` | kept |
| `GlobalUsings.cs` | Project-wide `global using` directives — required for compilation | kept |

## Reflection / expression-tree risk

Per the ticket's Risk note ("domain events may be raised reflectively or via expression
trees — when in doubt, leave the event in"), the audit explicitly checked for that and
did not need to invoke the rule: every type already had at least one direct compile-time
reference. Nothing was retained on a "kept pending review" basis.

## Acceptance criteria status

* **`docs/audits/ordering-domain-prune.md` lists every event/entity considered, with grep
  evidence of zero callers for those deleted** — ✅ this document. No files deleted, so
  no zero-caller evidence is required; positive-evidence grep output for every kept file
  is included instead.
* **Every deleted file has zero remaining references in the `src/` tree** — ✅ vacuously
  true (no deletions).
* **`dotnet build src/Ordering.Domain` and `dotnet build src/Ordering.API` both succeed
  after the prune** — ✅ no source files in either project were modified by this ticket;
  no build regression possible. The `dotnet` CLI is not available in the agent's sandbox
  environment, so a local build was not re-run; reviewer can verify on a normal dev
  workstation.
* **No new domain events or entities are introduced** — ✅ no new `*.cs` files were
  added to `src/Ordering.Domain/`. Only `docs/audits/ordering-domain-prune.md` and a
  `.gitignore` update for Claude agent files were added by this ticket.
