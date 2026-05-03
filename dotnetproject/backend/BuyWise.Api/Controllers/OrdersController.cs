using BuyWise.Api.Data;
using BuyWise.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace BuyWise.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class OrdersController : ControllerBase
{
    private readonly IOrderRepository _orderRepository;
    private readonly IUserActivityRepository _activityRepository;
    private readonly ICartRepository _cartRepository;

    public OrdersController(
        IOrderRepository orderRepository,
        IUserActivityRepository activityRepository,
        ICartRepository cartRepository)
    {
        _orderRepository = orderRepository;
        _activityRepository = activityRepository;
        _cartRepository = cartRepository;
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
        if (request.UserId > 0)
        {
            foreach (var item in request.Items)
            {
                await _activityRepository.RecordAsync(new UserActivityRequest(request.UserId, item.ProductId, "Purchase", item.Quantity));
            }

            await _cartRepository.ClearAsync(request.UserId);
        }

        return CreatedAtAction(nameof(CreateOrder), new { id = order.Id }, order);
    }

    [HttpGet("user/{userId:int}")]
    public async Task<ActionResult<IReadOnlyList<OrderSummaryDto>>> GetUserOrders(int userId)
    {
        var orders = await _orderRepository.GetUserOrdersAsync(userId);
        return Ok(orders);
    }

    [HttpGet("user/{userId:int}/latest")]
    public async Task<ActionResult<OrderSummaryDto>> GetLatestOrder(int userId)
    {
        var order = await _orderRepository.GetLatestForUserAsync(userId);
        return order is null ? NotFound(new { message = "No orders found." }) : Ok(order);
    }

    [HttpGet("{orderId:int}")]
    public async Task<ActionResult<OrderSummaryDto>> GetOrder(int orderId, [FromQuery] int? userId)
    {
        var order = await _orderRepository.GetByIdAsync(orderId, userId);
        return order is null ? NotFound(new { message = "Order not found." }) : Ok(order);
    }

    [HttpPost("{orderId:int}/cancel")]
    public async Task<ActionResult<OrderSummaryDto>> CancelOrder(int orderId, CancelOrderRequest request)
    {
        var order = await _orderRepository.CancelAsync(orderId, request.UserId);
        return order is null ? NotFound(new { message = "Order not found." }) : Ok(order);
    }
}

public sealed record CancelOrderRequest(int UserId);
