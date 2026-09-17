namespace Lightstone.OrdersInventory.Tests;

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TEST_SQLSERVER_CONNECTION")))
            Skip = "Set TEST_SQLSERVER_CONNECTION to run SQL Server integration tests.";
    }
}
