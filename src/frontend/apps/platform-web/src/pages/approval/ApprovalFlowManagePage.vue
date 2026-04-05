<template>
  <SemanticBridgeCard
    title-key="route.approvalFlowManage"
    description-key="semanticBridge.approvalFlowManageDesc"
    primary-action-key="semanticBridge.goApprovalDesigner"
    primary-path="/approval/designer"
    secondary-action-key="semanticBridge.goApprovalAgentConfig"
    secondary-path="/approval/agents"
  />
</template>

<script setup lang="ts">
import SemanticBridgeCard from "@/components/navigation/SemanticBridgeCard.vue";
</script>
