using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace LiveSync.SignalR.Tests;

public class DatabaseConnectionTests
{
    [Fact]
    public async Task Database_DefaultConnection_ShouldConnect_AndExecuteScalar()
    {
        var connectionString = GetDatabaseConnectionString();
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        Assert.Equal(System.Data.ConnectionState.Open, connection.State);

        await using var command = new NpgsqlCommand("SELECT 1", connection);
        var result = await command.ExecuteScalarAsync();

        Assert.Equal(1, Convert.ToInt32(result));
    }

    private static string GetDatabaseConnectionString()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("LiveSync.Api.appsettings.json", optional: true)
            .AddJsonFile("LiveSync.Api.appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        return configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured for tests.");
    }
}
