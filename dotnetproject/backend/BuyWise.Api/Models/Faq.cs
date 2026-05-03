namespace BuyWise.Api.Models;

public sealed record FaqDto(
    int Id,
    string Question,
    string Answer,
    string Keywords);
