# 01-solution-validation: Validate .NET 10 solution baseline

The solution already targets net10.0 across API, SignalR, and test projects, so this task focuses on confirming runtime and build integrity after the framework move. Scope includes dependency restore, full solution compilation, and test execution to verify no hidden compatibility regressions remain.

This task also captures any residual configuration/package adjustments needed to keep all upgraded projects warning-free and production-ready. Any discovered warning or failure is treated as in-scope and fixed before completion.

**Done when**: The full solution restores and builds cleanly with zero errors/warnings, all test projects pass, and no additional net10.0 compatibility actions remain.

## Scope Inventory

### Projects Affected
- `backend/LiveSync.Api/LiveSync.Api.csproj` — validate restore/build output on net10.0.
- `backend/LiveSync.SignalR/LiveSync.SignalR.csproj` — validate restore/build output on net10.0.
- `backend/LiveSync.SignalR.Tests/LiveSync.SignalR.Tests.csproj` — run and pass tests against upgraded dependencies.

### Distinct Concerns
- End-to-end restore and compile validation for the full solution.
- Warning cleanup if any warnings surface in touched projects.
- Test execution for the backend test project.

## Research Findings

- Assessment indicates all 3 projects are SDK-style, on net10.0, and report no package/API incompatibilities.
- Project files confirm `<TargetFramework>net10.0</TargetFramework>` across all backend projects.
- No additional package migration work is currently indicated; task execution is validation-focused unless build/test output reveals regressions.
