using BuyWise.Api.Models;

namespace BuyWise.Api.Data;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByIdAsync(int id);
    Task<User> CreateAsync(RegisterRequest request, string passwordHash);
}

public sealed class UserRepository : IUserRepository
{
    private readonly IConnectionFactory _connectionFactory;

    public UserRepository(IConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, full_name, email, password_hash, role, created_at
            FROM users
            WHERE email = @Email;
            """;
        command.Parameters.AddWithValue("@Email", email.Trim().ToLowerInvariant());

        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? MapUser(reader) : null;
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT id, full_name, email, password_hash, role, created_at
            FROM users
            WHERE id = @Id;
            """;
        command.Parameters.AddWithValue("@Id", id);

        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? MapUser(reader) : null;
    }

    public async Task<User> CreateAsync(RegisterRequest request, string passwordHash)
    {
        await using var connection = _connectionFactory.CreateConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO users (full_name, email, password_hash, role)
            VALUES (@FullName, @Email, @PasswordHash, 'Customer');
            SELECT LAST_INSERT_ID();
            """;
        command.Parameters.AddWithValue("@FullName", request.FullName.Trim());
        command.Parameters.AddWithValue("@Email", request.Email.Trim().ToLowerInvariant());
        command.Parameters.AddWithValue("@PasswordHash", passwordHash);

        var id = Convert.ToInt32(await command.ExecuteScalarAsync());
        return await GetByIdAsync(id)
            ?? throw new InvalidOperationException("User was created but could not be loaded.");
    }

    private static User MapUser(MySqlConnector.MySqlDataReader reader) => new(
        reader.GetInt32("id"),
        reader.GetString("full_name"),
        reader.GetString("email"),
        reader.GetString("password_hash"),
        reader.GetString("role"),
        reader.GetDateTime("created_at"));
}
