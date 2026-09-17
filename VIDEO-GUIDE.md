# Video demo guide (about 8 minutes)

Use this as speaking notes, not a script to read verbatim.

## 0:00-0:45 - Problem and approach

- The service accepts external orders, decrements physical inventory, and reports daily sales.
- The two risks I prioritized are overselling under concurrency and processing a duplicated upstream order twice.
- SQL Server, rather than application memory, enforces both guarantees so multiple API instances remain safe.

## 0:45-1:30 - Run and project shape

- Show `docker compose up -d`, the API starting, Swagger, `/health/live`, and `/health/ready`.
- Briefly show the folders: contracts, controllers, data, domain, services, tests.
- Explain why one deployable project is proportionate for this assessment.

## 1:30-3:15 - Order transaction

- Open `OrderService.SubmitAsync`.
- Show SKU normalization and duplicate-line rejection.
- Show the provisional order insert backed by the unique `ExternalOrderId` index.
- Show each conditional `ExecuteUpdateAsync`: stock changes only when enough remains.
- Explain rollback on any failed line, SKU ordering, and why an in-process lock was rejected.
- Show the unique-constraint catch returning the original order with `is_duplicate: true`.

## 3:15-4:30 - Working behavior

- List seeded products.
- Submit `ext-10001`; point out `201` and reduced stock.
- Submit the identical request again; point out `200`, `is_duplicate: true`, the same order ID, and unchanged stock.
- Submit an excessive quantity; show `409` and unchanged stock.

## 4:30-5:20 - Sales report

- Call the daily endpoint for a range containing the example order.
- Show product quantity/gross sales and daily overall totals.
- In code, point out the half-open UTC range, database grouping, `AsNoTracking`, placed-date index, and 367-day guard.

## 5:20-6:15 - Operations and configuration

- Show structured order outcome logs in the console.
- Contrast liveness with readiness: readiness includes SQL Server.
- Show connection string, logging-level, and migration settings, emphasizing environment-variable overrides and no production secret in source.

## 6:15-7:15 - Tests

- Run tests with `TEST_SQLSERVER_CONNECTION` configured.
- Open the two concurrency tests: different orders competing for one unit, and the same ID submitted twice.
- Explain why SQL Server is used instead of EF InMemory for these tests.

## 7:15-8:00 - Trade-offs and next steps

- Unit price is an order snapshot supplied by checkout; in production, validate the trust boundary.
- Empty sales days are omitted; a date dimension could add zero rows.
- Next production additions: authentication, inventory movement ledger, outbox/events, observability metrics, and load testing.
- Finish by pointing reviewers to `SOLUTION.md` for the complete rationale.

## Recording checklist

- Start Docker and the API before recording.
- Reset demo data or use a fresh external order ID.
- Increase editor and terminal font sizes.
- Close notifications and hide unrelated windows or secrets.
- Test microphone audio, then keep the final recording between 5 and 10 minutes.
