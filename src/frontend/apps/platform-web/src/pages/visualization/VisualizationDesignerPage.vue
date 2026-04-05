<template>
  <SemanticBridgeCard
    title-key="route.visualizationDesigner"
    description-key="semanticBridge.visualizationDesignerDesc"
    primary-action-key="semanticBridge.goAppBuilder"
    primary-path="/lowcode/builder"
    secondary-action-key="semanticBridge.goVisualizationCenter"
    secondary-path="/visualization/center"
  />
</template>

<script setup lang="ts">
import SemanticBridgeCard from "@/components/navigation/SemanticBridgeCard.vue";
</script>
