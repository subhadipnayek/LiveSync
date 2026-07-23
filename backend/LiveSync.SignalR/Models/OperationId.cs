namespace LiveSync.Models;

/// <summary>
/// Unique identifier for an operation in CRDT-based conflict resolution.
/// Combines a site ID (per-client) with a logical clock to ensure global uniqueness and ordering.
/// </summary>
public record OperationId(string SiteId, long Clock) : IComparable<OperationId>
{
    public int CompareTo(OperationId? other)
    {
        if (other == null)
            return 1;

        // First compare by clock (logical timestamp)
        var clockComparison = Clock.CompareTo(other.Clock);
        if (clockComparison != 0)
            return clockComparison;

        // If clocks are equal, use site ID for deterministic ordering
        return string.Compare(SiteId, other.SiteId, StringComparison.Ordinal);
    }

    public override string ToString() => $"({SiteId}:{Clock})";
}
