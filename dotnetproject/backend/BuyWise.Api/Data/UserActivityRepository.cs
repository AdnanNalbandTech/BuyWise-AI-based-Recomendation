using BuyWise.Api.Models;

namespace BuyWise.Api.Data;

public interface IUserActivityRepository
{
    Task RecordAsync(UserActivityRequest request);
    Task<IReadOnlyList<UserActivity>> GetRecentAsync(int userId, int take = 50);
    Task<IReadOnlyList<int>> GetRecentProductIdsAsync(int userId, int take = 20);
    Task<IReadOnlyList<int>> GetPurchasedProductIdsAsync(int userId, int take = 20);
}

public sealed class UserActivityRepository : IUserActivityRepository
{
    private readonly IConnectionFactory _connectionFactory;

    public UserActivityRepository(IConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task RecordAsync(UserActivityRequest request)
    {
        if (request.UserId <= 0 || request.ProductId <= 0 || string.IsNullOrWhiteSpace(request.ActivityType))
        {
            return;
        }

        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO user_activities (user_id, product_id, activity_type, quantity)
            VALUES (@UserId, @ProductId, @ActivityType, @Quantity);
            """;
        command.Parameters.AddWithValue("@UserId", request.UserId);
        command.Parameters.AddWithValue("@ProductId", request.ProductId);
        command.Parameters.AddWithValue("@ActivityType", request.ActivityType.Trim());
        command.Parameters.AddWithValue("@Quantity", Math.Max(1, request.Quantity));
        await command.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<UserActivity>> GetRecentAsync(int userId, int take = 50)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, user_id, product_id, activity_type, quantity, created_at
            FROM user_activities
            WHERE user_id = @UserId
            ORDER BY created_at DESC
            LIMIT @Take;
            """;
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@Take", Math.Clamp(take, 1, 100));

        var activities = new List<UserActivity>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            activities.Add(new UserActivity(
                reader.GetInt32("id"),
                reader.GetInt32("user_id"),
                reader.GetInt32("product_id"),
                reader.GetString("activity_type"),
                reader.GetInt32("quantity"),
                reader.GetDateTime("created_at")));
        }

        return activities;
    }

    public async Task<IReadOnlyList<int>> GetRecentProductIdsAsync(int userId, int take = 20)
    {
        var activities = await GetRecentAsync(userId, Math.Clamp(take * 3, 1, 100));
        return activities
            .Select(activity => activity.ProductId)
            .Distinct()
            .Take(take)
            .ToList();
    }

    public async Task<IReadOnlyList<int>> GetPurchasedProductIdsAsync(int userId, int take = 20)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT oi.product_id, MAX(o.created_at) AS latest_purchase
            FROM order_items oi
            INNER JOIN orders o ON o.id = oi.order_id
            WHERE o.user_id = @UserId
            GROUP BY oi.product_id
            ORDER BY latest_purchase DESC
            LIMIT @Take;
            """;
        command.Parameters.AddWithValue("@UserId", userId);
        command.Parameters.AddWithValue("@Take", Math.Clamp(take, 1, 100));

        var productIds = new List<int>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            productIds.Add(reader.GetInt32("product_id"));
        }

        return productIds;
    }
}
