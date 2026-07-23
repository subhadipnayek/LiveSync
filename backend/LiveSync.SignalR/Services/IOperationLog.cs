using LiveSync.Models;

namespace LiveSync.Services;

/// <summary>
/// Manages persistent storage and retrieval of operations for conflict resolution and resync.
/// Implementations must be safe to call concurrently from multiple hub instances.
/// </summary>
public interface IOperationLog
{
    /// <summary>
    /// Appends a new operation to the log for a specific document.
    /// Must be atomic and idempotent (same operation shouldn't be added twice).
    /// </summary>
    /// <param name="documentId">The document ID</param>
    /// <param name="operation">The operation to append</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task AppendOperationAsync(string documentId, Operation operation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all operations for a document with server revision greater than the specified value.
    /// Used for resync when a client has missed operations.
    /// </summary>
    /// <param name="documentId">The document ID</param>
    /// <param name="fromRevision">Only return operations with ServerRevision > this value (exclusive)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Operations in order of ServerRevision</returns>
    Task<IReadOnlyList<Operation>> GetOperationsSinceAsync(string documentId, long fromRevision, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all operations for a document (from the beginning).
    /// Used for state reconstruction.
    /// </summary>
    /// <param name="documentId">The document ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>All operations for the document in order</returns>
    Task<IReadOnlyList<Operation>> GetAllOperationsAsync(string documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current server revision (highest revision number) for a document.
    /// </summary>
    /// <param name="documentId">The document ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The current revision, or 0 if no operations exist</returns>
    Task<long> GetCurrentRevisionAsync(string documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all operations for a document (called when the last user leaves).
    /// </summary>
    /// <param name="documentId">The document ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteOperationsAsync(string documentId, CancellationToken cancellationToken = default);
}
