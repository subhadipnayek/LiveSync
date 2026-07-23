using System.Text.Json;
using LiveSync.Models;
using StackExchange.Redis;

namespace LiveSync.Services;

/// <summary>
/// Redis-backed operation log for CRDT conflict resolution.
/// Stores operations as JSON in sorted sets, keyed by document ID.
/// Uses server revision as the sort score to maintain ordering.
/// Thread-safe for concurrent access.
/// </summary>
public class RedisOperationLog : IOperationLog
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisOperationLog> _logger;

    private const string OperationLogKeyPrefix = "doc:operations:";
    private const string CurrentRevisionKeyPrefix = "doc:revision:";

    public RedisOperationLog(IConnectionMultiplexer redis, ILogger<RedisOperationLog> logger)
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task AppendOperationAsync(string documentId, Operation operation, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentId))
            throw new ArgumentException("Document ID cannot be null or empty", nameof(documentId));

        if (operation == null)
            throw new ArgumentNullException(nameof(operation));

        var db = _redis.GetDatabase();
        var operationKey = GetOperationLogKey(documentId);
        var revisionKey = GetCurrentRevisionKey(documentId);

        // Serialize the operation to JSON
        var json = SerializeOperation(operation);

        // Use a transaction to atomically:
        // 1. Add operation to sorted set (score = ServerRevision)
        // 2. Update the current revision counter
        var transaction = db.CreateTransaction();

        // Add operation to sorted set with ServerRevision as score
        transaction.SortedSetAddAsync(
            operationKey,
            json,
            operation.ServerRevision);

        // Update current revision
        transaction.StringSetAsync(
            revisionKey,
            operation.ServerRevision.ToString());

        try
        {
            var success = await transaction.ExecuteAsync();
            if (!success)
            {
                _logger.LogWarning("Failed to append operation to log for document {DocumentId}", documentId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error appending operation to log for document {DocumentId}", documentId);
            throw;
        }
    }

    public async Task<IReadOnlyList<Operation>> GetOperationsSinceAsync(string documentId, long fromRevision, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentId))
            throw new ArgumentException("Document ID cannot be null or empty", nameof(documentId));

        var db = _redis.GetDatabase();
        var operationKey = GetOperationLogKey(documentId);

        try
        {
            // Get operations with ServerRevision in range (fromRevision, +inf)
            // ZRANGEBYSCORE returns in ascending order by default (which is what we want)
            var entries = await db.SortedSetRangeByScoreAsync(
                operationKey,
                fromRevision + 1,  // Exclusive lower bound
                double.PositiveInfinity);

            var operations = new List<Operation>();
            foreach (var entry in entries)
            {
                if (entry.IsNull)
                    continue;

                var operation = DeserializeOperation(entry.ToString());
                if (operation != null)
                    operations.Add(operation);
            }

            return operations.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving operations since revision {FromRevision} for document {DocumentId}",
                fromRevision, documentId);
            throw;
        }
    }

    public async Task<IReadOnlyList<Operation>> GetAllOperationsAsync(string documentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentId))
            throw new ArgumentException("Document ID cannot be null or empty", nameof(documentId));

        var db = _redis.GetDatabase();
        var operationKey = GetOperationLogKey(documentId);

        try
        {
            // Get all operations (from score 0 to +inf)
            var entries = await db.SortedSetRangeByScoreAsync(
                operationKey,
                0,
                double.PositiveInfinity);

            var operations = new List<Operation>();
            foreach (var entry in entries)
            {
                if (entry.IsNull)
                    continue;

                var operation = DeserializeOperation(entry.ToString());
                if (operation != null)
                    operations.Add(operation);
            }

            return operations.AsReadOnly();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all operations for document {DocumentId}", documentId);
            throw;
        }
    }

    public async Task<long> GetCurrentRevisionAsync(string documentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentId))
            throw new ArgumentException("Document ID cannot be null or empty", nameof(documentId));

        var db = _redis.GetDatabase();
        var revisionKey = GetCurrentRevisionKey(documentId);

        try
        {
            var revisionValue = await db.StringGetAsync(revisionKey);
            if (revisionValue.IsNull)
                return 0;

            if (long.TryParse(revisionValue.ToString(), out var revision))
                return revision;

            _logger.LogWarning("Invalid revision value for document {DocumentId}: {Value}", documentId, revisionValue);
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving current revision for document {DocumentId}", documentId);
            throw;
        }
    }

    public async Task DeleteOperationsAsync(string documentId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(documentId))
            throw new ArgumentException("Document ID cannot be null or empty", nameof(documentId));

        var db = _redis.GetDatabase();
        var operationKey = GetOperationLogKey(documentId);
        var revisionKey = GetCurrentRevisionKey(documentId);

        try
        {
            var transaction = db.CreateTransaction();
            transaction.KeyDeleteAsync(operationKey);
            transaction.KeyDeleteAsync(revisionKey);

            var success = await transaction.ExecuteAsync();
            if (!success)
            {
                _logger.LogWarning("Failed to delete operations for document {DocumentId}", documentId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting operations for document {DocumentId}", documentId);
            throw;
        }
    }

    private string SerializeOperation(Operation operation)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        return JsonSerializer.Serialize(operation, operation.GetType(), options);
    }

    private Operation? DeserializeOperation(string json)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            // Try to determine the operation type by looking for "position" and "text" or "length"
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Check if this is an Insert or Delete
            if (root.TryGetProperty("text", out _))
            {
                return JsonSerializer.Deserialize<InsertOperation>(json, options);
            }
            else if (root.TryGetProperty("length", out _))
            {
                return JsonSerializer.Deserialize<DeleteOperation>(json, options);
            }

            _logger.LogWarning("Unable to determine operation type from JSON: {Json}", json);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deserializing operation: {Json}", json);
            return null;
        }
    }

    private string GetOperationLogKey(string documentId) => $"{OperationLogKeyPrefix}{documentId}";
    private string GetCurrentRevisionKey(string documentId) => $"{CurrentRevisionKeyPrefix}{documentId}";
}
