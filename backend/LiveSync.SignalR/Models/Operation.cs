namespace LiveSync.Models;

/// <summary>
/// Base class for collaborative editing operations in a CRDT system.
/// Each operation has a unique identifier, revision metadata, and timestamp for causal ordering.
/// </summary>
public abstract record Operation
{
    /// <summary>
    /// Unique identifier for this operation (site ID + logical clock).
    /// Ensures global uniqueness and deterministic ordering across all replicas.
    /// </summary>
    public required OperationId Id { get; init; }

    /// <summary>
    /// Client-side revision number when this operation was generated.
    /// Helps clients track which operations they have seen.
    /// </summary>
    public required long ClientRevision { get; init; }

    /// <summary>
    /// Server-side revision number assigned when the operation was accepted.
    /// Used for resync and operation sequencing on the server.
    /// </summary>
    public required long ServerRevision { get; init; }

    /// <summary>
    /// Timestamp when the operation was created (client-side clock).
    /// Used for causality tracking and debugging.
    /// </summary>
    public required DateTime Timestamp { get; init; }
}

/// <summary>
/// Insert operation: adds one or more characters at a specific position.
/// </summary>
public record InsertOperation : Operation
{
    /// <summary>
    /// The position in the document where the text should be inserted.
    /// </summary>
    public required int Position { get; init; }

    /// <summary>
    /// The text content to insert.
    /// </summary>
    public required string Text { get; init; }
}

/// <summary>
/// Delete operation: removes one or more characters starting at a specific position.
/// </summary>
public record DeleteOperation : Operation
{
    /// <summary>
    /// The position in the document where deletion starts.
    /// </summary>
    public required int Position { get; init; }

    /// <summary>
    /// The number of characters to delete.
    /// </summary>
    public required int Length { get; init; }
}
