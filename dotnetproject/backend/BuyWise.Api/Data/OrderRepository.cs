using BuyWise.Api.Models;
using MySqlConnector;

namespace BuyWise.Api.Data;

public interface IOrderRepository
{
    Task<OrderResponse> CreateAsync(OrderRequest request);
    Task<IReadOnlyList<OrderSummaryDto>> GetUserOrdersAsync(int userId);
    Task<OrderSummaryDto?> GetLatestForUserAsync(int userId);
    Task<OrderSummaryDto?> GetByIdAsync(int orderId, int? userId = null);
    Task<OrderSummaryDto?> CancelAsync(int orderId, int userId);
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
        var estimatedDelivery = DateTime.UtcNow.Date.AddDays(5);
        var trackingNumber = $"BW{DateTime.UtcNow:yyyyMMddHHmmss}";

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await using var orderCommand = connection.CreateCommand();
        orderCommand.Transaction = transaction;
        orderCommand.CommandText = """
            INSERT INTO orders (user_id, full_name, email, shipping_address, total, status, tracking_number, estimated_delivery)
            VALUES (@UserId, @FullName, @Email, @ShippingAddress, @Total, 'Pending', @TrackingNumber, @EstimatedDelivery);
            SELECT LAST_INSERT_ID();
            """;
        orderCommand.Parameters.AddWithValue("@UserId", request.UserId > 0 ? request.UserId : DBNull.Value);
        orderCommand.Parameters.AddWithValue("@FullName", request.FullName.Trim());
        orderCommand.Parameters.AddWithValue("@Email", request.Email.Trim().ToLowerInvariant());
        orderCommand.Parameters.AddWithValue("@ShippingAddress", request.ShippingAddress.Trim());
        orderCommand.Parameters.AddWithValue("@Total", total);
        orderCommand.Parameters.AddWithValue("@TrackingNumber", trackingNumber);
        orderCommand.Parameters.AddWithValue("@EstimatedDelivery", estimatedDelivery);

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
        return new OrderResponse(orderId, total, DateTime.UtcNow, "Pending", trackingNumber, estimatedDelivery);
    }

    public async Task<IReadOnlyList<OrderSummaryDto>> GetUserOrdersAsync(int userId)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id
            FROM orders
            WHERE user_id = @UserId
            ORDER BY created_at DESC;
            """;
        command.Parameters.AddWithValue("@UserId", userId);

        var orderIds = new List<int>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                orderIds.Add(reader.GetInt32("id"));
            }
        }

        var orders = new List<OrderSummaryDto>();
        foreach (var orderId in orderIds)
        {
            var order = await GetByIdAsync(orderId, userId);
            if (order is not null)
            {
                orders.Add(order);
            }
        }

        return orders;
    }

    public async Task<OrderSummaryDto?> GetLatestForUserAsync(int userId)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id
            FROM orders
            WHERE user_id = @UserId
            ORDER BY created_at DESC
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("@UserId", userId);

        var result = await command.ExecuteScalarAsync();
        return result is null ? null : await GetByIdAsync(Convert.ToInt32(result), userId);
    }

    public async Task<OrderSummaryDto?> GetByIdAsync(int orderId, int? userId = null)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, total, status, tracking_number, estimated_delivery, created_at
            FROM orders
            WHERE id = @OrderId
              AND (@UserId IS NULL OR user_id = @UserId);
            """;
        command.Parameters.AddWithValue("@OrderId", orderId);
        command.Parameters.AddWithValue("@UserId", userId.HasValue ? userId.Value : DBNull.Value);

        OrderSummaryDto? order = null;
        await using (var reader = await command.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                order = new OrderSummaryDto(
                    reader.GetInt32("id"),
                    reader.GetDecimal("total"),
                    reader.GetString("status"),
                    reader.IsDBNull(reader.GetOrdinal("tracking_number")) ? null : reader.GetString("tracking_number"),
                    reader.IsDBNull(reader.GetOrdinal("estimated_delivery")) ? null : reader.GetDateTime("estimated_delivery"),
                    reader.GetDateTime("created_at"),
                    Array.Empty<OrderItemRequest>());
            }
        }

        if (order is null)
        {
            return null;
        }

        var items = await GetOrderItemsAsync(connection, orderId);
        return order with { Items = items };
    }

    public async Task<OrderSummaryDto?> CancelAsync(int orderId, int userId)
    {
        var order = await GetByIdAsync(orderId, userId);
        if (order is null)
        {
            return null;
        }

        if (!order.Status.Equals("Pending", StringComparison.OrdinalIgnoreCase)
            && !order.Status.Equals("Processing", StringComparison.OrdinalIgnoreCase))
        {
            return order;
        }

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE orders
            SET status = 'Cancelled',
                canceled_at = CURRENT_TIMESTAMP
            WHERE id = @OrderId AND user_id = @UserId;
            """;
        command.Parameters.AddWithValue("@OrderId", orderId);
        command.Parameters.AddWithValue("@UserId", userId);
        await command.ExecuteNonQueryAsync();

        return await GetByIdAsync(orderId, userId);
    }

    private static async Task<IReadOnlyList<OrderItemRequest>> GetOrderItemsAsync(MySqlConnection connection, int orderId)
    {
        await using var itemCommand = connection.CreateCommand();
        itemCommand.CommandText = """
            SELECT product_id, product_name, quantity, unit_price
            FROM order_items
            WHERE order_id = @OrderId;
            """;
        itemCommand.Parameters.AddWithValue("@OrderId", orderId);

        var items = new List<OrderItemRequest>();
        await using var reader = await itemCommand.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            items.Add(new OrderItemRequest(
                reader.GetInt32("product_id"),
                reader.GetString("product_name"),
                reader.GetInt32("quantity"),
                reader.GetDecimal("unit_price")));
        }

        return items;
    }
}
