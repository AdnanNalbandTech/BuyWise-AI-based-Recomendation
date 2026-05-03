using BuyWise.Api.Data;
using BuyWise.Api.Models;

namespace BuyWise.Api.Services;

public sealed class RecommendationService
{
    private readonly IProductRepository _productRepository;
    private readonly IUserActivityRepository _activityRepository;
    private readonly ICartRepository _cartRepository;
    private readonly IConnectionFactory _connectionFactory;

    public RecommendationService(
        IProductRepository productRepository,
        IUserActivityRepository activityRepository,
        ICartRepository cartRepository,
        IConnectionFactory connectionFactory)
    {
        _productRepository = productRepository;
        _activityRepository = activityRepository;
        _cartRepository = cartRepository;
        _connectionFactory = connectionFactory;
    }

    public Task<IReadOnlyList<RecommendationDto>> RecommendAsync(
        int productId,
        IReadOnlyList<int>? cartProductIds,
        int take = 6) =>
        GetSimilarProductsAsync(productId, cartProductIds, take);

    public async Task<IReadOnlyList<RecommendationDto>> GetSimilarProductsAsync(
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
            .Select(product => ScoreProductSimilarity(target, product, cartProducts))
            .OrderByDescending(item => item.Score)
            .ThenByDescending(item => item.Rating)
            .Take(Math.Clamp(take, 1, 12))
            .ToList();
    }

    public async Task<IReadOnlyList<RecommendationDto>> GetRecommendedForUserAsync(int userId, int take = 8)
    {
        var allProducts = await _productRepository.GetProductsAsync();
        var recentProductIds = await _activityRepository.GetRecentProductIdsAsync(userId, 20);
        var purchasedProductIds = await _activityRepository.GetPurchasedProductIdsAsync(userId, 20);
        var cart = await _cartRepository.GetAsync(userId);
        var cartProductIds = cart.Items.Select(item => item.ProductId).ToHashSet();

        var interestProducts = recentProductIds
            .Concat(purchasedProductIds)
            .Concat(cartProductIds)
            .Distinct()
            .Select(id => allProducts.FirstOrDefault(product => product.Id == id))
            .OfType<Product>()
            .ToList();

        if (interestProducts.Count == 0)
        {
            return allProducts
                .OrderByDescending(product => product.Featured)
                .ThenByDescending(product => product.Rating)
                .ThenByDescending(product => product.ReviewCount)
                .Take(Math.Clamp(take, 1, 12))
                .Select(product => ToRecommendation(product, "Trending now for BUYWISE shoppers", product.Rating * 10))
                .ToList();
        }

        var interestTags = interestProducts.SelectMany(Tags).ToHashSet();
        var interestCategories = interestProducts.Select(product => product.CategoryId).ToHashSet();
        var interestBrands = interestProducts.Select(product => product.Brand.ToLowerInvariant()).ToHashSet();

        return allProducts
            .Where(product => !purchasedProductIds.Contains(product.Id))
            .Select(product =>
            {
                var score = product.Rating * 4 + Math.Min(product.ReviewCount / 60.0, 8);
                var reasons = new List<string>();
                var sharedTags = Tags(product).Intersect(interestTags).Take(3).ToArray();

                if (interestCategories.Contains(product.CategoryId))
                {
                    score += 28;
                    reasons.Add($"matches your {product.CategoryName} activity");
                }

                if (sharedTags.Length > 0)
                {
                    score += sharedTags.Length * 11;
                    reasons.Add($"matches {string.Join(", ", sharedTags)}");
                }

                if (interestBrands.Contains(product.Brand.ToLowerInvariant()))
                {
                    score += 7;
                    reasons.Add($"brand fit: {product.Brand}");
                }

                if (cartProductIds.Contains(product.Id))
                {
                    score -= 35;
                }

                if (product.Featured)
                {
                    score += 4;
                }

                var reason = reasons.Count == 0
                    ? "Popular pick based on BUYWISE trends"
                    : $"Recommended for you: {string.Join("; ", reasons.Take(3))}";

                return ToRecommendation(product, reason, score);
            })
            .OrderByDescending(product => product.Score)
            .ThenByDescending(product => product.Rating)
            .Take(Math.Clamp(take, 1, 12))
            .ToList();
    }

    public async Task<IReadOnlyList<RecommendationDto>> GetFrequentlyBoughtTogetherAsync(int productId, int take = 4)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT p.id, p.name, p.description, p.price, p.image_url, p.stock, p.category_id,
                   c.name AS category_name, p.rating, p.review_count, p.brand, p.tags, p.featured, p.created_at,
                   f.reason, f.confidence
            FROM frequently_bought_together f
            INNER JOIN products p ON p.id = f.related_product_id
            INNER JOIN categories c ON c.id = p.category_id
            WHERE f.primary_product_id = @ProductId
            ORDER BY f.confidence DESC
            LIMIT @Take;
            """;
        command.Parameters.AddWithValue("@ProductId", productId);
        command.Parameters.AddWithValue("@Take", Math.Clamp(take, 1, 8));

        var recommendations = new List<RecommendationDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            recommendations.Add(new RecommendationDto(
                reader.GetInt32("id"),
                reader.GetString("name"),
                reader.GetDecimal("price"),
                reader.GetString("image_url"),
                reader.GetString("category_name"),
                reader.GetDouble("rating"),
                $"Frequently bought together: {reader.GetString("reason")}",
                Math.Round(reader.GetDouble("confidence") * 100, 2)));
        }

        if (recommendations.Count > 0)
        {
            return recommendations;
        }

        return await GetSimilarProductsAsync(productId, Array.Empty<int>(), take);
    }

    public async Task<IReadOnlyList<RecommendationDto>> SearchForShoppingAssistantAsync(ProductSearchRequest request, int take = 5)
    {
        var products = await _productRepository.GetProductsAsync(request);
        return products
            .Take(Math.Clamp(take, 1, 8))
            .Select(product => ToRecommendation(product, "Matched your shopping request", product.Rating * 10))
            .ToList();
    }

    private static RecommendationDto ScoreProductSimilarity(Product target, Product candidate, IReadOnlyList<Product> cartProducts)
    {
        var targetTags = Tags(target).ToHashSet();
        var candidateTags = Tags(candidate).ToHashSet();
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
            score += sharedTags.Length * 10;
            reasons.Add($"matches {string.Join(", ", sharedTags.Take(3))}");
        }

        if (candidate.Brand.Equals(target.Brand, StringComparison.OrdinalIgnoreCase))
        {
            score += 8;
            reasons.Add($"same {candidate.Brand} brand");
        }

        var priceGap = Math.Abs(candidate.Price - target.Price);
        var priceBase = Math.Max(target.Price, 1);
        var priceScore = Math.Max(0, 20 - (double)(priceGap / priceBase) * 20);
        score += priceScore;
        if (priceScore >= 11)
        {
            reasons.Add("similar price range");
        }

        var cartCategoryMatches = cartProducts.Count(product => product.CategoryId == candidate.CategoryId);
        var cartTagMatches = cartProducts
            .SelectMany(Tags)
            .Intersect(candidateTags)
            .Count();
        score += cartCategoryMatches * 13;
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

        return ToRecommendation(candidate, $"Similar product: {string.Join("; ", reasons.Take(3))}", score);
    }

    private static RecommendationDto ToRecommendation(Product product, string reason, double score) =>
        new(
            product.Id,
            product.Name,
            product.Price,
            product.ImageUrl,
            product.CategoryName,
            product.Rating,
            reason,
            Math.Round(score, 2));

    private static IEnumerable<string> Tags(Product product) =>
        product.Tags
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(tag => tag.ToLowerInvariant());
}
