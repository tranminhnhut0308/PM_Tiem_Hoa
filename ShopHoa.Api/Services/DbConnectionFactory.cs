using System.Data;
using MySqlConnector;

namespace ShopHoa.Api.Services;

public interface IDbConnectionFactory
{
    Task<MySqlConnection> CreateConnectionAsync();
}

public sealed class DbConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public DbConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Chuỗi kết nối 'DefaultConnection' không được tìm thấy trong cấu hình.");
    }

    public async Task<MySqlConnection> CreateConnectionAsync()
    {
        var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }
}
