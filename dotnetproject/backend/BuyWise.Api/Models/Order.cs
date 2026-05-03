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
    DateTime CreatedAt,
    string Status = "Pending",
    string? TrackingNumber = null,
    DateTime? EstimatedDelivery = null);

public sealed record OrderSummaryDto(
    int Id,
    decimal Total,
    string Status,
    string? TrackingNumber,
    DateTime? EstimatedDelivery,
    DateTime CreatedAt,
    IReadOnlyList<OrderItemRequest> Items);
