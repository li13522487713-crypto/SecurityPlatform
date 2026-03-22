<template>
  <div class="query-rule">
    <a-space>
      <a-select
        v-model:value="modelValue.field"
        :options="fieldOptions"
        placeholder="请选择字段"
        style="width: 150px"
        @change="handleFieldChange"
      />
      <a-select
        v-model:value="modelValue.operator"
        :options="operatorOptions"
        placeholder="请选择操作符"
        style="width: 120px"
      />
      
      <template v-if="['in', 'between'].includes(modelValue.operator)">
         <a-input v-model:value="arrayStringValue" placeholder="逗号分隔 (例: a,b,c)" style="width: 200px" />
      </template>
      <template v-else-if="currentFieldType === 'Bool'">
        <a-switch v-model:checked="modelValue.value" />
      </template>
      <template v-else-if="currentFieldType === 'Int' || currentFieldType === 'Long' || currentFieldType === 'Decimal'">
        <a-input-number v-model:value="modelValue.value" style="width: 200px" />
      </template>
      <template v-else-if="currentFieldType === 'Date' || currentFieldType === 'DateTime'">
        <a-date-picker v-if="currentFieldType === 'Date'" v-model:value="modelValue.value" value-format="YYYY-MM-DD" style="width: 200px" />
        <a-date-picker v-else show-time v-model:value="modelValue.value" value-format="YYYY-MM-DD HH:mm:ss" style="width: 200px" />
      </template>
      <template v-else>
        <a-input v-model:value="modelValue.value" placeholder="请输入值" style="width: 200px" />
      </template>
      
      <a-button type="text" danger @click="$emit('delete')">
        <template #icon><DeleteOutlined /></template>
      </a-button>
    </a-space>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { DeleteOutlined } from '@ant-design/icons-vue';
import type { QueryRule } from '@/types/advanced-query';
import type { DynamicFieldDefinition } from '@/types/dynamic-tables';

const props = defineProps<{
  modelValue: QueryRule;
  fields: DynamicFieldDefinition[];
}>();

const emit = defineEmits(['update:modelValue', 'delete']);

const fieldOptions = computed(() => {
  return props.fields.map(f => ({
    label: f.displayName || f.name,
    value: f.name
  }));
});

const currentFieldDef = computed(() => {
  return props.fields.find(f => f.name === props.modelValue.field);
});

const currentFieldType = computed(() => {
  return currentFieldDef.value?.fieldType || 'String';
});

const operatorOptions = computed(() => {
  const type = currentFieldType.value;
  const common = [
    { label: '等于', value: 'eq' },
    { label: '不等于', value: 'ne' }
  ];
  
  if (['Int', 'Long', 'Decimal', 'DateTime', 'Date'].includes(type)) {
    return [
      ...common,
      { label: '大于', value: 'gt' },
      { label: '大于等于', value: 'gte' },
      { label: '小于', value: 'lt' },
      { label: '小于等于', value: 'lte' },
      { label: '区间', value: 'between' }
    ];
  } else if (['String', 'Text'].includes(type)) {
    return [
      ...common,
      { label: '包含', value: 'like' },
      { label: '列表包含', value: 'in' }
    ];
  }
  
  return common;
});

const handleFieldChange = () => {
  props.modelValue.operator = operatorOptions.value[0].value;
  props.modelValue.value = null;
};

const arrayStringValue = computed({
  get: () => {
    if (Array.isArray(props.modelValue.value)) {
      return props.modelValue.value.join(',');
    }
    return props.modelValue.value || '';
  },
  set: (val: string) => {
    props.modelValue.value = val.split(',').map(s => s.trim()).filter(s => s !== '');
  }
});
</script>

<style scoped>
.query-rule {
  margin-bottom: 8px;
}
</style>
