using BuyWise.Api.Data;
using BuyWise.Api.Models;

namespace BuyWise.Api.Services;

public sealed class RecommendationService
{
    private readonly IProductRepository _productRepository;

    public RecommendationService(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    public async Task<IReadOnlyList<RecommendationDto>> RecommendAsync(
        int productId,
        IReadOnlyList<int>? cartProductIds,
        int take = 6)
    {
        var allProducts = await _productRepository.GetProductsAsync();
        var target = allProducts.FirstOrDefault(product => product.Id == productId);
        if (target is null)
        {
            return Array.Empty<RecommendationDto>();
        }

        var cartProducts = (cartProductIds ?? Array.Empty<int>())
            .Where(id => id != productId)
            .Select(id => allProducts.FirstOrDefault(product => product.Id == id))
            .OfType<Product>()
            .ToList();

        return allProducts
            .Where(product => product.Id != productId)
            .Select(product => ScoreProduct(target, product, cartProducts))
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Rating)
            .Take(Math.Clamp(take, 1, 12))
            .ToList();
    }

    private static RecommendationDto ScoreProduct(Product target, Product candidate, IReadOnlyList<Product> cartProducts)
    {
        var targetTags = Tags(target);
        var candidateTags = Tags(candidate);
        var sharedTags = targetTags.Intersect(candidateTags).ToArray();
        var score = 0.0;
        var reasons = new List<string>();

        if (candidate.CategoryId == target.CategoryId)
        {
            score += 50;
            reasons.Add($"same {candidate.CategoryName} category");
        }

        if (sharedTags.Length > 0)
        {
            score += sharedTags.Length * 9;
            reasons.Add($"matches {string.Join(", ", sharedTags.Take(3))}");
        }

        if (candidate.Brand.Equals(target.Brand, StringComparison.OrdinalIgnoreCase))
        {
            score += 7;
            reasons.Add($"same {candidate.Brand} brand");
        }

        var priceGap = Math.Abs(candidate.Price - target.Price);
        var priceBase = Math.Max(target.Price, 1);
        var priceScore = Math.Max(0, 18 - (double)(priceGap / priceBase) * 18);
        score += priceScore;
        if (priceScore >= 10)
        {
            reasons.Add("similar price range");
        }

        var cartCategoryMatches = cartProducts.Count(product => product.CategoryId == candidate.CategoryId);
        var cartTagMatches = cartProducts
            .SelectMany(Tags)
            .Intersect(candidateTags)
            .Count();
        score += cartCategoryMatches * 12;
        score += cartTagMatches * 4;
        if (cartCategoryMatches > 0 || cartTagMatches > 1)
        {
            reasons.Add("fits your basket");
        }

        score += candidate.Rating * 3;
        score += Math.Min(candidate.ReviewCount / 50.0, 8);
        if (candidate.Featured)
        {
            score += 4;
        }

        if (reasons.Count == 0)
        {
            reasons.Add("popular with BUYWISE shoppers");
        }

        return new RecommendationDto(
            candidate.Id,
            candidate.Name,
            candidate.Price,
            candidate.ImageUrl,
            candidate.CategoryName,
            candidate.Rating,
            $"AI match: {string.Join("; ", reasons.Take(3))}",
            Math.Round(score, 2));
    }

    private static IEnumerable<string> Tags(Product product) =>
        product.Tags
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(tag => tag.ToLowerInvariant());
}
