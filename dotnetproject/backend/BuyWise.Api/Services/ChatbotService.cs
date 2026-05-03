using System.Text.RegularExpressions;
using BuyWise.Api.Data;
using BuyWise.Api.Models;

namespace BuyWise.Api.Services;

public sealed class ChatbotService
{
    private readonly IProductRepository _productRepository;
    private readonly RecommendationService _recommendationService;
    private readonly ICartRepository _cartRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IUserActivityRepository _activityRepository;
    private readonly IFaqRepository _faqRepository;

    public ChatbotService(
        IProductRepository productRepository,
        RecommendationService recommendationService,
        ICartRepository cartRepository,
        IOrderRepository orderRepository,
        IUserActivityRepository activityRepository,
        IFaqRepository faqRepository)
    {
        _productRepository = productRepository;
        _recommendationService = recommendationService;
        _cartRepository = cartRepository;
        _orderRepository = orderRepository;
        _activityRepository = activityRepository;
        _faqRepository = faqRepository;
    }

    public async Task<ChatbotResponse> ProcessAsync(ChatbotRequest request)
    {
        var message = request.Message.Trim();
        var normalized = message.ToLowerInvariant();
        var userId = request.UserId.GetValueOrDefault();

        if (string.IsNullOrWhiteSpace(message))
        {
            return Reply("Ask me to find products, recommend add-ons, manage your cart, track orders, or answer shopping FAQs.", "Empty");
        }

        var faq = await _faqRepository.FindBestMatchAsync(message);
        if (faq is not null)
        {
            return Reply(faq.Answer, "Faq", quickReplies: DefaultQuickReplies);
        }

        if (ContainsAny(normalized, "where is my order", "track order", "order status"))
        {
            if (userId <= 0)
            {
                return Reply("Please log in so I can check your latest BUYWISE order.", "OrderTracking");
            }

            var order = await _orderRepository.GetLatestForUserAsync(userId);
            if (order is null)
            {
                return Reply("I could not find any orders for your account yet.", "OrderTracking");
            }

            return Reply(
                $"Your latest order #{order.Id} is {order.Status}. Tracking: {order.TrackingNumber ?? "not assigned yet"}. Estimated delivery: {order.EstimatedDelivery?.ToString("dd MMM yyyy") ?? "not available"}.",
                "OrderTracking",
                order: order);
        }

        if (ContainsAny(normalized, "cancel my order", "cancel order"))
        {
            if (userId <= 0)
            {
                return Reply("Please log in so I can find and cancel your eligible order.", "CancelOrder");
            }

            var latestOrder = await _orderRepository.GetLatestForUserAsync(userId);
            if (latestOrder is null)
            {
                return Reply("I could not find an order to cancel.", "CancelOrder");
            }

            var cancelled = await _orderRepository.CancelAsync(latestOrder.Id, userId);
            if (cancelled?.Status.Equals("Cancelled", StringComparison.OrdinalIgnoreCase) == true)
            {
                return Reply($"Order #{cancelled.Id} has been cancelled successfully.", "CancelOrder", order: cancelled);
            }

            return Reply($"Order #{latestOrder.Id} is {latestOrder.Status}, so it cannot be cancelled from chat.", "CancelOrder", order: latestOrder);
        }

        if (ContainsAny(normalized, "cart total", "show my cart", "what is in my cart", "basket total"))
        {
            if (userId <= 0)
            {
                return Reply("Please log in so I can read your BUYWISE cart.", "CartSummary");
            }

            var cart = await _cartRepository.GetAsync(userId);
            var cartText = cart.Items.Count == 0
                ? "Your cart is currently empty."
                : $"Your cart has {cart.Items.Sum(item => item.Quantity)} item(s), total Rs. {cart.Total:N0}.";

            return Reply(cartText, "CartSummary", cart: cart);
        }

        if (ContainsAny(normalized, "add this to cart", "add this to basket"))
        {
            if (userId <= 0)
            {
                return Reply("Please log in so I can update your cart.", "CartAdd");
            }

            if (request.CurrentProductId.GetValueOrDefault() <= 0)
            {
                return Reply("Open a product page first, then ask me to add this item to your cart.", "CartAdd");
            }

            var currentProductId = request.CurrentProductId.GetValueOrDefault();
            var cart = await _cartRepository.AddAsync(new CartUpsertRequest(userId, currentProductId, 1));
            await _activityRepository.RecordAsync(new UserActivityRequest(userId, currentProductId, "CartAdd", 1));
            return Reply("Added the current product to your BUYWISE cart.", "CartAdd", cart: cart);
        }

        if (ContainsAny(normalized, "what should i buy with", "bought together", "with this phone", "with this laptop"))
        {
            var productId = request.CurrentProductId ?? request.CartProductIds?.FirstOrDefault() ?? 0;
            if (productId <= 0)
            {
                return Reply("Open a product page or add an item to your cart, and I will suggest useful add-ons.", "FrequentlyBoughtTogether");
            }

            var products = await _recommendationService.GetFrequentlyBoughtTogetherAsync(productId, 4);
            return Reply("These products are frequently bought together by BUYWISE shoppers.", "FrequentlyBoughtTogether", products: products);
        }

        if (ContainsAny(normalized, "similar", "recommend products similar", "like this"))
        {
            var productId = request.CurrentProductId ?? request.CartProductIds?.FirstOrDefault() ?? 0;
            if (productId <= 0)
            {
                return Reply("Open a product detail page first, and I will compare similar products.", "SimilarProducts");
            }

            var products = await _recommendationService.GetSimilarProductsAsync(productId, request.CartProductIds, 5);
            return Reply("Here are similar products ranked by category, tags, brand, price range, and rating.", "SimilarProducts", products: products);
        }

        if (LooksLikeProductSearch(normalized))
        {
            var filters = BuildProductSearch(normalized);
            var products = await _recommendationService.SearchForShoppingAssistantAsync(filters, 6);
            if (products.Count == 0)
            {
                return Reply("I could not find products matching that request. Try a category like laptop, shoes, watch, phone, or appliance.", "ProductSearch");
            }

            return Reply($"I found {products.Count} product(s) that match your request.", "ProductSearch", products: products);
        }

        if (userId > 0)
        {
            var products = await _recommendationService.GetRecommendedForUserAsync(userId, 6);
            return Reply("Here are personalized picks based on your views, cart, and previous purchases.", "RecommendedForYou", products: products);
        }

        return Reply("I can help with product search, recommendations, order status, cart totals, cancellations, returns, delivery, payments, and warranty.", "Help", quickReplies: DefaultQuickReplies);
    }

    private static ProductSearchRequest BuildProductSearch(string normalized)
    {
        var categoryKeyword = DetectCategoryKeyword(normalized);
        var maxPrice = ExtractMaxPrice(normalized);
        var minRating = ExtractMinRating(normalized);

        return new ProductSearchRequest
        {
            Search = categoryKeyword.Search,
            MaxPrice = maxPrice,
            MinRating = minRating,
            Tags = categoryKeyword.Tags
        };
    }

    private static (string? Search, string? Tags) DetectCategoryKeyword(string normalized)
    {
        if ((normalized.Contains("shoe") && normalized.Contains("running"))
            || ContainsAny(normalized, "running shoes", "running shoe"))
        {
            return ("shoes", "running");
        }

        if (ContainsAny(normalized, "gaming laptop", "gaming laptops"))
        {
            return ("laptop", "gaming");
        }

        if (normalized.Contains("laptop"))
        {
            return ("laptop", null);
        }

        if (ContainsAny(normalized, "phone", "mobile"))
        {
            return ("phone", null);
        }

        if (normalized.Contains("shoe"))
        {
            return ("shoes", null);
        }

        if (ContainsAny(normalized, "watch", "smartwatch"))
        {
            return ("watch", null);
        }

        if (ContainsAny(normalized, "clothes", "tshirt", "hoodie", "jeans"))
        {
            return ("clothes", null);
        }

        if (ContainsAny(normalized, "appliance", "refrigerator", "vacuum", "air fryer", "mixer"))
        {
            return ("home", "appliance");
        }

        if (ContainsAny(normalized, "headphone", "earbuds", "speaker", "charger", "case", "keyboard", "bag"))
        {
            return (normalized, null);
        }

        return (normalized, null);
    }

    private static decimal? ExtractMaxPrice(string normalized)
    {
        if (!ContainsAny(normalized, "under", "below", "less than", "upto", "up to"))
        {
            return null;
        }

        var match = Regex.Match(normalized.Replace(",", string.Empty), @"\d+(\.\d+)?");
        return match.Success ? decimal.Parse(match.Value) : null;
    }

    private static double? ExtractMinRating(string normalized)
    {
        if (!normalized.Contains("rating"))
        {
            return null;
        }

        var match = Regex.Match(normalized, @"([3-5](\.\d)?)\s*(star|rating)");
        return match.Success ? double.Parse(match.Groups[1].Value) : null;
    }

    private static bool LooksLikeProductSearch(string normalized) =>
        ContainsAny(
            normalized,
            "show me",
            "suggest",
            "find",
            "search",
            "under",
            "below",
            "laptop",
            "phone",
            "mobile",
            "shoes",
            "watch",
            "appliance",
            "clothes",
            "headphone",
            "earbuds",
            "charger");

    private static bool ContainsAny(string value, params string[] terms) =>
        terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static ChatbotResponse Reply(
        string text,
        string intent,
        IReadOnlyList<RecommendationDto>? products = null,
        CartSummaryDto? cart = null,
        OrderSummaryDto? order = null,
        IReadOnlyList<string>? quickReplies = null) =>
        new(text, intent, products ?? Array.Empty<RecommendationDto>(), cart, order, quickReplies ?? DefaultQuickReplies);

    private static readonly string[] DefaultQuickReplies =
    [
        "Show me laptops under 50000",
        "Suggest running shoes",
        "What should I buy with this?",
        "Where is my order?",
        "Show my cart total",
        "Return policy"
    ];
}
