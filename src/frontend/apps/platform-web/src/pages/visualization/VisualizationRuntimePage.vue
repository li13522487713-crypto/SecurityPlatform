<template>
  <SemanticBridgeCard
    title-key="route.visualizationRuntime"
    description-key="semanticBridge.visualizationRuntimeDesc"
    primary-action-key="semanticBridge.goRuntimeExecutions"
    primary-path="/console/runtime-executions"
    secondary-action-key="semanticBridge.goRuntimeContexts"
    secondary-path="/console/runtime-contexts"
  />
</template>

<script setup lang="ts">
import SemanticBridgeCard from "@/components/navigation/SemanticBridgeCard.vue";
</script>
