<template>
  <SemanticBridgeCard
    title-key="route.aiDatabaseDetail"
    description-key="semanticBridge.aiDatabaseDetailDesc"
    primary-action-key="semanticBridge.goKnowledgeBases"
    primary-path="/ai/knowledge-bases"
    secondary-action-key="semanticBridge.goAiPluginList"
    secondary-path="/ai/plugins"
  />
</template>

<script setup lang="ts">
import SemanticBridgeCard from "@/components/navigation/SemanticBridgeCard.vue";
</script>
