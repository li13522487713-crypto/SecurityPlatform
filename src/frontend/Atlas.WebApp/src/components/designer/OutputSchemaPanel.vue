<template>
  <div class="output-schema-panel">
    <a-list size="small" :data-source="mappings">
      <template #renderItem="{ item }">
        <a-list-item>
          <a-space>
            <a-tag color="blue">{{ item.targetFieldKey }}</a-tag>
            <span>{{ item.targetLabel }}</span>
            <a-tag>{{ item.targetType }}</a-tag>
          </a-space>
        </a-list-item>
      </template>
    </a-list>
  </div>
</template>

<script setup lang="ts">
import type { OutputFieldMapping } from "@/types/dynamic-dataflow";

defineProps<{
  mappings: OutputFieldMapping[];
}>();
</script>

<style scoped>
.output-schema-panel {
  padding: 8px;
}
</style>
