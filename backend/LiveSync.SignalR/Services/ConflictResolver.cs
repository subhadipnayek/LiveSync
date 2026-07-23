using LiveSync.Models;

namespace LiveSync.Services;

/// <summary>
/// Implements conflict-free document state management using CRDT (Conflict-free Replicated Data Type) principles.
/// Handles operation transformation, application, and composition to ensure consistency across replicas.
/// This class is stateless and fully testable — it should not depend on any external services.
/// </summary>
public class ConflictResolver
{
    /// <summary>
    /// Applies a single operation to document content.
    /// Handles both Insert and Delete operations with proper position adjustment.
    /// </summary>
    /// <param name="content">The current document content</param>
    /// <param name="operation">The operation to apply</param>
    /// <returns>The updated document content</returns>
    public string ApplyOperation(string content, Operation operation)
    {
        return operation switch
        {
            InsertOperation insert => ApplyInsert(content, insert),
            DeleteOperation delete => ApplyDelete(content, delete),
            _ => throw new ArgumentException($"Unknown operation type: {operation.GetType()}")
        };
    }

    /// <summary>
    /// Applies a sequence of operations to the document, reconstructing its final state.
    /// Used during resync or when rebuilding state from operation log.
    /// </summary>
    /// <param name="content">The initial document content (usually empty or from snapshot)</param>
    /// <param name="operations">Ordered sequence of operations to apply</param>
    /// <returns>The final document state after all operations applied</returns>
    public string ApplyOperations(string content, IEnumerable<Operation> operations)
    {
        return operations.Aggregate(content, ApplyOperation);
    }

    /// <summary>
    /// Transforms two operations that were generated concurrently (independent of each other)
    /// so they can be applied in either order and produce the same final result.
    /// 
    /// This implements operational transformation (OT) for CRDT, specifically:
    /// Transform(A, B) returns A' such that:
    ///   Apply(Apply(doc, A), Transform(A, B)) == Apply(Apply(doc, B), Transform(B, A))
    /// 
    /// This is the key algorithm that ensures consistency.
    /// </summary>
    /// <param name="op1">First operation (will be transformed against op2)</param>
    /// <param name="op2">Second operation (concurrent with op1)</param>
    /// <returns>Transformed version of op1 that is compatible with op2 already applied</returns>
    public Operation TransformAgainstConcurrent(Operation op1, Operation op2)
    {
        // Use stable ordering by operation ID to ensure deterministic results
        var shouldSwap = op1.Id.CompareTo(op2.Id) > 0;
        var (first, second) = shouldSwap ? (op2, op1) : (op1, op2);

        // Transform first against second (always in same order for determinism)
        var transformed = TransformInternal(first, second);

        // If we swapped, return the transformed second; else return transformed first
        return shouldSwap ? TransformInternal(op2, transformed) : transformed;
    }

    /// <summary>
    /// Internal transform implementation that handles all operation type combinations.
    /// Assumes operations are processed in a consistent order.
    /// </summary>
    private Operation TransformInternal(Operation baseTx, Operation concurrentTx)
    {
        return (baseTx, concurrentTx) switch
        {
            // Insert transforming against Insert
            (InsertOperation baseInsert, InsertOperation concurrentInsert) =>
                TransformInsertAgainstInsert(baseInsert, concurrentInsert),

            // Insert transforming against Delete
            (InsertOperation baseInsert, DeleteOperation concurrentDelete) =>
                TransformInsertAgainstDelete(baseInsert, concurrentDelete),

            // Delete transforming against Insert
            (DeleteOperation baseDelete, InsertOperation concurrentInsert) =>
                TransformDeleteAgainstInsert(baseDelete, concurrentInsert),

            // Delete transforming against Delete
            (DeleteOperation baseDelete, DeleteOperation concurrentDelete) =>
                TransformDeleteAgainstDelete(baseDelete, concurrentDelete),

            _ => throw new ArgumentException(
                $"Unknown operation combination: {baseTx.GetType().Name}, {concurrentTx.GetType().Name}")
        };
    }

    private Operation TransformInsertAgainstInsert(InsertOperation baseInsert, InsertOperation concurrentInsert)
    {
        // When two inserts happen at the same position, use operation ID for tie-breaking
        // This ensures deterministic behavior across all replicas
        if (baseInsert.Position == concurrentInsert.Position)
        {
            // If concurrent insert has lower ID, it "wins" and shifts our position right
            if (concurrentInsert.Id.CompareTo(baseInsert.Id) < 0)
                return baseInsert with { Position = baseInsert.Position + concurrentInsert.Text.Length };

            // Our ID is lower, position stays the same
            return baseInsert;
        }

        // If concurrent insert is before our position, shift us right
        if (concurrentInsert.Position < baseInsert.Position)
            return baseInsert with { Position = baseInsert.Position + concurrentInsert.Text.Length };

        // Concurrent insert is after our position, no change needed
        return baseInsert;
    }

    private Operation TransformInsertAgainstDelete(InsertOperation baseInsert, DeleteOperation concurrentDelete)
    {
        var deleteStart = concurrentDelete.Position;
        var deleteEnd = concurrentDelete.Position + concurrentDelete.Length;

        // If our insert is before the deletion, no change
        if (baseInsert.Position < deleteStart)
            return baseInsert;

        // If our insert is within the deleted range, shift it to the start of deletion
        if (baseInsert.Position >= deleteStart && baseInsert.Position <= deleteEnd)
            return baseInsert with { Position = deleteStart };

        // If our insert is after the deleted range, shift it left by deletion length
        return baseInsert with { Position = baseInsert.Position - concurrentDelete.Length };
    }

    private Operation TransformDeleteAgainstInsert(DeleteOperation baseDelete, InsertOperation concurrentInsert)
    {
        var deleteStart = baseDelete.Position;
        var deleteEnd = baseDelete.Position + baseDelete.Length;

        // If insert is before our deletion, shift our deletion right
        if (concurrentInsert.Position < deleteStart)
            return baseDelete with { Position = baseDelete.Position + concurrentInsert.Text.Length };

        // If insert is within our deletion range, extend our deletion to include inserted text
        if (concurrentInsert.Position >= deleteStart && concurrentInsert.Position <= deleteEnd)
            return baseDelete with { Length = baseDelete.Length + concurrentInsert.Text.Length };

        // If insert is after our deletion, no change to position or length
        return baseDelete;
    }

    private Operation TransformDeleteAgainstDelete(DeleteOperation baseDelete, DeleteOperation concurrentDelete)
    {
        var baseStart = baseDelete.Position;
        var baseEnd = baseDelete.Position + baseDelete.Length;
        var concurrentStart = concurrentDelete.Position;
        var concurrentEnd = concurrentDelete.Position + concurrentDelete.Length;

        // Case 1: Deletions don't overlap
        if (baseEnd <= concurrentStart)
        {
            // Our deletion is entirely before concurrent, no change
            return baseDelete;
        }

        if (baseStart >= concurrentEnd)
        {
            // Our deletion is entirely after concurrent, shift left by concurrent length
            return baseDelete with { Position = baseDelete.Position - concurrentDelete.Length };
        }

        // Case 2: Deletions overlap or one contains the other
        // Calculate the overlap
        var overlapStart = Math.Max(baseStart, concurrentStart);
        var overlapEnd = Math.Min(baseEnd, concurrentEnd);
        var overlapLength = Math.Max(0, overlapEnd - overlapStart);

        // Calculate how much of our deletion is before the concurrent deletion
        var beforeLength = Math.Max(0, concurrentStart - baseStart);

        // Calculate how much of our deletion is after the concurrent deletion
        var afterLength = Math.Max(0, baseEnd - concurrentEnd);

        // New position is where it started, adjusted if concurrent started before us
        var newPosition = baseStart < concurrentStart ? baseStart : concurrentStart;

        // New length accounts for the parts that don't overlap
        var newLength = beforeLength + afterLength;

        return baseDelete with 
        { 
            Position = newPosition,
            Length = Math.Max(0, newLength) // Ensure non-negative
        };
    }

    /// <summary>
    /// Applies an insert operation to the document.
    /// </summary>
    private string ApplyInsert(string content, InsertOperation insert)
    {
        if (insert.Position < 0 || insert.Position > content.Length)
            throw new ArgumentException(
                $"Insert position {insert.Position} is out of bounds for content of length {content.Length}");

        return content.Insert(insert.Position, insert.Text);
    }

    /// <summary>
    /// Applies a delete operation to the document.
    /// </summary>
    private string ApplyDelete(string content, DeleteOperation delete)
    {
        if (delete.Position < 0 || delete.Position > content.Length)
            throw new ArgumentException(
                $"Delete position {delete.Position} is out of bounds for content of length {content.Length}");

        var endPosition = Math.Min(delete.Position + delete.Length, content.Length);
        var actualLength = endPosition - delete.Position;

        if (actualLength <= 0)
            return content; // Nothing to delete

        return content.Remove(delete.Position, actualLength);
    }
}
