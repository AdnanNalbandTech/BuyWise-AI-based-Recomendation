using BuyWise.Api.Data;
using BuyWise.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace BuyWise.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class OrdersController : ControllerBase
{
    private readonly IOrderRepository _orderRepository;

    public OrdersController(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    [HttpPost]
    public async Task<ActionResult<OrderResponse>> CreateOrder(OrderRequest request)
    {
        if (request.Items.Count == 0)
        {
            return BadRequest(new { message = "Cart is empty." });
        }

        if (string.IsNullOrWhiteSpace(request.FullName)
            || string.IsNullOrWhiteSpace(request.Email)
            || string.IsNullOrWhiteSpace(request.ShippingAddress))
        {
            return BadRequest(new { message = "Customer and shipping details are required." });
        }

        var order = await _orderRepository.CreateAsync(request);
        return CreatedAtAction(nameof(CreateOrder), new { id = order.Id }, order);
    }
}
