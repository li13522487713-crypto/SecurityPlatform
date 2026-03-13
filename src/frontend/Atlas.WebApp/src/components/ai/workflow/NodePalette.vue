<template>
  <div class="node-palette">
    <h4>节点面板</h4>
    <a-space direction="vertical" style="width: 100%">
      <a-button
        v-for="item in nodeTypes"
        :key="item.key"
        block
        @click="$emit('add-node', item)"
      >
        {{ item.name }}
      </a-button>
    </a-space>
  </div>
</template>

<script setup lang="ts">
import type { AiWorkflowNodeTypeDto } from "@/services/api-ai-workflow";

defineProps<{ nodeTypes: AiWorkflowNodeTypeDto[] }>();

defineEmits<{
  (e: "add-node", nodeType: AiWorkflowNodeTypeDto): void;
}>();
</script>

<style scoped>
.node-palette {
  width: 220px;
  border-right: 1px solid #f0f0f0;
  padding: 12px;
  overflow-y: auto;
}
</style>
