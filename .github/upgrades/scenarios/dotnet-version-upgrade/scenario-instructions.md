# .NET Version Upgrade

## Preferences
- **Flow Mode**: Automatic
- **Target Framework**: net10.0

## Source Control
- **Source Branch**: main
- **Working Branch**: upgrade-dotnet-10
- **Commit Strategy**: After Each Task
- **Branch Sync**: Auto (Merge)

## Upgrade Options
**Source**: .github/upgrades/scenarios/dotnet-version-upgrade/upgrade-options.md

### Strategy
- Upgrade Strategy: All-at-Once

## Strategy
**Selected**: All-At-Once
**Rationale**: 3 projects, all SDK-style and already on modern .NET, with no package incompatibilities or API incidents.

### Execution Constraints
- Perform the upgrade in a single atomic pass across all projects (no tiered phases).
- Apply project and package updates before restore/build validation.
- Run full solution validation after upgrade changes complete.
- Keep commit cadence at task boundaries unless user changes commit strategy.
