# 02-aspireify: Wire projects and dependencies into Aspire AppHost

Integrate compatible application projects into Aspire orchestration using the discovered communication map and infrastructure signals. This includes wiring runtime relationships so SignalR references API correctly and infrastructure resources are attached with proper startup ordering.

This task must include all compatible solution projects where orchestration is applicable while keeping test-only projects out of runnable AppHost resources unless explicitly required.

**Done when**: AppHost includes the compatible services with correct service references/resource wiring, and orchestration starts with expected resource topology.
