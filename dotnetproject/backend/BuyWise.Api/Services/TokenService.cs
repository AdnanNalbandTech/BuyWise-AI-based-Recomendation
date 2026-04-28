using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BuyWise.Api.Models;

namespace BuyWise.Api.Services;

public sealed class TokenService
{
    private readonly byte[] _secret;

    public TokenService(IConfiguration configuration)
    {
        var secret = configuration["BuyWise:TokenSecret"];
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new InvalidOperationException("BuyWise:TokenSecret is missing.");
        }

        _secret = Encoding.UTF8.GetBytes(secret);
    }

    public string CreateToken(User user)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddHours(8);
        var payload = new TokenPayload(user.Id, user.Email, user.Role, expiresAt.ToUnixTimeSeconds());
        var payloadJson = JsonSerializer.Serialize(payload);
        var payloadPart = Base64UrlEncode(Encoding.UTF8.GetBytes(payloadJson));
        var signaturePart = Sign(payloadPart);

        return $"{payloadPart}.{signaturePart}";
    }

    public TokenPrincipal? ValidateToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var parts = token.Split('.', 2);
        if (parts.Length != 2)
        {
            return null;
        }

        var expectedSignature = Sign(parts[0]);
        if (!FixedTimeEquals(expectedSignature, parts[1]))
        {
            return null;
        }

        TokenPayload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<TokenPayload>(Encoding.UTF8.GetString(Base64UrlDecode(parts[0])));
        }
        catch
        {
            return null;
        }

        if (payload is null)
        {
            return null;
        }

        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(payload.Exp).UtcDateTime;
        if (expiresAt <= DateTime.UtcNow)
        {
            return null;
        }

        return new TokenPrincipal(payload.UserId, payload.Email, payload.Role, expiresAt);
    }

    public static string? ReadBearerToken(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";

        return header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? header[prefix.Length..].Trim()
            : null;
    }

    private string Sign(string payloadPart)
    {
        using var hmac = new HMACSHA256(_secret);
        return Base64UrlEncode(hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadPart)));
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length
            && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
        return Convert.FromBase64String(base64);
    }

    private sealed record TokenPayload(int UserId, string Email, string Role, long Exp);
}
