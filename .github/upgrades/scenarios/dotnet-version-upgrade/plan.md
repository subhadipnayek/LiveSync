# .NET Version Upgrade Plan

## Overview

**Target**: Validate and finalize LiveSync backend on .NET 10.
**Scope**: 3 SDK-style projects (~3.7k LOC) already targeting net10.0 with compatible dependencies.

### Selected Strategy
**All-At-Once** — All projects upgraded simultaneously in a single operation.
**Rationale**: 3 projects, all on modern .NET with no compatibility findings, so a single-pass validation-focused plan is sufficient.

## Tasks

### 01-solution-validation: Validate .NET 10 solution baseline

The solution already targets net10.0 across API, SignalR, and test projects, so this task focuses on confirming runtime and build integrity after the framework move. Scope includes dependency restore, full solution compilation, and test execution to verify no hidden compatibility regressions remain.

This task also captures any residual configuration/package adjustments needed to keep all upgraded projects warning-free and production-ready. Any discovered warning or failure is treated as in-scope and fixed before completion.

**Done when**: The full solution restores and builds cleanly with zero errors/warnings, all test projects pass, and no additional net10.0 compatibility actions remain.
