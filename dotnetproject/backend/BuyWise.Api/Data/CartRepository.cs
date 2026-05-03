using BuyWise.Api.Models;

namespace BuyWise.Api.Data;

public interface ICartRepository
{
    Task<CartSummaryDto> GetAsync(int userId);
    Task<CartSummaryDto> AddAsync(CartUpsertRequest request);
    Task<CartSummaryDto> UpdateQuantityAsync(int userId, int productId, int quantity);
    Task<CartSummaryDto> RemoveAsync(int userId, int productId);
    Task ClearAsync(int userId);
}

public sealed class CartRepository : ICartRepository
{
    private readonly IConnectionFactory _connectionFactory;

    public CartRepository(IConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<CartSummaryDto> GetAsync(int userId)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ci.product_id, p.name, p.price, p.image_url, c.name AS category_name, p.brand, ci.quantity
            FROM cart_items ci
            INNER JOIN products p ON p.id = ci.product_id
            INNER JOIN categories c ON c.id = p.category_id
            WHERE ci.user_id = @UserId
            ORDER BY ci.updated_at DESC;
            """;
        command.Parameters.AddWithValue("@UserId", userId);

        var items = new List<CartItemDto>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var price = reader.GetDecimal("price");
            var quantity = reader.GetInt32("quantity");
            items.Add(new CartItemDto(
                reader.GetInt32("product_id"),
                reader.GetString("name"),
                price,
                reader.GetString("image_url"),
                reader.GetString("category_name"),
                reader.GetString("brand"),
                quantity,
                price * quantity));
        }

        return new CartSummaryDto(userId, items, items.Sum(item => item.LineTotal));
    }

    public async Task<CartSummaryDto> AddAsync(CartUpsertRequest request)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO cart_items (user_id, product_id, quantity)
            VALUES (@UserId, @ProductId, @Quantity)
            ON DUPLICATE KEY UPDATE quantity = quantity + @Quantity;
            """;
        command.Parameters.AddWithValue("@UserId", request.UserId);
        command.Parameters.AddWithValue("@ProductId", request.ProductId);
        command.Parameters.AddWithValue("@Quantity", Math.Max(1, request.Quantity));
        await command.ExecuteNonQueryAsync();

        return await GetAsync(request.UserId);
    }

    public async Task<CartSummaryDto> UpdateQuantityAsync(int userId, int productId, int quantity)
    {
        if (quantity <= 0)
        {
            return await RemoveAsync(userId, productId);
        }

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE cart_items
            SET quantity = @Quantity
            WHERE user_id = @UserId AND product_id = @ProductId;
            """;
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@ProductId", productId);
        command.Parameters.AddWithValue("@Quantity", quantity);
        await command.ExecuteNonQueryAsync();

        return await GetAsync(userId);
    }

    public async Task<CartSummaryDto> RemoveAsync(int userId, int productId)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM cart_items WHERE user_id = @UserId AND product_id = @ProductId;";
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@ProductId", productId);
        await command.ExecuteNonQueryAsync();

        return await GetAsync(userId);
    }

    public async Task ClearAsync(int userId)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM cart_items WHERE user_id = @UserId;";
        command.Parameters.AddWithValue("@UserId", userId);
        await command.ExecuteNonQueryAsync();
    }
}
