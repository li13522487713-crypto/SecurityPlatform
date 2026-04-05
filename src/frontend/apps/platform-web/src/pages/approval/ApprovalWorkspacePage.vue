<template>
  <SemanticBridgeCard
    title-key="route.approvalWorkspace"
    description-key="semanticBridge.approvalWorkspaceDesc"
    primary-action-key="semanticBridge.goApprovalStart"
    primary-path="/approval/start"
    secondary-action-key="semanticBridge.goApprovalTaskPool"
    secondary-path="/approval/tasks/pool"
  />
</template>

<script setup lang="ts">
import SemanticBridgeCard from "@/components/navigation/SemanticBridgeCard.vue";
</script>
