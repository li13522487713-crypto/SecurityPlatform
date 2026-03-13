<template>
  <div class="ai-node">
    <strong>{{ data.label || "Node" }}</strong>
    <div class="node-key">{{ data.type }}</div>
  </div>
</template>

<script setup lang="ts">
defineProps<{ data: { label?: string; type?: string } }>();
</script>

<style scoped>
.ai-node {
  min-width: 150px;
  padding: 8px 10px;
  border-radius: 8px;
  border: 1px solid #d9d9d9;
  background: #fff;
}

.node-key {
  margin-top: 4px;
  color: rgba(0, 0, 0, 0.45);
  font-size: 12px;
}
</style>
