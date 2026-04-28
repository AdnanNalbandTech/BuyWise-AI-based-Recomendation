using MySqlConnector;

namespace BuyWise.Api.Data;

public interface IConnectionFactory
{
    MySqlConnection CreateConnection();
    string ConnectionString { get; }
}
