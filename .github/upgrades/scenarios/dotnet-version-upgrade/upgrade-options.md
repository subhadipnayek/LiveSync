# Upgrade Options — LiveSync.sln

Assessment: 3 SDK-style projects already on modern .NET, no incompatible packages, no API incidents.

## Strategy

### Upgrade Strategy
All projects are already on modern .NET and the solution scope is small, so a single-pass upgrade is the default.

| Value | Description |
|-------|-------------|
| **All-at-Once** (selected) | Upgrade all projects in one pass for the fastest path with no multi-targeting overhead. |
| Top-Down | Upgrade entry-point apps first and keep libraries incrementally compatible during migration. |
