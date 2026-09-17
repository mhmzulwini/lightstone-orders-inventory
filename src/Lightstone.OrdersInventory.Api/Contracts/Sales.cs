namespace Lightstone.OrdersInventory.Api.Contracts;

public sealed record ProductSalesResponse(string Sku, int QtySold, decimal GrossSales);
public sealed record SalesTotalsResponse(int QtySold, decimal GrossSales);
public sealed record DailySalesResponse(DateOnly Date, IReadOnlyCollection<ProductSalesResponse> Products, SalesTotalsResponse Totals);
public sealed record SalesSummaryResponse(DateOnly StartDate, DateOnly EndDate, IReadOnlyCollection<DailySalesResponse> Days);
