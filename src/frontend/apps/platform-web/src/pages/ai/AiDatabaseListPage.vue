<template>
  <SemanticBridgeCard
    title-key="route.aiDatabaseList"
    description-key="semanticBridge.aiDatabaseListDesc"
    primary-action-key="semanticBridge.goKnowledgeBases"
    primary-path="/ai/knowledge-bases"
    secondary-action-key="semanticBridge.goAiWorkspace"
    secondary-path="/ai/workspace"
  />
</template>

<script setup lang="ts">
import SemanticBridgeCard from "@/components/navigation/SemanticBridgeCard.vue";
</script>
