# How I used AI during development

## Overview

I used OpenAI Codex as a development partner while completing this assessment. My goal was not simply to generate code, but to use AI to understand the problem, compare possible approaches, speed up implementation, identify mistakes, and verify the finished solution.

I worked iteratively: I gave Codex a specific objective, reviewed the result, asked follow-up questions, ran the code, and corrected issues found during testing. I made sure I understood the final implementation and the reasons behind its design.

## 1. Understanding the requirements

My first step was to ask Codex for a clear, simple summary of the assignment requirements. I wanted to confirm what needed to be delivered before making technical decisions.

Representative prompt:

> Review the attached assessment and give me a clear summary of the functional requirements, technical requirements, deliverables, and the areas the reviewers are most likely to evaluate.

This helped me identify the three main areas of work:

- Product and inventory management
- Safe and idempotent order processing
- Daily sales reporting and operational readiness

It also highlighted that concurrency and duplicate order handling were the highest-risk parts of the solution.

## 2. Designing the approach

I then used AI to discuss an appropriate architecture for a four-hour assessment. I wanted the solution to be easy to understand without sacrificing database correctness.

Representative prompt:

> Propose a simple production-minded architecture using ASP.NET Core 8, Entity Framework Core, and SQL Server. Explain how to prevent overselling and duplicate order processing when requests arrive simultaneously.

After reviewing the options, I chose:

- One ASP.NET Core API project with clear internal folders
- Entity Framework Core with SQL Server
- A transaction around order creation, inventory deductions, and order items
- Conditional SQL stock updates to prevent inventory from becoming negative
- A unique database index on `ExternalOrderId` for idempotency
- Immutable order-line price snapshots for accurate historical reporting

I deliberately avoided using an in-memory application lock because it would not protect inventory when more than one API instance is running.

## 3. Implementing the solution

I asked Codex to help implement the solution in small, reviewable areas rather than treating the assignment as one unexplained code-generation request.

Examples of the instructions I used:

> Implement the product model, order model, EF Core mappings, and SQL Server constraints required by the design.

> Implement order submission as one transaction. Use an atomic conditional SQL update so simultaneous orders cannot oversell stock.

> Add duplicate-order handling that returns the original order without reducing inventory again.

> Add a daily sales endpoint that groups stored order items by UTC date and product and returns product and overall totals.

> Add structured logging, liveness and readiness checks, configuration, migrations, and sample data.

I reviewed the generated files and asked for explanatory comments around the important reasoning, especially transactions, atomic updates, rollback behaviour, reporting, and integration tests.

## 4. Testing and debugging

I used AI to help run the solution, reproduce failures, interpret error messages, and fix the causes rather than hiding them.

For example, the first end-to-end API test exposed an ASP.NET Core validation error caused by attributes being attached to positional-record properties instead of constructor parameters. I asked Codex to diagnose and fix the runtime error, then repeated the full API scenario.

Representative prompts:

> Build the complete solution, run the tests, and fix confirmed errors without weakening the business requirements.

> Run the API against SQL Server and verify product creation, stock adjustment, successful orders, duplicate replay, insufficient stock, sales totals, and health checks.

> Add real SQL Server concurrency tests for two orders competing for the final stock unit and for two simultaneous submissions with the same external order ID.

The concurrency tests use real SQL Server rather than EF Core's in-memory provider because the important behaviour depends on SQL transactions, locking, conditional updates, and unique constraints.

The final verification result was:

```text
Build succeeded: 0 warnings, 0 errors
Tests passed: 4
Tests failed: 0
Tests skipped: 0
```

## 5. Documentation and preparation

I also used AI to help draft and improve:

- Local setup instructions
- The architecture and trade-off explanation
- API examples
- Code comments
- A video demonstration outline
- Likely technical interview discussion points

I reviewed and personalized these documents so they describe the solution I implemented and the decisions I can explain myself.

## Useful development commands

These are the commands I used to restore, migrate, build, test, and run the completed project. I have omitted routine file-inspection and troubleshooting commands because they do not help someone understand or reproduce the solution.

### Restore and build

```powershell
dotnet tool restore
dotnet restore .\Lightstone.OrdersInventory.sln --configfile .\NuGet.Config
dotnet build .\Lightstone.OrdersInventory.sln --no-restore
```

### Create a migration after changing the database model

```powershell
dotnet tool run dotnet-ef migrations add MigrationName `
  --project .\src\Lightstone.OrdersInventory.Api `
  --startup-project .\src\Lightstone.OrdersInventory.Api
```

The submitted solution already contains the `InitialCreate` migration, so a reviewer does not need to run this command during setup.

### Run with the supplied Docker SQL Server

```powershell
docker compose up -d
dotnet run --project .\src\Lightstone.OrdersInventory.Api
```

The API applies migrations and adds sample products automatically in the Development environment.

### Run all SQL Server integration tests

```powershell
$env:TEST_SQLSERVER_CONNECTION = "Server=localhost,1433;Database=master;User Id=sa;Password=Your_password123;TrustServerCertificate=True"
dotnet test .\Lightstone.OrdersInventory.sln --no-build
```

### Verify code formatting

```powershell
dotnet format .\Lightstone.OrdersInventory.sln --no-restore --verify-no-changes
```

### Stop the local database

```powershell
docker compose down
```

## What AI contributed and what I remained responsible for

AI helped me work faster by summarizing requirements, suggesting implementation options, generating code, identifying errors, and drafting documentation. I remained responsible for selecting the final approach, running and reviewing the solution, checking that the business requirements were satisfied, understanding the trade-offs, and ensuring I could explain the implementation during the video and technical interview.

The most important decision was to place the concurrency and idempotency guarantees in SQL Server rather than relying on process-local state. That makes the design safe even when multiple API instances handle requests at the same time.
