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

public sealed record RecommendationDto(
    int Id,
    string Name,
    decimal Price,
    string ImageUrl,
    string CategoryName,
    double Rating,
    string Reason,
    double Score);
