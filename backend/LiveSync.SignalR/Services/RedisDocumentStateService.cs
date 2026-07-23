using StackExchange.Redis;

namespace LiveSync.Services;

/// <summary>
/// Redis-backed implementation of <see cref="IDocumentStateService"/>.
///
/// Key schema:
///   livesync:doc:{docId}:users          Redis Set  — connectionIds present in the document
///   livesync:doc:{docId}:content        Redis String — latest full content snapshot
///   livesync:conn:{connId}:docs         Redis Hash   — documentId → accessLevel (all docs this connection joined)
///   livesync:conn:{connId}:color        Redis String — assigned cursor colour
///
/// Connection-scoped keys carry a 24-hour TTL as a safety net against orphaned
/// entries if a server crashes without firing OnDisconnectedAsync.
/// </summary>
public sealed class RedisDocumentStateService : IDocumentStateService
{
    private readonly IDatabase _db;
    private readonly IOperationLog _operationLog;
    private static readonly TimeSpan ConnKeyTtl = TimeSpan.FromHours(24);

    public RedisDocumentStateService(IConnectionMultiplexer redis, IOperationLog operationLog)
    {
        _db = redis.GetDatabase();
        _operationLog = operationLog;
    }

    // ── Key helpers ─────────────────────────────────────────────────────────

    private static string DocUsersKey(string docId)   => $"livesync:doc:{docId}:users";
    private static string DocContentKey(string docId) => $"livesync:doc:{docId}:content";
    private static string ConnDocsKey(string connId)  => $"livesync:conn:{connId}:docs";
    private static string ConnColorKey(string connId) => $"livesync:conn:{connId}:color";

    // ── Presence ────────────────────────────────────────────────────────────

    public async Task<bool> AddUserToDocumentAsync(string documentId, string connectionId, string accessLevel)
    {
        var added = await _db.SetAddAsync(DocUsersKey(documentId), connectionId);

        // Always (re-)write the access level in the connection's doc hash so a
        // reconnect after a partial cleanup still has the correct access recorded.
        await _db.HashSetAsync(ConnDocsKey(connectionId), documentId, accessLevel);
        await _db.KeyExpireAsync(ConnDocsKey(connectionId), ConnKeyTtl);

        return added;
    }

    public async Task<bool> RemoveUserFromDocumentAsync(string documentId, string connectionId)
    {
        var removed = await _db.SetRemoveAsync(DocUsersKey(documentId), connectionId);
        await _db.HashDeleteAsync(ConnDocsKey(connectionId), documentId);
        return removed;
    }

    public async Task<long> GetUserCountAsync(string documentId)
        => await _db.SetLengthAsync(DocUsersKey(documentId));

    public async Task<IReadOnlyDictionary<string, string>> GetDocumentsForConnectionAsync(string connectionId)
    {
        var entries = await _db.HashGetAllAsync(ConnDocsKey(connectionId));
        return entries.ToDictionary(e => e.Name.ToString(), e => e.Value.ToString());
    }

    // ── Content snapshot ────────────────────────────────────────────────────

    public async Task SetContentAsync(string documentId, string content)
        => await _db.StringSetAsync(DocContentKey(documentId), content);

    public async Task<string?> GetContentAsync(string documentId)
    {
        var value = await _db.StringGetAsync(DocContentKey(documentId));
        return value.HasValue ? value.ToString() : null;
    }

    public async Task DeleteContentAsync(string documentId)
        => await _db.KeyDeleteAsync(DocContentKey(documentId));

    // ── Access ───────────────────────────────────────────────────────────────

    public async Task<string?> GetAccessAsync(string connectionId, string documentId)
    {
        var value = await _db.HashGetAsync(ConnDocsKey(connectionId), documentId);
        return value.HasValue ? value.ToString() : null;
    }

    public async Task RemoveConnectionAsync(string connectionId)
    {
        await _db.KeyDeleteAsync(ConnDocsKey(connectionId));
        await _db.KeyDeleteAsync(ConnColorKey(connectionId));
    }

    // ── Cursor colour ────────────────────────────────────────────────────────

    public async Task SetColorAsync(string connectionId, string color)
        => await _db.StringSetAsync(ConnColorKey(connectionId), color, ConnKeyTtl);

    public async Task<string?> GetColorAsync(string connectionId)
    {
        var value = await _db.StringGetAsync(ConnColorKey(connectionId));
        return value.HasValue ? value.ToString() : null;
    }

    // ── Operations for CRDT conflict resolution ──────────────────────────────

    public IOperationLog GetOperationLog()
        => _operationLog;
}
