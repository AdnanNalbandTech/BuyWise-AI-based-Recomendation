namespace BuyWise.Api.Models;

public sealed record User(
    int Id,
    string FullName,
    string Email,
    string PasswordHash,
    string Role,
    DateTime CreatedAt);

public sealed record PublicUser(
    int Id,
    string FullName,
    string Email,
    string Role);

public sealed record RegisterRequest(
    string FullName,
    string Email,
    string Password);

public sealed record LoginRequest(
    string Email,
    string Password);

public sealed record AuthResponse(
    string Token,
    PublicUser User);

public sealed record TokenPrincipal(
    int UserId,
    string Email,
    string Role,
    DateTime ExpiresAt);
