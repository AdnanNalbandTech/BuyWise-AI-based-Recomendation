namespace BuyWise.Api.Models;

public sealed record Category(
    int Id,
    string Name,
    string Slug,
    string Description,
    string ImageUrl);
