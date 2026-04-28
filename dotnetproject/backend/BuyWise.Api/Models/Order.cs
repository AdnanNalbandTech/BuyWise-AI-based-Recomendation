namespace BuyWise.Api.Models;

public sealed record OrderRequest(
    int UserId,
    string FullName,
    string Email,
    string ShippingAddress,
    IReadOnlyList<OrderItemRequest> Items);

public sealed record OrderItemRequest(
    int ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice);

public sealed record OrderResponse(
    int Id,
    decimal Total,
    DateTime CreatedAt);
