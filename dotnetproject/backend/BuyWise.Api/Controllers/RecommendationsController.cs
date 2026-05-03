using BuyWise.Api.Models;
using BuyWise.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace BuyWise.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class RecommendationsController : ControllerBase
{
    private readonly RecommendationService _recommendationService;

    public RecommendationsController(RecommendationService recommendationService)
    {
        _recommendationService = recommendationService;
    }

    [HttpGet("{productId:int}")]
    public async Task<ActionResult<IReadOnlyList<RecommendationDto>>> GetRecommendations(
        int productId,
        [FromQuery] string? cartIds,
        [FromQuery] int take = 6)
    {
        var cartProductIds = ParseIds(cartIds);
        var recommendations = await _recommendationService.RecommendAsync(productId, cartProductIds, take);
        return Ok(recommendations);
    }

    [HttpGet("similar/{productId:int}")]
    public async Task<ActionResult<IReadOnlyList<RecommendationDto>>> GetSimilarProducts(
        int productId,
        [FromQuery] string? cartIds,
        [FromQuery] int take = 6)
    {
        var recommendations = await _recommendationService.GetSimilarProductsAsync(productId, ParseIds(cartIds), take);
        return Ok(recommendations);
    }

    [HttpGet("frequently-bought-together/{productId:int}")]
    public async Task<ActionResult<IReadOnlyList<RecommendationDto>>> GetFrequentlyBoughtTogether(
        int productId,
        [FromQuery] int take = 4)
    {
        var recommendations = await _recommendationService.GetFrequentlyBoughtTogetherAsync(productId, take);
        return Ok(recommendations);
    }

    [HttpGet("for-you/{userId:int}")]
    public async Task<ActionResult<IReadOnlyList<RecommendationDto>>> GetRecommendedForYou(
        int userId,
        [FromQuery] int take = 8)
    {
        var recommendations = await _recommendationService.GetRecommendedForUserAsync(userId, take);
        return Ok(recommendations);
    }

    [HttpGet("user/{userId:int}")]
    public async Task<ActionResult<IReadOnlyList<RecommendationDto>>> GetUserBasedRecommendations(
        int userId,
        [FromQuery] int take = 8)
    {
        var recommendations = await _recommendationService.GetRecommendedForUserAsync(userId, take);
        return Ok(recommendations);
    }

    private static IReadOnlyList<int> ParseIds(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<int>();
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(item => int.TryParse(item, out var id) ? id : 0)
            .Where(id => id > 0)
            .Distinct()
            .ToList();
    }
}
