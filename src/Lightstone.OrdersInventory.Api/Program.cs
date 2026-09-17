using System.Text.Json;
using System.Text.Json.Serialization;
using Lightstone.OrdersInventory.Api.Data;
using Lightstone.OrdersInventory.Api.Services;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// JSON console logs remain structured so a log platform can search fields such as
// ExternalOrderId and OrderId without parsing a human-formatted message.
builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole(options => options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ");

// Snake-case JSON keeps the public API consistent with the examples in the brief.
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
});
builder.Services.AddProblemDetails();
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(
    builder.Configuration.GetConnectionString("OrdersDatabase")));
builder.Services.AddScoped<OrderService>();
// Injecting TimeProvider makes server-owned timestamps replaceable in tests.
builder.Services.AddSingleton(TimeProvider.System);

// Liveness only checks the process; readiness additionally checks SQL Server.
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"], timeout: TimeSpan.FromSeconds(3));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Swagger is a development/demo tool and is not exposed in production by default.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = check => check.Tags.Contains("live") });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

// Local development can create and seed its database automatically. A production
// deployment should normally apply migrations as a separate controlled step.
if (app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
    await SeedData.InitialiseAsync(app.Services);

app.Run();

public partial class Program;
