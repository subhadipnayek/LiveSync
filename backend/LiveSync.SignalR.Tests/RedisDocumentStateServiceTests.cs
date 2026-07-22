using LiveSync.Services;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using Xunit;

namespace LiveSync.SignalR.Tests;

/// <summary>
/// Integration tests for RedisDocumentStateService.
/// These tests require the Redis container to be running (docker-compose up redis-backplane).
/// </summary>
public class RedisDocumentStateServiceTests : IAsyncLifetime
{
    private ConnectionMultiplexer _redis = null!;
    private RedisDocumentStateService _sut = null!;
    private IDatabase _db = null!;

    // Unique prefix per test run to avoid cross-test pollution
    private readonly string _testRunId = Guid.NewGuid().ToString("N")[..8];
    private string DocId(string name) => $"test-{_testRunId}-{name}";
    private string ConnId(string name) => $"conn-{_testRunId}-{name}";

    public async ValueTask InitializeAsync()
    {
        var connectionString = GetRedisConnectionString();
        var options = ConfigurationOptions.Parse(connectionString);
        options.AbortOnConnectFail = false;
        _redis = await ConnectionMultiplexer.ConnectAsync(options);
        _db = _redis.GetDatabase();
        _sut = new RedisDocumentStateService(_redis);
    }

    public async ValueTask DisposeAsync()
    {
        // Clean up all keys created during this test run
        var server = _redis.GetServer(_redis.GetEndPoints()[0]);
        var keys = server.Keys(pattern: $"livesync:*{_testRunId}*").ToArray();
        if (keys.Length > 0)
            await _db.KeyDeleteAsync(keys);

        await _redis.DisposeAsync();
    }

    // ── Presence ────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddUserToDocument_NewConnection_ReturnsTrueAndStoresAccess()
    {
        var doc = DocId("doc1");
        var conn = ConnId("conn1");

        var added = await _sut.AddUserToDocumentAsync(doc, conn, "Edit");

        Assert.True(added);
        Assert.Equal(1, await _sut.GetUserCountAsync(doc));
        Assert.Equal("Edit", await _sut.GetAccessAsync(conn, doc));
    }

    [Fact]
    public async Task AddUserToDocument_SameConnectionTwice_ReturnsFalseSecondTime()
    {
        var doc = DocId("doc2");
        var conn = ConnId("conn2");

        var first  = await _sut.AddUserToDocumentAsync(doc, conn, "Edit");
        var second = await _sut.AddUserToDocumentAsync(doc, conn, "Edit");

        Assert.True(first);
        Assert.False(second);
        Assert.Equal(1, await _sut.GetUserCountAsync(doc));
    }

    [Fact]
    public async Task RemoveUserFromDocument_ExistingConnection_ReturnsTrueAndDecrementsCount()
    {
        var doc   = DocId("doc3");
        var conn1 = ConnId("conn3a");
        var conn2 = ConnId("conn3b");

        await _sut.AddUserToDocumentAsync(doc, conn1, "Edit");
        await _sut.AddUserToDocumentAsync(doc, conn2, "View");

        var removed = await _sut.RemoveUserFromDocumentAsync(doc, conn1);

        Assert.True(removed);
        Assert.Equal(1, await _sut.GetUserCountAsync(doc));
    }

    [Fact]
    public async Task GetDocumentsForConnection_ReturnsAllJoinedDocs()
    {
        var conn = ConnId("conn4");
        var doc1 = DocId("doc4a");
        var doc2 = DocId("doc4b");

        await _sut.AddUserToDocumentAsync(doc1, conn, "Edit");
        await _sut.AddUserToDocumentAsync(doc2, conn, "View");

        var docs = await _sut.GetDocumentsForConnectionAsync(conn);

        Assert.Equal(2, docs.Count);
        Assert.Equal("Edit", docs[doc1]);
        Assert.Equal("View", docs[doc2]);
    }

    // ── Content ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task SetAndGetContent_RoundTripsCorrectly()
    {
        var doc     = DocId("doc5");
        var content = "Hello from Redis!";

        await _sut.SetContentAsync(doc, content);
        var result = await _sut.GetContentAsync(doc);

        Assert.Equal(content, result);
    }

    [Fact]
    public async Task GetContent_WhenNotSet_ReturnsNull()
    {
        var result = await _sut.GetContentAsync(DocId("doc-missing"));
        Assert.Null(result);
    }

    [Fact]
    public async Task DeleteContent_RemovesKey()
    {
        var doc = DocId("doc6");
        await _sut.SetContentAsync(doc, "some content");

        await _sut.DeleteContentAsync(doc);

        Assert.Null(await _sut.GetContentAsync(doc));
    }

    // ── Access ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAccess_AfterRemoveUser_ReturnsNull()
    {
        var doc  = DocId("doc7");
        var conn = ConnId("conn7");

        await _sut.AddUserToDocumentAsync(doc, conn, "Edit");
        await _sut.RemoveUserFromDocumentAsync(doc, conn);

        Assert.Null(await _sut.GetAccessAsync(conn, doc));
    }

    // ── Connection cleanup ────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveConnection_ClearsAllConnectionState()
    {
        var conn = ConnId("conn8");
        var doc  = DocId("doc8");

        await _sut.AddUserToDocumentAsync(doc, conn, "Edit");
        await _sut.SetColorAsync(conn, "#FF0000");

        await _sut.RemoveConnectionAsync(conn);

        var docs  = await _sut.GetDocumentsForConnectionAsync(conn);
        var color = await _sut.GetColorAsync(conn);

        Assert.Empty(docs);
        Assert.Null(color);
    }

    // ── Colour ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task SetAndGetColor_RoundTripsCorrectly()
    {
        var conn = ConnId("conn9");

        await _sut.SetColorAsync(conn, "#4ECDC4");

        Assert.Equal("#4ECDC4", await _sut.GetColorAsync(conn));
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

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
