using Lightstone.OrdersInventory.Api.Contracts;
using Lightstone.OrdersInventory.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Lightstone.OrdersInventory.Api.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController(OrderService orderService) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<OrderResponse>> Submit(SubmitOrderRequest request, CancellationToken cancellationToken)
    {
        var result = await orderService.SubmitAsync(request, cancellationToken);
        return result.Outcome switch
        {
            SubmitOrderOutcome.Accepted => CreatedAtAction(nameof(Submit), result.Order),
            SubmitOrderOutcome.Duplicate => Ok(result.Order),
            SubmitOrderOutcome.ProductNotFound => NotFound(Problem("Product not found", result.Detail, 404)),
            SubmitOrderOutcome.InsufficientStock => Conflict(Problem("Insufficient stock", result.Detail, 409)),
            _ => BadRequest(Problem("Invalid order", result.Detail, 400))
        };
    }

    private static ProblemDetails Problem(string title, string? detail, int status) => new() { Title = title, Detail = detail, Status = status };
}
