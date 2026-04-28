using BuyWise.Api.Models;

namespace BuyWise.Api.Data;

public interface IOrderRepository
{
    Task<OrderResponse> CreateAsync(OrderRequest request);
}

public sealed class OrderRepository : IOrderRepository
{
    private readonly IConnectionFactory _connectionFactory;

    public OrderRepository(IConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<OrderResponse> CreateAsync(OrderRequest request)
    {
        if (request.Items.Count == 0)
        {
            throw new InvalidOperationException("An order must contain at least one item.");
        }

        var total = request.Items.Sum(item => item.UnitPrice * item.Quantity);

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await using var orderCommand = connection.CreateCommand();
        orderCommand.Transaction = transaction;
        orderCommand.CommandText = """
            INSERT INTO orders (user_id, full_name, email, shipping_address, total)
            VALUES (@UserId, @FullName, @Email, @ShippingAddress, @Total);
            SELECT LAST_INSERT_ID();
            """;
        orderCommand.Parameters.AddWithValue("@UserId", request.UserId > 0 ? request.UserId : DBNull.Value);
        orderCommand.Parameters.AddWithValue("@FullName", request.FullName.Trim());
        orderCommand.Parameters.AddWithValue("@Email", request.Email.Trim().ToLowerInvariant());
        orderCommand.Parameters.AddWithValue("@ShippingAddress", request.ShippingAddress.Trim());
        orderCommand.Parameters.AddWithValue("@Total", total);

        var orderId = Convert.ToInt32(await orderCommand.ExecuteScalarAsync());

        foreach (var item in request.Items)
        {
            await using var itemCommand = connection.CreateCommand();
            itemCommand.Transaction = transaction;
            itemCommand.CommandText = """
                INSERT INTO order_items (order_id, product_id, product_name, quantity, unit_price)
                VALUES (@OrderId, @ProductId, @ProductName, @Quantity, @UnitPrice);
                """;
            itemCommand.Parameters.AddWithValue("@OrderId", orderId);
            itemCommand.Parameters.AddWithValue("@ProductId", item.ProductId);
            itemCommand.Parameters.AddWithValue("@ProductName", item.ProductName);
            itemCommand.Parameters.AddWithValue("@Quantity", item.Quantity);
            itemCommand.Parameters.AddWithValue("@UnitPrice", item.UnitPrice);
            await itemCommand.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
        return new OrderResponse(orderId, total, DateTime.UtcNow);
    }
}
