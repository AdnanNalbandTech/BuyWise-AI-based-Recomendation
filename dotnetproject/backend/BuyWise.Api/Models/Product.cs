namespace BuyWise.Api.Models;

public sealed record Product(
    int Id,
    string Name,
    string Description,
    decimal Price,
    string ImageUrl,
    int Stock,
    int CategoryId,
    string CategoryName,
    double Rating,
    int ReviewCount,
    string Brand,
    string Tags,
    bool Featured,
    DateTime CreatedAt);

public sealed record ProductUpsertRequest(
    string Name,
    string Description,
    decimal Price,
    string ImageUrl,
    int Stock,
    int CategoryId,
    double Rating,
    int ReviewCount,
    string Brand,
    string Tags,
    bool Featured);

public sealed record ProductSearchRequest
{
    public string? Search { get; init; }
    public int? CategoryId { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public string? Brand { get; init; }
    public double? MinRating { get; init; }
    public string? Tags { get; init; }
}

public sealed record RecommendationDto(
    int Id,
    string Name,
    decimal Price,
    string ImageUrl,
    string CategoryName,
    double Rating,
    string Reason,
    double Score);
