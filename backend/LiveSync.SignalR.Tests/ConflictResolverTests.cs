using LiveSync.Models;
using LiveSync.Services;
using Xunit;

namespace LiveSync.Tests;

/// <summary>
/// Comprehensive tests for the ConflictResolver CRDT operational transformation logic.
/// Tests cover all operation combinations and edge cases to ensure consistency.
/// </summary>
public class ConflictResolverTests
{
    private readonly ConflictResolver _resolver = new();

    #region ApplyOperation Tests

    [Fact]
    public void ApplyInsert_AtBeginning_InsertsText()
    {
        var content = "hello";
        var operation = new InsertOperation
        {
            Id = new OperationId("site1", 1),
            ClientRevision = 1,
            ServerRevision = 1,
            Timestamp = DateTime.UtcNow,
            Position = 0,
            Text = "hi "
        };

        var result = _resolver.ApplyOperation(content, operation);
        Assert.Equal("hi hello", result);
    }

    [Fact]
    public void ApplyInsert_InMiddle_InsertsText()
    {
        var content = "helo";
        var operation = new InsertOperation
        {
            Id = new OperationId("site1", 1),
            ClientRevision = 1,
            ServerRevision = 1,
            Timestamp = DateTime.UtcNow,
            Position = 2,
            Text = "l"
        };

        var result = _resolver.ApplyOperation(content, operation);
        Assert.Equal("hello", result);
    }

    [Fact]
    public void ApplyInsert_AtEnd_InsertsText()
    {
        var content = "hello";
        var operation = new InsertOperation
        {
            Id = new OperationId("site1", 1),
            ClientRevision = 1,
            ServerRevision = 1,
            Timestamp = DateTime.UtcNow,
            Position = 5,
            Text = "!"
        };

        var result = _resolver.ApplyOperation(content, operation);
        Assert.Equal("hello!", result);
    }

    [Fact]
    public void ApplyDelete_AtBeginning_DeletesText()
    {
        var content = "hello";
        var operation = new DeleteOperation
        {
            Id = new OperationId("site1", 1),
            ClientRevision = 1,
            ServerRevision = 1,
            Timestamp = DateTime.UtcNow,
            Position = 0,
            Length = 1
        };

        var result = _resolver.ApplyOperation(content, operation);
        Assert.Equal("ello", result);
    }

    [Fact]
    public void ApplyDelete_InMiddle_DeletesText()
    {
        var content = "hello";
        var operation = new DeleteOperation
        {
            Id = new OperationId("site1", 1),
            ClientRevision = 1,
            ServerRevision = 1,
            Timestamp = DateTime.UtcNow,
            Position = 1,
            Length = 3
        };

        var result = _resolver.ApplyOperation(content, operation);
        Assert.Equal("ho", result);
    }

    #endregion

    #region ApplyOperations Tests

    [Fact]
    public void ApplyOperations_SequenceOfOperations_AppliesAllInOrder()
    {
        var content = "";
        var operations = new List<Operation>
        {
            new InsertOperation
            {
                Id = new OperationId("site1", 1),
                ClientRevision = 1,
                ServerRevision = 1,
                Timestamp = DateTime.UtcNow,
                Position = 0,
                Text = "hello"
            },
            new InsertOperation
            {
                Id = new OperationId("site1", 2),
                ClientRevision = 2,
                ServerRevision = 2,
                Timestamp = DateTime.UtcNow,
                Position = 5,
                Text = " "
            },
            new InsertOperation
            {
                Id = new OperationId("site1", 3),
                ClientRevision = 3,
                ServerRevision = 3,
                Timestamp = DateTime.UtcNow,
                Position = 6,
                Text = "world"
            }
        };

        var result = _resolver.ApplyOperations(content, operations);
        Assert.Equal("hello world", result);
    }

    #endregion

    #region Transform - Insert vs Insert Tests

    [Fact]
    public void Transform_InsertInsert_SamePositionDifferentSites_DeterministicOrdering()
    {
        var op1 = new InsertOperation
        {
            Id = new OperationId("site1", 1),
            ClientRevision = 1,
            ServerRevision = 1,
            Timestamp = DateTime.UtcNow,
            Position = 0,
            Text = "a"
        };

        var op2 = new InsertOperation
        {
            Id = new OperationId("site2", 1),
            ClientRevision = 1,
            ServerRevision = 2,
            Timestamp = DateTime.UtcNow,
            Position = 0,
            Text = "b"
        };

        var transformed1 = _resolver.TransformAgainstConcurrent(op1, op2);
        var transformed2 = _resolver.TransformAgainstConcurrent(op2, op1);

        // Verify deterministic behavior: both should result in consistent final state
        var content1 = _resolver.ApplyOperation("", op1);
        content1 = _resolver.ApplyOperation(content1, transformed1);

        var content2 = _resolver.ApplyOperation("", op2);
        content2 = _resolver.ApplyOperation(content2, transformed2);

        // Both paths should produce same result - actual content is "ab" because site1 < site2
        Assert.Equal(content1, content2);
        Assert.Equal("ab", content1);
    }

    [Fact]
    public void Transform_InsertInsert_BeforeAndAfter_NoConflict()
    {
        var op1 = new InsertOperation
        {
            Id = new OperationId("site1", 1),
            ClientRevision = 1,
            ServerRevision = 1,
            Timestamp = DateTime.UtcNow,
            Position = 0,
            Text = "a"
        };

        var op2 = new InsertOperation
        {
            Id = new OperationId("site2", 1),
            ClientRevision = 1,
            ServerRevision = 2,
            Timestamp = DateTime.UtcNow,
            Position = 5,
            Text = "b"
        };

        var transformed = _resolver.TransformAgainstConcurrent(op1, op2);

        // Since op1 is before op2, no transformation needed
        Assert.IsType<InsertOperation>(transformed);
        Assert.Equal(0, ((InsertOperation)transformed).Position);
    }

    [Fact]
    public void Transform_InsertInsert_AfterPrevious_PositionShifted()
    {
        var op1 = new InsertOperation
        {
            Id = new OperationId("site1", 1),
            ClientRevision = 1,
            ServerRevision = 1,
            Timestamp = DateTime.UtcNow,
            Position = 5,
            Text = "a"
        };

        var op2 = new InsertOperation
        {
            Id = new OperationId("site2", 1),
            ClientRevision = 1,
            ServerRevision = 2,
            Timestamp = DateTime.UtcNow,
            Position = 2,
            Text = "bbb"
        };

        var transformed = _resolver.TransformAgainstConcurrent(op1, op2);

        // op2 is before op1, so op1's position should shift right by length of op2
        Assert.IsType<InsertOperation>(transformed);
        Assert.Equal(8, ((InsertOperation)transformed).Position);
    }

    #endregion

    #region Transform - Insert vs Delete Tests

    [Fact]
    public void Transform_InsertDelete_DeleteBefore_PositionShifted()
    {
        var insert = new InsertOperation
        {
            Id = new OperationId("site1", 1),
            ClientRevision = 1,
            ServerRevision = 1,
            Timestamp = DateTime.UtcNow,
            Position = 5,
            Text = "x"
        };

        var delete = new DeleteOperation
        {
            Id = new OperationId("site2", 1),
            ClientRevision = 1,
            ServerRevision = 2,
            Timestamp = DateTime.UtcNow,
            Position = 0,
            Length = 3
        };

        var transformed = _resolver.TransformAgainstConcurrent(insert, delete);

        // Delete removes 3 chars before our insert, so position shifts left
        Assert.IsType<InsertOperation>(transformed);
        Assert.Equal(2, ((InsertOperation)transformed).Position);
    }

    [Fact]
    public void Transform_InsertDelete_InsertInDeleteRange_ShiftsToDeleteStart()
    {
        var insert = new InsertOperation
        {
            Id = new OperationId("site1", 1),
            ClientRevision = 1,
            ServerRevision = 1,
            Timestamp = DateTime.UtcNow,
            Position = 3,
            Text = "x"
        };

        var delete = new DeleteOperation
        {
            Id = new OperationId("site2", 1),
            ClientRevision = 1,
            ServerRevision = 2,
            Timestamp = DateTime.UtcNow,
            Position = 2,
            Length = 5
        };

        var transformed = _resolver.TransformAgainstConcurrent(insert, delete);

        // Insert is within deletion range, shift to start of deletion
        Assert.IsType<InsertOperation>(transformed);
        Assert.Equal(2, ((InsertOperation)transformed).Position);
    }

    #endregion

    #region Transform - Delete vs Insert Tests

    [Fact]
    public void Transform_DeleteInsert_InsertBefore_PositionShifted()
    {
        var delete = new DeleteOperation
        {
            Id = new OperationId("site1", 1),
            ClientRevision = 1,
            ServerRevision = 1,
            Timestamp = DateTime.UtcNow,
            Position = 5,
            Length = 2
        };

        var insert = new InsertOperation
        {
            Id = new OperationId("site2", 1),
            ClientRevision = 1,
            ServerRevision = 2,
            Timestamp = DateTime.UtcNow,
            Position = 2,
            Text = "aaa"
        };

        var transformed = _resolver.TransformAgainstConcurrent(delete, insert);

        // Insert before delete, shift delete position right
        Assert.IsType<DeleteOperation>(transformed);
        Assert.Equal(8, ((DeleteOperation)transformed).Position);
    }

    [Fact]
    public void Transform_DeleteInsert_InsertInDeleteRange_LengthExtended()
    {
        var delete = new DeleteOperation
        {
            Id = new OperationId("site1", 1),
            ClientRevision = 1,
            ServerRevision = 1,
            Timestamp = DateTime.UtcNow,
            Position = 2,
            Length = 5
        };

        var insert = new InsertOperation
        {
            Id = new OperationId("site2", 1),
            ClientRevision = 1,
            ServerRevision = 2,
            Timestamp = DateTime.UtcNow,
            Position = 4,
            Text = "xxx"
        };

        var transformed = _resolver.TransformAgainstConcurrent(delete, insert);

        // Insert within delete range, extend delete length
        Assert.IsType<DeleteOperation>(transformed);
        Assert.Equal(8, ((DeleteOperation)transformed).Length);
    }

    #endregion

    #region Transform - Delete vs Delete Tests

    [Fact]
    public void Transform_DeleteDelete_NoOverlap_PositionAdjusted()
    {
        var delete1 = new DeleteOperation
        {
            Id = new OperationId("site1", 1),
            ClientRevision = 1,
            ServerRevision = 1,
            Timestamp = DateTime.UtcNow,
            Position = 0,
            Length = 2
        };

        var delete2 = new DeleteOperation
        {
            Id = new OperationId("site2", 1),
            ClientRevision = 1,
            ServerRevision = 2,
            Timestamp = DateTime.UtcNow,
            Position = 5,
            Length = 3
        };

        var transformed = _resolver.TransformAgainstConcurrent(delete1, delete2);

        // delete2 is after delete1, but doesn't overlap, no adjustment needed
        Assert.IsType<DeleteOperation>(transformed);
        Assert.Equal(0, ((DeleteOperation)transformed).Position);
        Assert.Equal(2, ((DeleteOperation)transformed).Length);
    }

    [Fact]
    public void Transform_DeleteDelete_PartialOverlap_AdjustLength()
    {
        var delete1 = new DeleteOperation
        {
            Id = new OperationId("site1", 1),
            ClientRevision = 1,
            ServerRevision = 1,
            Timestamp = DateTime.UtcNow,
            Position = 0,
            Length = 5
        };

        var delete2 = new DeleteOperation
        {
            Id = new OperationId("site2", 1),
            ClientRevision = 1,
            ServerRevision = 2,
            Timestamp = DateTime.UtcNow,
            Position = 3,
            Length = 4
        };

        var transformed = _resolver.TransformAgainstConcurrent(delete1, delete2);

        // Deletions overlap, verify position and length are adjusted correctly
        Assert.IsType<DeleteOperation>(transformed);
        Assert.Equal(0, ((DeleteOperation)transformed).Position);
        // delete1 covers [0,5), delete2 covers [3,7)
        // After adjusting: delete1 should cover [0,3)
        Assert.Equal(3, ((DeleteOperation)transformed).Length);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void Integration_ConcurrentInserts_ProducesConsistentState()
    {
        var doc = "";

        // User 1 inserts "abc" at position 0
        var op1 = new InsertOperation
        {
            Id = new OperationId("user1", 1),
            ClientRevision = 1,
            ServerRevision = 1,
            Timestamp = DateTime.UtcNow,
            Position = 0,
            Text = "abc"
        };

        // User 2 inserts "xyz" at position 0 (concurrent)
        var op2 = new InsertOperation
        {
            Id = new OperationId("user2", 1),
            ClientRevision = 1,
            ServerRevision = 2,
            Timestamp = DateTime.UtcNow,
            Position = 0,
            Text = "xyz"
        };

        // Apply in order 1, 2
        var doc1 = _resolver.ApplyOperation(doc, op1);
        var op2Transformed = _resolver.TransformAgainstConcurrent(op2, op1);
        doc1 = _resolver.ApplyOperation(doc1, op2Transformed);

        // Apply in order 2, 1
        var doc2 = _resolver.ApplyOperation(doc, op2);
        var op1Transformed = _resolver.TransformAgainstConcurrent(op1, op2);
        doc2 = _resolver.ApplyOperation(doc2, op1Transformed);

        // Both should produce the same result
        Assert.Equal(doc1, doc2);
        // Actual result is "abcxyz": user1's "abc" is inserted first, then user2's "xyz" shifts to position 3
        Assert.Equal("abcxyz", doc1);
        Assert.Equal(6, doc1.Length); // 3 chars from each
    }

    [Fact]
    public void Integration_MixedOperations_ProducesCorrectText()
    {
        var doc = "";

        // User 1: insert "hello"
        var op1 = new InsertOperation
        {
            Id = new OperationId("user1", 1),
            ClientRevision = 1,
            ServerRevision = 1,
            Timestamp = DateTime.UtcNow,
            Position = 0,
            Text = "hello"
        };
        doc = _resolver.ApplyOperation(doc, op1);
        Assert.Equal("hello", doc);

        // User 2: insert " world" at position 5
        var op2 = new InsertOperation
        {
            Id = new OperationId("user2", 1),
            ClientRevision = 1,
            ServerRevision = 2,
            Timestamp = DateTime.UtcNow,
            Position = 5,
            Text = " world"
        };
        doc = _resolver.ApplyOperation(doc, op2);
        Assert.Equal("hello world", doc);

        // User 1: delete " wor" (positions 5-8)
        var op3 = new DeleteOperation
        {
            Id = new OperationId("user1", 2),
            ClientRevision = 2,
            ServerRevision = 3,
            Timestamp = DateTime.UtcNow,
            Position = 5,
            Length = 4
        };
        doc = _resolver.ApplyOperation(doc, op3);
        // "hello world" with delete at position 5 length 4 removes " wor", leaving "hello" + "ld" = "hellold"
        Assert.Equal("hellold", doc);
    }

    #endregion
}
