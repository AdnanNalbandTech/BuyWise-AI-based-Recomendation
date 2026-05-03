using BuyWise.Api.Data;
using BuyWise.Api.Models;
using Microsoft.AspNetCore.Mvc;

namespace BuyWise.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class CartController : ControllerBase
{
    private readonly ICartRepository _cartRepository;
    private readonly IUserActivityRepository _activityRepository;

    public CartController(ICartRepository cartRepository, IUserActivityRepository activityRepository)
    {
        _cartRepository = cartRepository;
        _activityRepository = activityRepository;
    }

    [HttpGet("{userId:int}")]
    public async Task<ActionResult<CartSummaryDto>> GetCart(int userId)
    {
        var cart = await _cartRepository.GetAsync(userId);
        return Ok(cart);
    }

    [HttpPost]
    public async Task<ActionResult<CartSummaryDto>> AddToCart(CartUpsertRequest request)
    {
        if (request.UserId <= 0 || request.ProductId <= 0)
        {
            return BadRequest(new { message = "User and product are required." });
        }

        var cart = await _cartRepository.AddAsync(request);
        await _activityRepository.RecordAsync(new UserActivityRequest(request.UserId, request.ProductId, "CartAdd", request.Quantity));
        return Ok(cart);
    }

    [HttpPut("{productId:int}")]
    public async Task<ActionResult<CartSummaryDto>> UpdateQuantity(int productId, CartQuantityRequest request)
    {
        var cart = await _cartRepository.UpdateQuantityAsync(request.UserId, productId, request.Quantity);
        return Ok(cart);
    }

    [HttpDelete("{userId:int}/items/{productId:int}")]
    public async Task<ActionResult<CartSummaryDto>> RemoveItem(int userId, int productId)
    {
        var cart = await _cartRepository.RemoveAsync(userId, productId);
        return Ok(cart);
    }

    [HttpDelete("{userId:int}")]
    public async Task<IActionResult> ClearCart(int userId)
    {
        await _cartRepository.ClearAsync(userId);
        return NoContent();
    }
}
