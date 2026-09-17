# Video demo and rehearsal script

Target length: **7-8 minutes**. Speak naturally and use this as a guide rather than reading every sentence word for word.

## Before recording

Only reset the Docker database used by this assessment. From the repository root:

```powershell
docker compose down -v
docker compose up -d
dotnet run --project .\src\Lightstone.OrdersInventory.Api
```

Prepare these windows before pressing Record:

1. Swagger in the browser using the URL printed by `dotnet run` plus `/swagger`.
2. Visual Studio or your editor with `OrderService.cs`, `AppDbContext.cs`, `SalesController.cs`, and `OrderConcurrencyTests.cs` open.
3. The API terminal, so structured logs are visible.
4. A second terminal ready to run the tests.

Use this order ID during the demo:

```text
demo-order-001
```

Use a date matching the order payload when requesting the sales report.

## 0:00-0:40 - Introduction

Show the repository README or Swagger overview.

Suggested wording:

> Hi, I am Minenhle Mzulwini. This is my Orders and Inventory Service for the Senior .NET Developer assessment. It is built with ASP.NET Core 8, Entity Framework Core, and SQL Server. I focused most of my attention on the two highest-risk requirements: preventing overselling when orders arrive simultaneously and making duplicate order submissions idempotent.

## 0:40-1:20 - Project structure and startup

Briefly show the folders under the API project.

Suggested wording:

> I kept the solution intentionally small: one deployable API and one test project. Inside the API, controllers handle HTTP concerns, contracts define request and response shapes, the order service contains the transaction, domain classes represent persisted data, and the data folder contains EF Core configuration, migrations, and seeding. This keeps the assessment easy to navigate without adding unnecessary project layers.

Show that the application is running, then call:

- `GET /health/live`
- `GET /health/ready`

Suggested wording:

> Liveness confirms that the process is running. Readiness also checks the SQL Server connection, so traffic should not be sent to an instance that cannot fulfil database requests.

## 1:20-2:10 - Products and stock

Call `GET /api/products` and show the three seeded products.

Then call `POST /api/products/SKU-001/stock-adjustments` with:

```json
{
  "quantity_change": 5
}
```

Suggested wording:

> The development startup applies the committed migration and adds sample products only when the product table is empty. Stock adjustments use one conditional SQL update, so even competing adjustments cannot make the stock negative.

## 2:10-3:25 - Submit and replay an order

Call `POST /api/orders` with:

```json
{
  "external_order_id": "demo-order-001",
  "placed_at": "2026-09-17T10:00:00Z",
  "items": [
    {
      "sku": "SKU-001",
      "qty": 2,
      "unit_price": 24.99
    }
  ]
}
```

Point out:

- `201 Created`
- `is_duplicate` is `false`
- Gross amount is `49.98`

Submit exactly the same request again and point out:

- `200 OK`
- The same order ID is returned
- `is_duplicate` is `true`

Call `GET /api/products` and show that stock was deducted only once.

Suggested wording:

> The external order ID represents the upstream checkout order. A normal replay returns the existing order. A unique database index is still the final authority if identical requests arrive at the same time, so only one can create the order and reduce inventory.

## 3:25-4:10 - Reject insufficient stock

Submit another order with a new external ID and an excessive quantity:

```json
{
  "external_order_id": "demo-order-too-large",
  "placed_at": "2026-09-17T10:05:00Z",
  "items": [
    {
      "sku": "SKU-001",
      "qty": 100000,
      "unit_price": 24.99
    }
  ]
}
```

Point out the `409 Conflict` response.

Suggested wording:

> This request is rejected because there is not enough stock. The order and all stock deductions are inside one transaction, so a failure on any line rolls the entire order back.

## 4:10-5:10 - Explain the concurrency implementation

Open `Services/OrderService.cs` and show:

1. SKU normalization and ordering.
2. The explicit transaction.
3. The conditional `ExecuteUpdateAsync` statement.
4. The unique-constraint exception handling.
5. The final commit.

Suggested wording:

> I did not use an in-process lock because that would only protect one API instance. The stock update contains both the availability check and deduction in one SQL statement. SQL Server performs that atomically while locking the row. Items are processed in SKU order to reduce deadlock risk. If any update affects zero rows, the transaction rolls back. The unique external-order index provides database-level idempotency across every API instance.

Open `Data/AppDbContext.cs` and briefly show:

- Unique SKU index
- Unique external order ID index
- Non-negative stock check constraint
- Decimal precision
- Placed-date index

## 5:10-5:55 - Daily sales report

Call:

```text
GET /api/sales/daily?startDate=2026-09-17&endDate=2026-09-17
```

Point out quantity `2` and gross sales `49.98` for `SKU-001`.

Open `Controllers/SalesController.cs`.

Suggested wording:

> The report is calculated from stored order items rather than hard-coded values. The inclusive date range is implemented as a half-open UTC interval. Filtering and grouping happen in SQL, so the API receives only one aggregate row per day and product rather than loading every order line into memory.

## 5:55-6:35 - Logging and configuration

Show the API terminal and point out the structured JSON fields for accepted, duplicate, and rejected attempts.

Show `appsettings.json` and `appsettings.Development.json`.

Suggested wording:

> Order outcomes use structured message templates, making fields such as the external order ID searchable in a logging platform. Connection strings, logging levels, and startup migration behaviour use normal ASP.NET configuration, so they can be overridden through environment variables without changing code. The checked-in password is only for the isolated local Docker database.

## 6:35-7:20 - Tests

In the second terminal, run:

```powershell
$env:TEST_SQLSERVER_CONNECTION = "Server=localhost,1433;Database=master;User Id=sa;Password=Your_password123;TrustServerCertificate=True"
dotnet test .\Lightstone.OrdersInventory.sln --no-build
```

Show `4 passed, 0 failed, 0 skipped`, then briefly open `OrderConcurrencyTests.cs`.

Suggested wording:

> The two important tests use separate database contexts against real SQL Server. One submits two different orders for the final unit and proves only one succeeds. The other submits the same external ID simultaneously and proves stock is deducted once. I did not use EF InMemory for these tests because it cannot prove SQL locking, rollback, or unique-constraint behaviour.

## 7:20-8:00 - Trade-offs and close

Suggested wording:

> For this scope I chose one clear deployable API instead of adding multiple projects and abstractions. The checkout-provided unit price is stored as a historical snapshot; in a real production integration I would validate that trust boundary. Further improvements could include authentication, an inventory movement ledger, an outbox for order events, metrics, and higher-volume load testing. The README contains setup instructions, and SOLUTION.md documents the design and trade-offs. Thank you for reviewing my submission.

## Rehearsal checklist

- [ ] Complete one practice run without recording.
- [ ] Keep the practice run between 7 and 8 minutes.
- [ ] Explain ideas in your own words instead of memorising sentences.
- [ ] Confirm Docker and SQL Server are healthy before recording.
- [ ] Use a fresh database or fresh external order IDs.
- [ ] Increase editor, Swagger, and terminal font sizes.
- [ ] Close notifications, email, and unrelated browser tabs.
- [ ] Hide personal information and unrelated connection strings.
- [ ] Confirm the selected microphone moves on Loom's audio meter.
- [ ] Record a 10-second sound test and play it back.
- [ ] Keep the final video between 5 and 10 minutes.
- [ ] Watch the final video once at normal speed.
- [ ] Set access to anyone with the link.
- [ ] Test the link in an incognito/private browser window.
