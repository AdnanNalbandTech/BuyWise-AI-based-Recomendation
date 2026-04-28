using MySqlConnector;

namespace BuyWise.Api.Data;

public sealed class MySqlConnectionFactory : IConnectionFactory
{
    private readonly IConfiguration _configuration;

    public MySqlConnectionFactory(IConfiguration configuration)
    {
        _configuration = configuration;
        ConnectionString = _configuration.GetConnectionString("BuyWiseDb")
            ?? throw new InvalidOperationException("Connection string 'BuyWiseDb' is missing.");
    }

    public string ConnectionString { get; }

    public MySqlConnection CreateConnection() => new(ConnectionString);
}
