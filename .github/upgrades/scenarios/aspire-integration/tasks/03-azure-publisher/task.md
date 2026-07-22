# 03-azure-publisher: Add Azure publisher configuration for deployment readiness

Configure Azure publishing support in the AppHost by adding the appropriate Aspire publisher packages and authoring publish configuration code for the selected hosting target. This task prepares the solution for user-initiated deployment without performing deployment during the scenario.

Validation includes successful AppHost build after publisher setup and ensuring the publish entry points are present for future deploy commands.

**Done when**: Azure publisher dependencies and publish code are in place, AppHost builds successfully, and deployment instructions can be handed off.
