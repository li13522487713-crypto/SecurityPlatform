<template>
  <SemanticBridgeCard
    title-key="route.visualizationCenter"
    description-key="semanticBridge.visualizationCenterDesc"
    primary-action-key="semanticBridge.goVisualizationRuntime"
    primary-path="/visualization/runtime"
    secondary-action-key="semanticBridge.goRuntimeExecutions"
    secondary-path="/console/runtime-executions"
  />
</template>

<script setup lang="ts">
import SemanticBridgeCard from "@/components/navigation/SemanticBridgeCard.vue";
</script>
