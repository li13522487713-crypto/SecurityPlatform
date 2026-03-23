<template>
  <div class="condition-editor">
    <div v-if="!modelValue || modelValue.length === 0" class="empty-state">
      <div class="empty-text">{{ t('approvalDesigner.condEmptyAddGroup') }}</div>
      <a-button type="primary" size="small" @click="addGroup">
        <PlusOutlined /> {{ t('approvalDesigner.condBtnAddGroup') }}
      </a-button>
    </div>

    <div v-else class="group-list">
      <div v-for="(group, gIndex) in modelValue" :key="gIndex" class="condition-group">
        <div class="group-header">
          <span class="group-title">{{ t('approvalDesigner.condGroupTitle', { index: gIndex + 1 }) }}</span>
          <a-button type="text" danger size="small" @click="removeGroup(gIndex)">
            <DeleteOutlined />
          </a-button>
        </div>
        
        <div class="condition-list">
          <div v-for="(cond, cIndex) in group.conditions" :key="cIndex" class="condition-item">
            <div class="condition-row">
              <a-select 
                v-model:value="cond.field" 
                :placeholder="t('approvalDesigner.phSelectField')" 
                style="width: 120px"
                @change="onFieldChange(cond)"
              >
                <a-select-option v-for="field in formFields" :key="field.fieldId" :value="field.fieldId">
                  {{ field.fieldName }}
                </a-select-option>
              </a-select>
              
              <a-select v-model:value="cond.operator" :placeholder="t('approvalDesigner.phOperator')" style="width: 100px">
                <a-select-option v-for="op in getOperators(cond.field)" :key="op.value" :value="op.value">
                  {{ op.label }}
                </a-select-option>
              </a-select>
              
              <div class="value-input">
                <a-input-number
                  v-if="isNumberField(cond.field)"
                  :value="typeof cond.value === 'number' ? cond.value : Number(cond.value) || undefined"
                  :placeholder="t('approvalDesigner.phNumber')"
                  style="width: 100%"
                  @update:value="(v: number | null) => cond.value = v ?? ''"
                />
                <a-date-picker
                  v-else-if="isDateField(cond.field)"
                  :value="undefined"
                  :placeholder="t('approvalDesigner.phPickDate')"
                  style="width: 100%"
                  value-format="YYYY-MM-DD"
                  @change="(_d: unknown, dateStr: string) => cond.value = dateStr"
                />
                <a-select
                  v-else-if="getFieldOptions(cond.field).length > 0"
                  v-model:value="cond.value"
                  :placeholder="t('approvalDesigner.phPickValue')"
                  style="width: 100%"
                  allow-clear
                  show-search
                >
                  <a-select-option
                    v-for="opt in getFieldOptions(cond.field)"
                    :key="String(opt.value)"
                    :value="opt.value"
                  >
                    {{ opt.label }}
                  </a-select-option>
                </a-select>
                <a-input v-else v-model:value="cond.value" :placeholder="t('approvalDesigner.phCompareValue')" />
              </div>
              
              <a-button type="text" danger size="small" class="delete-btn" @click="removeCondition(gIndex, cIndex)">
                <CloseOutlined />
              </a-button>
            </div>
          </div>
          
          <a-button type="dashed" size="small" block @click="addCondition(gIndex)">
            <PlusOutlined /> {{ t('approvalDesigner.condBtnAddCondition') }}
          </a-button>
        </div>

        <div v-if="gIndex < modelValue.length - 1" class="group-divider">
          <span>{{ t('approvalDesigner.condOrHint') }}</span>
        </div>
      </div>
      
      <a-button type="dashed" block style="margin-top: 8px" @click="addGroup">
        <PlusOutlined /> {{ t('approvalDesigner.condBtnAddGroupOr') }}
      </a-button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n';
import { PlusOutlined, DeleteOutlined, CloseOutlined } from '@ant-design/icons-vue';

const { t } = useI18n();
import type { ConditionGroup, ConditionExpression } from '@/types/approval-tree';
import type { LfFormField } from '@/types/approval-definition';

const props = defineProps<{
  modelValue: ConditionGroup[];
  formFields?: LfFormField[];
}>();

const emit = defineEmits<{
  'update:modelValue': [value: ConditionGroup[]];
}>();

// ── Operators (labels follow locale) ──
function commonOperators() {
  return [
    { label: t('approvalDesigner.condOpEquals'), value: 'equals' },
    { label: t('approvalDesigner.condOpNotEquals'), value: 'notEquals' },
  ];
}

function numberOperators() {
  return [
    ...commonOperators(),
    { label: t('approvalDesigner.condOpGreaterThan'), value: 'greaterThan' },
    { label: t('approvalDesigner.condOpLessThan'), value: 'lessThan' },
    { label: t('approvalDesigner.condOpGreaterOrEqual'), value: 'greaterThanOrEqual' },
    { label: t('approvalDesigner.condOpLessOrEqual'), value: 'lessThanOrEqual' },
  ];
}

function stringOperators() {
  return [
    ...commonOperators(),
    { label: t('approvalDesigner.condOpContains'), value: 'contains' },
    { label: t('approvalDesigner.condOpStartsWith'), value: 'startsWith' },
    { label: t('approvalDesigner.condOpEndsWith'), value: 'endsWith' },
  ];
}

function getOperators(fieldId: string) {
  if (!props.formFields) return commonOperators();
  const field = props.formFields.find(f => (f.fieldId || f.id) === fieldId);
  if (!field) return commonOperators();

  const fieldType = (field.fieldType || field.widgetType || '').toLowerCase();
  const valueType = (field.valueType || '').toLowerCase();
  if (
    ['number', 'integer', 'decimal'].some((it) => valueType.includes(it))
    || ['input-number', 'slider', 'rate', 'number'].some((it) => fieldType.includes(it))
  ) {
    return numberOperators();
  }
  if (['string', 'text'].some((it) => valueType.includes(it))) {
    return stringOperators();
  }
  return stringOperators();
}

function onFieldChange(cond: ConditionExpression) {
  // 重置操作符和值
  cond.operator = 'equals';
  cond.value = '';
  // 更新字段类型（用于 UI 渲染）
  const field = props.formFields?.find(f => (f.fieldId || f.id) === cond.field);
  if (field) {
    cond.fieldType = field.fieldType || field.widgetType || 'string';
  }
}

/** 判断字段是否为数字类型 */
function isNumberField(fieldId: string): boolean {
  if (!props.formFields) return false;
  const field = props.formFields.find(f => (f.fieldId || f.id) === fieldId);
  if (!field) return false;
  const fieldType = (field.fieldType || field.widgetType || '').toLowerCase();
  const valueType = (field.valueType || '').toLowerCase();
  return (
    ['number', 'integer', 'decimal', 'float', 'double', 'currency'].some(t => valueType.includes(t)) ||
    ['input-number', 'slider', 'rate', 'number'].some(t => fieldType.includes(t))
  );
}

/** 判断字段是否为日期类型 */
function isDateField(fieldId: string): boolean {
  if (!props.formFields) return false;
  const field = props.formFields.find(f => (f.fieldId || f.id) === fieldId);
  if (!field) return false;
  const fieldType = (field.fieldType || field.widgetType || '').toLowerCase();
  const valueType = (field.valueType || '').toLowerCase();
  return (
    ['date', 'datetime', 'time'].some(t => valueType.includes(t)) ||
    ['date-picker', 'time-picker', 'date-range'].some(t => fieldType.includes(t))
  );
}

/** 获取字段的选项列表（用于 select/radio/checkbox 类型） */
function getFieldOptions(fieldId: string): Array<{ label: string; value: string | number }> {
  if (!props.formFields) return [];
  const field = props.formFields.find(f => (f.fieldId || f.id) === fieldId);
  if (!field) return [];
  // 检查字段是否有预定义的选项列表
  const options = (field as unknown as Record<string, unknown>).options;
  if (Array.isArray(options)) {
    return options.map((opt: Record<string, unknown>) => ({
      label: String(opt.label ?? opt.text ?? opt.value ?? ''),
      value: opt.value as string | number,
    }));
  }
  return [];
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