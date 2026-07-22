# 01-environment-setup: Initialize Aspire orchestration workspace

Prepare the repository for Aspire orchestration by validating CLI prerequisites, ensuring the AppHost skeleton is created in the solution context, and confirming all compatible projects are in scope for integration. This task establishes the baseline artifacts required for downstream wiring.

The task also verifies that existing upgrade state remains intact and that generated Aspire artifacts are compatible with the current branch and solution structure.

**Done when**: Aspire AppHost initialization artifacts exist, environment checks pass, and the solution is ready for Aspire service wiring.

## Research Findings

- All compatible projects in scope target `net10.0` (`LiveSync.Api`, `LiveSync.SignalR`, `LiveSync.SignalR.Tests`).
- Aspire CLI was already installed during pre-check and must be re-validated before initialization.
- No prior AppHost/ServiceDefaults artifacts were detected, so `aspire init` is required in this task.
- `aspire init` in non-interactive mode needed an explicit language and an unambiguous solution context; execution used `--language csharp` from `backend/` and created `backend/LiveSync.AppHost/`.
