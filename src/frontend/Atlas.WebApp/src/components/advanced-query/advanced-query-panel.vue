<template>
  <div class="advanced-query-panel">
    <div class="panel-header" v-if="title">
      <h3>{{ title }}</h3>
    </div>
    <div class="panel-body">
      <QueryGroup :model-value="modelValue.rootGroup" :fields="fields" :is-root="true" />
    </div>
    <div class="panel-footer" v-if="showActions">
      <a-button type="primary" @click="$emit('search')">查询</a-button>
      <a-button style="margin-left: 8px" @click="resetQuery">重置</a-button>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { AdvancedQueryConfig } from '@/types/advanced-query';
import type { DynamicFieldDefinition } from '@/types/dynamic-tables';
import QueryGroup from './query-group.vue';

const props = defineProps<{
  modelValue: AdvancedQueryConfig;
  fields: DynamicFieldDefinition[];
  title?: string;
  showActions?: boolean;
}>();

const emit = defineEmits(['update:modelValue', 'search', 'reset']);

const generateId = () => typeof crypto !== 'undefined' && crypto.randomUUID ? crypto.randomUUID() : Math.random().toString(36).substring(2);

const resetQuery = () => {
  const emptyConfig: AdvancedQueryConfig = {
    rootGroup: {
      id: generateId(),
      conjunction: 'and',
      rules: [],
      groups: []
    }
  };
  emit('update:modelValue', emptyConfig);
  emit('reset');
};
</script>

<style scoped>
.advanced-query-panel {
  background-color: #fff;
  border-radius: 4px;
}
.panel-header {
  margin-bottom: 16px;
}
.panel-footer {
  margin-top: 16px;
  text-align: right;
}
</style>
