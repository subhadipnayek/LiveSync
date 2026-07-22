# Aspire Integration Plan

## Overview

**Target**: Add Aspire orchestration to the LiveSync backend for local inner-loop development and Azure-ready publishing setup.
**Scope**: 3 compatible net10.0 projects with one service-to-service HTTP dependency (SignalR -> API) plus Redis/PostgreSQL infrastructure integration.

## Tasks

### 01-environment-setup: Initialize Aspire orchestration workspace

Prepare the repository for Aspire orchestration by validating CLI prerequisites, ensuring the AppHost skeleton is created in the solution context, and confirming all compatible projects are in scope for integration. This task establishes the baseline artifacts required for downstream wiring.

The task also verifies that existing upgrade state remains intact and that generated Aspire artifacts are compatible with the current branch and solution structure.

**Done when**: Aspire AppHost initialization artifacts exist, environment checks pass, and the solution is ready for Aspire service wiring.

---

### 02-aspireify: Wire projects and dependencies into Aspire AppHost

Integrate compatible application projects into Aspire orchestration using the discovered communication map and infrastructure signals. This includes wiring runtime relationships so SignalR references API correctly and infrastructure resources are attached with proper startup ordering.

This task must include all compatible solution projects where orchestration is applicable while keeping test-only projects out of runnable AppHost resources unless explicitly required.

**Done when**: AppHost includes the compatible services with correct service references/resource wiring, and orchestration starts with expected resource topology.

---

### 03-azure-publisher: Add Azure publisher configuration for deployment readiness

Configure Azure publishing support in the AppHost by adding the appropriate Aspire publisher packages and authoring publish configuration code for the selected hosting target. This task prepares the solution for user-initiated deployment without performing deployment during the scenario.

Validation includes successful AppHost build after publisher setup and ensuring the publish entry points are present for future deploy commands.

**Done when**: Azure publisher dependencies and publish code are in place, AppHost builds successfully, and deployment instructions can be handed off.

---

### 04-complete: Finalize integration summary and operational handoff

Produce the final integration surface for the user: dashboard access details, resource status summary, and any deferred actions. This task confirms the scenario outcomes and captures operational next steps for local run and optional Azure deployment.

It also records any limitations or skipped items so follow-up work is explicit and actionable.

**Done when**: Final status artifacts are complete, dashboard/resource information is available, and the integration handoff is documented.
