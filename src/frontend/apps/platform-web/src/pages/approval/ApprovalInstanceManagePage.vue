<template>
  <SemanticBridgeCard
    title-key="route.approvalInstanceManage"
    description-key="semanticBridge.approvalInstanceManageDesc"
    primary-action-key="semanticBridge.goApprovalTaskPool"
    primary-path="/approval/tasks/pool"
    secondary-action-key="semanticBridge.goApprovalWorkspace"
    secondary-path="/approval/workspace"
  />
</template>

<script setup lang="ts">
import SemanticBridgeCard from "@/components/navigation/SemanticBridgeCard.vue";
</script>
