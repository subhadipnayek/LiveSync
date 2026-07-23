namespace LiveSync.Services;

/// <summary>
/// Manages shared, distributed state for collaborative document sessions.
/// All implementations must be safe to call concurrently from multiple hub instances.
/// </summary>
public interface IDocumentStateService
{
    // ── Presence ────────────────────────────────────────────────────────────

    /// <summary>Adds a connection to a document's active user set and records its access level.</summary>
    /// <returns>true if the connection was newly added; false if it was already present.</returns>
    Task<bool> AddUserToDocumentAsync(string documentId, string connectionId, string accessLevel);

    /// <summary>Removes a connection from a document's active user set.</summary>
    /// <returns>true if the connection was removed; false if it was not present.</returns>
    Task<bool> RemoveUserFromDocumentAsync(string documentId, string connectionId);

    /// <summary>Returns the number of active connections currently in a document.</summary>
    Task<long> GetUserCountAsync(string documentId);

    /// <summary>
    /// Returns all documentIds the connection is currently joined to,
    /// together with the stored access level for each.
    /// Used during disconnect to clean up all documents at once.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> GetDocumentsForConnectionAsync(string connectionId);

    // ── Content snapshot ────────────────────────────────────────────────────

    /// <summary>Stores the latest full content snapshot for a document.</summary>
    Task SetContentAsync(string documentId, string content);

    /// <summary>Returns the latest content snapshot, or null if none exists yet.</summary>
    Task<string?> GetContentAsync(string documentId);

    /// <summary>Deletes the content snapshot (called when the last user leaves).</summary>
    Task DeleteContentAsync(string documentId);

    // ── Access ───────────────────────────────────────────────────────────────

    /// <summary>Returns the stored access level for a connection on a specific document, or null.</summary>
    Task<string?> GetAccessAsync(string connectionId, string documentId);

    /// <summary>Removes all state associated with a connection (called on disconnect).</summary>
    Task RemoveConnectionAsync(string connectionId);

    // ── Cursor colour ────────────────────────────────────────────────────────

    /// <summary>Stores the assigned cursor colour for a connection.</summary>
    Task SetColorAsync(string connectionId, string color);

    /// <summary>Returns the cursor colour for a connection, or null.</summary>
    Task<string?> GetColorAsync(string connectionId);

    // ── Operations for CRDT conflict resolution ──────────────────────────────

    /// <summary>
    /// Gets the IOperationLog service for managing operations.
    /// This is lazily retrieved and may be used during operations.
    /// </summary>
    IOperationLog GetOperationLog();
}
