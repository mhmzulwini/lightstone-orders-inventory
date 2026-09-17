# Orders & Inventory Service

A production-minded ASP.NET Core 8 API for product inventory, idempotent order submission, and daily sales reporting. SQL Server owns the important correctness guarantees: unique order IDs, non-negative inventory, and atomic stock deductions.

## Prerequisites

- .NET 8 SDK or later
- Docker Desktop (recommended), or a reachable SQL Server 2022 instance

## Run locally

From the repository root:

```powershell
docker compose up -d
dotnet tool restore
dotnet restore --configfile .\NuGet.Config
dotnet run --project .\src\Lightstone.OrdersInventory.Api
```

On first development startup, the API applies its EF Core migrations and seeds three products. Open Swagger using the URL printed by `dotnet run` plus `/swagger`. Ready and live checks are available at `/health/ready` and `/health/live`.

The default development connection is suitable only for this local Docker setup. Override it without changing code:

```powershell
$env:ConnectionStrings__OrdersDatabase = "Server=...;Database=...;User Id=...;Password=...;TrustServerCertificate=True"
$env:Database__ApplyMigrationsOnStartup = "false"
$env:Logging__LogLevel__Default = "Information"
```

For production, inject secrets through the deployment platform and apply migrations as a separate deployment step.

## API

| Method | Route | Purpose |
|---|---|---|
| `GET` | `/api/products` | List products and available stock |
| `POST` | `/api/products` | Add a product |
| `POST` | `/api/products/{sku}/stock-adjustments` | Increase or decrease stock |
| `POST` | `/api/orders` | Submit an idempotent order |
| `GET` | `/api/sales/daily?startDate=2026-09-01&endDate=2026-09-30` | Daily product and overall totals |
| `GET` | `/health/live` | Process liveness |
| `GET` | `/health/ready` | Readiness including SQL Server |

Ready-to-run requests are in [`Lightstone.OrdersInventory.Api.http`](src/Lightstone.OrdersInventory.Api/Lightstone.OrdersInventory.Api.http).

Example order:

```json
{
  "external_order_id": "ext-10001",
  "placed_at": "2026-09-16T12:34:56Z",
  "items": [
    { "sku": "SKU-001", "qty": 2, "unit_price": 24.99 }
  ]
}
```

The first accepted request returns `201 Created`. Replaying `ext-10001` returns `200 OK`, the original order, and `is_duplicate: true`. Missing products return `404`; insufficient stock and unsafe negative adjustments return `409`.

## Test

Fast validation tests run everywhere:

```powershell
dotnet test .\Lightstone.OrdersInventory.sln
```

The concurrency tests intentionally require SQL Server because an in-memory substitute cannot prove database locking and unique-constraint behavior. With the Docker container running:

```powershell
$env:TEST_SQLSERVER_CONNECTION = "Server=localhost,1433;Database=master;User Id=sa;Password=Your_password123;TrustServerCertificate=True"
dotnet test .\Lightstone.OrdersInventory.sln
```

Alternatively, an existing local SQL Server Express instance can be used on Windows:

```powershell
$env:TEST_SQLSERVER_CONNECTION = "Server=localhost\SQLEXPRESS;Database=master;Integrated Security=True;Encrypt=False"
dotnet test .\Lightstone.OrdersInventory.sln
```

`Encrypt=False` is only a compatibility setting for older local SQL Express installations. Keep encryption enabled for deployed environments.

Each integration test creates an isolated temporary database and removes it afterward. One submits two different orders against one unit of stock; the other submits the same external ID simultaneously. Both verify persisted stock and order counts.

## Repository guide

- `Domain/`: persisted entities and business state
- `Contracts/`: HTTP request and response shapes
- `Data/`: EF Core context, migrations, and demo seeding
- `Services/OrderService.cs`: transaction, idempotency, and inventory logic
- `Controllers/`: HTTP mapping and the aggregate sales query
- `tests/`: validation and real SQL Server concurrency tests
- [`SOLUTION.md`](SOLUTION.md): architecture and trade-offs
- [`AI-USAGE.md`](AI-USAGE.md): AI prompts and how AI was used
- [`VIDEO-GUIDE.md`](VIDEO-GUIDE.md): suggested demo flow

## Cleanup

```powershell
docker compose down
```

Add `-v` only if you also want to delete the local SQL Server volume and all demo data.
