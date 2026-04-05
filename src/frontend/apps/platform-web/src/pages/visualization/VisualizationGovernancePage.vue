<template>
  <SemanticBridgeCard
    title-key="route.visualizationGovernance"
    description-key="semanticBridge.visualizationGovernanceDesc"
    primary-action-key="semanticBridge.goResourceCenter"
    primary-path="/console/resources"
    secondary-action-key="semanticBridge.goRuntimeExecutions"
    secondary-path="/console/runtime-executions"
  />
</template>

<script setup lang="ts">
import SemanticBridgeCard from "@/components/navigation/SemanticBridgeCard.vue";
</script>
