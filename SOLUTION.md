# Solution design and trade-offs

## Architecture

The service is a single deployable ASP.NET Core API with boundaries kept explicit inside the project: controllers own HTTP concerns, contracts define the public API, `OrderService` owns the order transaction, entities represent persisted state, and `AppDbContext` owns SQL mapping. This is deliberately smaller than a multi-project clean-architecture solution. For a four-hour service, extra projects would add navigation and ceremony without creating a meaningful deployment boundary.

SQL Server is the source of truth. The API can be scaled horizontally because correctness does not depend on an in-process lock.

## Data model

- `Products`: unique normalized SKU, current catalogue price, and available stock.
- `Orders`: unique external order ID plus customer-supplied `PlacedAt` and server-owned `AcceptedAt` timestamps.
- `OrderItems`: product reference plus immutable SKU, quantity, and unit-price snapshots.

Money uses `decimal(18,2)`. Timestamps use `DateTimeOffset` and incoming `placed_at` must have a zero offset. A database check constraint prevents negative stock even if data is changed outside this API. Indexes support SKU lookup, external-ID idempotency, placed-date filtering, and order-item joins.

The checkout-provided `unit_price` is treated as the authoritative historical sale price. It is stored rather than recomputed from the mutable catalogue price. In a real integration I would confirm this trust boundary and may additionally compare the price with a server-side quote or signed checkout payload.

## Order transaction and concurrency

One explicit SQL transaction contains the complete state change:

1. Insert the order with its unique `external_order_id`.
2. Resolve all products.
3. For every item, execute one conditional SQL update equivalent to `SET AvailableStock = AvailableStock - quantity WHERE AvailableStock >= quantity`.
4. Insert immutable order-line snapshots.
5. Commit.

If any product is missing or lacks stock, rollback removes the provisional order and reverses all earlier item deductions. Conditional updates are atomic in SQL Server, so competing requests cannot both consume the same final units. Products are updated in normalized SKU order to reduce deadlock risk for multi-item orders.

The unique database index is the idempotency authority. A duplicate insert receives SQL Server error 2601/2627, rolls back, clears tracked state, and returns the already committed order. This also handles two duplicate requests arriving at the same time: the unique-key check blocks until the winning transaction commits or rolls back.

Retries are not enabled around this user-managed transaction. If transient retry support became necessary, the entire transaction delegate—not individual commands—would be run through EF Core's execution strategy so it could replay safely.

## Sales query

The report filters by an inclusive UTC date range using a half-open interval (`>= start`, `< day-after-end`), groups in SQL by order date and SKU, and returns only the aggregated rows. Overall daily totals are summed from those already-small grouped results in memory. The query is read-only and uses `AsNoTracking`.

The response omits dates with no sales. Returning calendar-zero rows would require a date dimension or generated date series; that was not required. The range is limited to 367 days to protect the endpoint. For much larger data volumes, I would consider a covering index, read replica, cached/materialized daily aggregates, and pagination or asynchronous exports.

## Operations

- `/health/live` checks the web process only.
- `/health/ready` also checks SQL connectivity.
- Structured message templates record every valid order attempt and its accepted, duplicate, missing-product, or insufficient-stock outcome without logging the full customer payload.
- Connection strings, startup migration behavior, and log levels use standard ASP.NET configuration and can be overridden with environment variables or a secret provider.
- Migrations and sample seeding happen automatically only in Development by default. Production should run migrations separately.

## API decisions

- `201 Created`: newly accepted order or product.
- `200 OK`: idempotent replay, with `is_duplicate: true`.
- `400 Bad Request`: malformed request, duplicate SKUs within one order, non-UTC timestamp, or invalid report range.
- `404 Not Found`: referenced SKU does not exist.
- `409 Conflict`: insufficient stock or a stock adjustment that would cross zero.
- JSON uses snake_case to match the supplied examples.

SKUs are trimmed and normalized to uppercase. Duplicate SKU lines are rejected rather than silently merged because merging lines with different prices would hide an ambiguous upstream request.

## Testing strategy

Validation tests prove rejection happens before database work. The highest-risk behavior is tested against actual SQL Server: simultaneous orders competing for one unit, and simultaneous submissions using the same external ID. EF InMemory or mocks would not exercise SQL atomicity, locking, rollback, or unique constraints and would provide false confidence here.

Potential next tests include API-level response contract tests, sales aggregates across UTC date boundaries, database migration tests, cancellation, and randomized high-contention load tests.

## Deliberate omissions

- Authentication and authorization: not part of the supplied scope; operational product/stock endpoints would normally require privileged access.
- Inventory movement ledger: a valuable audit feature, but the brief only requires current available stock. I would add immutable movements for reconciliation in a production retail system.
- Messaging/outbox: no downstream event requirement was supplied. If order-accepted events were needed, an outbox record would be committed in the same transaction.
- Reservation and cancellation workflows: the assignment models immediate acceptance only.
- Repository abstractions over EF Core: EF already supplies unit-of-work and repository behavior; another generic layer would obscure the conditional SQL update.
