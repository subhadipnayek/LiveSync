using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using Xunit;

namespace LiveSync.SignalR.Tests;

public class RedisBackplaneConnectionTests
{
    [Fact]
    public async Task Redis_Backplane_ConnectionString_ShouldConnect_AndReadWrite()
    {
        var connectionString = GetRedisConnectionString();
        Assert.False(string.IsNullOrWhiteSpace(connectionString));

        var options = ConfigurationOptions.Parse(connectionString);
        options.AbortOnConnectFail = false;
        options.ConnectRetry = 2;
        options.ConnectTimeout = 3000;
        options.SyncTimeout = 3000;

        using var connection = await ConnectionMultiplexer.ConnectAsync(options);
        Assert.True(connection.IsConnected);

        var db = connection.GetDatabase();
        var key = $"livesync:redis-test:{Guid.NewGuid():N}";

        var setResult = await db.StringSetAsync(key, "ok", TimeSpan.FromSeconds(30));
        Assert.True(setResult);

        var value = await db.StringGetAsync(key);
        Assert.Equal("ok", value.ToString());
    }

    private static string GetRedisConnectionString()
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("LiveSync.SignalR.appsettings.json", optional: true)
            .AddJsonFile("LiveSync.SignalR.appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        return configuration["Redis:ConnectionString"]
            ?? throw new InvalidOperationException("Redis:ConnectionString is not configured for tests.");
    }
}
