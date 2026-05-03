namespace BuyWise.Api.Models;

public sealed record ChatbotRequest(
    string Message,
    int? UserId,
    int? CurrentProductId,
    IReadOnlyList<int>? CartProductIds);

public sealed record ChatbotResponse(
    string Reply,
    string Intent,
    IReadOnlyList<RecommendationDto> Products,
    CartSummaryDto? Cart,
    OrderSummaryDto? Order,
    IReadOnlyList<string> QuickReplies);
