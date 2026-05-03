namespace BuyWise.Api.Models;

public sealed record UserActivity(
    int Id,
    int UserId,
    int ProductId,
    string ActivityType,
    int Quantity,
    DateTime CreatedAt);

public sealed record UserActivityRequest(
    int UserId,
    int ProductId,
    string ActivityType,
    int Quantity = 1);
