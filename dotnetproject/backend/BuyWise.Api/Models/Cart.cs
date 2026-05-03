namespace BuyWise.Api.Models;

public sealed record CartItemDto(
    int ProductId,
    string ProductName,
    decimal Price,
    string ImageUrl,
    string CategoryName,
    string Brand,
    int Quantity,
    decimal LineTotal);

public sealed record CartSummaryDto(
    int UserId,
    IReadOnlyList<CartItemDto> Items,
    decimal Total);

public sealed record CartUpsertRequest(
    int UserId,
    int ProductId,
    int Quantity = 1);

public sealed record CartQuantityRequest(
    int UserId,
    int Quantity);
