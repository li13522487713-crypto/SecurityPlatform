<template>
  <div class="condition-editor">
    <div v-if="!modelValue || modelValue.length === 0" class="empty-state">
      <div class="empty-text">暂无条件，请添加条件组</div>
      <a-button type="primary" size="small" @click="addGroup">
        <PlusOutlined /> 添加条件组
      </a-button>
    </div>

    <div v-else class="group-list">
      <div v-for="(group, gIndex) in modelValue" :key="gIndex" class="condition-group">
        <div class="group-header">
          <span class="group-title">条件组 {{ gIndex + 1 }} (组内满足所有条件)</span>
          <a-button type="text" danger size="small" @click="removeGroup(gIndex)">
            <DeleteOutlined />
          </a-button>
        </div>
        
        <div class="condition-list">
          <div v-for="(cond, cIndex) in group.conditions" :key="cIndex" class="condition-item">
            <div class="condition-row">
              <a-select 
                v-model:value="cond.field" 
                placeholder="选择字段" 
                style="width: 120px"
                @change="onFieldChange(cond)"
              >
                <a-select-option v-for="field in formFields" :key="field.id" :value="field.id">
                  {{ field.label }}
                </a-select-option>
              </a-select>
              
              <a-select v-model:value="cond.operator" placeholder="运算符" style="width: 100px">
                <a-select-option v-for="op in getOperators(cond.field)" :key="op.value" :value="op.value">
                  {{ op.label }}
                </a-select-option>
              </a-select>
              
              <div class="value-input">
                <a-input v-model:value="cond.value" placeholder="比较值" />
              </div>
              
              <a-button type="text" danger size="small" class="delete-btn" @click="removeCondition(gIndex, cIndex)">
                <CloseOutlined />
              </a-button>
            </div>
          </div>
          
          <a-button type="dashed" size="small" block @click="addCondition(gIndex)">
            <PlusOutlined /> 添加条件
          </a-button>
        </div>

        <div v-if="gIndex < modelValue.length - 1" class="group-divider">
          <span>OR (满足任一组即可)</span>
        </div>
      </div>
      
      <a-button type="dashed" block @click="addGroup" style="margin-top: 8px">
        <PlusOutlined /> 添加条件组 (OR)
      </a-button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { PlusOutlined, DeleteOutlined, CloseOutlined } from '@ant-design/icons-vue';
import type { ConditionGroup, ConditionExpression } from '@/types/approval-tree';
import type { LfFormField } from '@/types/approval-definition';

const props = defineProps<{
  modelValue: ConditionGroup[];
  formFields?: LfFormField[];
}>();

const emit = defineEmits<{
  'update:modelValue': [value: ConditionGroup[]];
}>();

// ── Operators ──
const commonOperators = [
  { label: '等于', value: 'equals' },
  { label: '不等于', value: 'notEquals' },
];

const numberOperators = [
  ...commonOperators,
  { label: '大于', value: 'greaterThan' },
  { label: '小于', value: 'lessThan' },
  { label: '大于等于', value: 'greaterThanOrEqual' },
  { label: '小于等于', value: 'lessThanOrEqual' },
];

const stringOperators = [
  ...commonOperators,
  { label: '包含', value: 'contains' },
  { label: '开头是', value: 'startsWith' },
  { label: '结尾是', value: 'endsWith' },
];

function getOperators(fieldId: string) {
  if (!props.formFields) return commonOperators;
  const field = props.formFields.find(f => (f.id || f.fieldId) === fieldId);
  if (!field) return commonOperators;
  
  // 简单根据类型判断
  // 假设 LfFormField 有 type 字段，或者我们可以推断
  // 这里暂时简化，如果是数字类组件则返回数字操作符
  if (['input-number', 'slider', 'rate'].includes(field.widgetType || '')) {
    return numberOperators;
  }
  return stringOperators;
}

function onFieldChange(cond: ConditionExpression) {
  // 重置操作符和值
  cond.operator = 'equals';
  cond.value = '';
}

// ── Actions ──
function addGroup() {
  const newGroups = [...props.modelValue];
  newGroups.push({
    conditions: [{ field: '', operator: 'equals', value: '' }]
  });
  emit('update:modelValue', newGroups);
}

function removeGroup(index: number) {
  const newGroups = [...props.modelValue];
  newGroups.splice(index, 1);
  emit('update:modelValue', newGroups);
}

function addCondition(groupIndex: number) {
  const newGroups = [...props.modelValue];
  newGroups[groupIndex].conditions.push({ field: '', operator: 'equals', value: '' });
  emit('update:modelValue', newGroups);
}

function removeCondition(groupIndex: number, conditionIndex: number) {
  const newGroups = [...props.modelValue];
  newGroups[groupIndex].conditions.splice(conditionIndex, 1);
  // 如果组内没有条件了，是否删除组？或者保留空组？
  // 这里保留空组，用户可以继续添加或删除组
  emit('update:modelValue', newGroups);
}
</script>

<style scoped>
.condition-editor {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.empty-state {
  text-align: center;
  padding: 16px;
  background: #fafafa;
  border: 1px dashed #d9d9d9;
  border-radius: 4px;
}
.empty-text {
  font-size: 12px;
  color: #8c8c8c;
  margin-bottom: 8px;
}

.condition-group {
  border: 1px solid #f0f0f0;
  border-radius: 6px;
  background: #fff;
  padding: 12px;
  position: relative;
}

.group-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 8px;
}
.group-title {
  font-size: 12px;
  font-weight: 500;
  color: #595959;
}

.condition-list {
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.condition-item {
  background: #fafafa;
  padding: 8px;
  border-radius: 4px;
}

.condition-row {
  display: flex;
  gap: 8px;
  align-items: center;
}

.value-input {
  flex: 1;
  min-width: 0;
}

.delete-btn {
  color: #999;
}
.delete-btn:hover {
  color: #ff4d4f;
}

.group-divider {
  text-align: center;
  margin-top: 12px;
  font-size: 12px;
  color: #1677ff;
  font-weight: 500;
  position: relative;
}
.group-divider::before {
  content: '';
  position: absolute;
  left: 0;
  top: 50%;
  width: 35%;
  height: 1px;
  background: #f0f0f0;
}
.group-divider::after {
  content: '';
  position: absolute;
  right: 0;
  top: 50%;
  width: 35%;
  height: 1px;
  background: #f0f0f0;
}
</style>