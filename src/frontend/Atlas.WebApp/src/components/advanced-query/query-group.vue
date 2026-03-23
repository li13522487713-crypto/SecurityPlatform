<template>
  <div class="query-group" :class="{ 'is-root': isRoot }">
    <div class="group-header">
      <a-radio-group v-model:value="model.conjunction" size="small">
        <a-radio-button value="and">AND</a-radio-button>
        <a-radio-button value="or">OR</a-radio-button>
      </a-radio-group>
      <div class="group-actions">
        <a-button type="link" size="small" @click="addRule">
          <template #icon><PlusOutlined /></template> 加条件
        </a-button>
        <a-button type="link" size="small" @click="addGroup">
          <template #icon><PlusSquareOutlined /></template> 加子分组
        </a-button>
        <a-button v-if="!isRoot" type="text" danger size="small" @click="$emit('delete')">
          <template #icon><DeleteOutlined /></template>
        </a-button>
      </div>
    </div>
    <div class="group-body">
      <template v-if="model.rules && model.rules.length > 0">
        <QueryRule
          v-for="(rule, index) in model.rules"
          :key="rule.id"
          v-model="model.rules[index]"
          :fields="fields"
          @delete="removeRule(index)"
        />
      </template>
      <template v-if="model.groups && model.groups.length > 0">
        <QueryGroup
          v-for="(group, index) in model.groups"
          :key="group.id"
          v-model="model.groups[index]"
          :fields="fields"
          :is-root="false"
          @delete="removeGroup(index)"
        />
      </template>
      <div v-if="isEmpty" class="empty-hint">
        点击上方按钮添加查询条件
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { PlusOutlined, PlusSquareOutlined, DeleteOutlined } from '@ant-design/icons-vue';
import type { QueryGroup as IQueryGroup } from '@/types/advanced-query';
import type { DynamicFieldDefinition } from '@/types/dynamic-tables';
import QueryRule from './query-rule.vue';

const model = defineModel<IQueryGroup>({ required: true });
const props = defineProps<{
  fields: DynamicFieldDefinition[];
  isRoot?: boolean;
}>();

defineEmits(['delete']);

const generateId = () => typeof crypto !== 'undefined' && crypto.randomUUID ? crypto.randomUUID() : Math.random().toString(36).substring(2);

const isEmpty = computed(() => {
  return (!model.value.rules || model.value.rules.length === 0) &&
         (!model.value.groups || model.value.groups.length === 0);
});

const addRule = () => {
  if (!model.value.rules) {
    model.value.rules = [];
  }
  model.value.rules.push({
    id: generateId(),
    field: props.fields.length > 0 ? props.fields[0].name : '',
    operator: 'eq',
    value: null
  });
};

const addGroup = () => {
  if (!model.value.groups) {
    model.value.groups = [];
  }
  model.value.groups.push({
    id: generateId(),
    conjunction: 'and',
    rules: [],
    groups: []
  });
};

const removeRule = (index: number) => {
  if (model.value.rules) {
    model.value.rules.splice(index, 1);
  }
};

const removeGroup = (index: number) => {
  if (model.value.groups) {
    model.value.groups.splice(index, 1);
  }
};
</script>

<style scoped>
.query-group {
  border: 1px solid #d9d9d9;
  border-radius: 4px;
  padding: 12px;
  margin-bottom: 8px;
  background-color: #fafafa;
}
.query-group.is-root {
  border: 2px solid #1890ff;
  background-color: #fff;
}
.group-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 12px;
}
.group-actions {
  display: flex;
  gap: 8px;
}
.group-body {
  padding-left: 16px;
  border-left: 2px dashed #d9d9d9;
}
.empty-hint {
  color: #999;
  font-size: 13px;
  padding: 8px 0;
}
</style>
