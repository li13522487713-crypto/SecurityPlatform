<template>
  <div class="branch-wrap">
    <div class="branch-box" @click="handleClick">
      <div class="branch-title">
        <span class="name">{{ branch.branchName }}</span>
        <span v-if="branch.isDefault" class="priority">{{ t('approvalDesigner.branchTagDefault') }}</span>
        <CloseOutlined v-if="!branch.isDefault" class="close-btn" @click.stop="handleDelete" />
      </div>
      <div class="branch-content">
        <span v-if="branch.isDefault">{{ t('approvalDesigner.branchElseFlow') }}</span>
        <span v-else>{{ getConditionLabel(branch) }}</span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n';
import { CloseOutlined } from '@ant-design/icons-vue';
import type { ConditionBranch } from '@/types/approval-tree';

const { t } = useI18n();

function operatorLabel(op: string): string {
  const map: Record<string, string> = {
    equals: t('approvalDesigner.condOpEquals'),
    notEquals: t('approvalDesigner.condOpNotEquals'),
    greaterThan: t('approvalDesigner.condOpGreaterThan'),
    lessThan: t('approvalDesigner.condOpLessThan'),
    contains: t('approvalDesigner.condOpContains'),
    greaterThanOrEqual: t('approvalDesigner.condOpGreaterOrEqual'),
    lessThanOrEqual: t('approvalDesigner.condOpLessOrEqual'),
    in: t('approvalDesigner.condOpInList'),
    startsWith: t('approvalDesigner.condOpStartsWith'),
    endsWith: t('approvalDesigner.condOpEndsWith'),
  };
  return map[op] || op;
}

const props = defineProps<{
  branch: ConditionBranch;
}>();

const emit = defineEmits<{
  click: [branch: ConditionBranch];
  delete: [branchId: string];
}>();

const handleClick = () => {
  emit('click', props.branch);
};

const handleDelete = () => {
  emit('delete', props.branch.id);
};

const getConditionLabel = (branch: ConditionBranch) => {
  if (!branch.conditionRule) return t('approvalDesigner.branchSetCondition');
  return `${branch.conditionRule.field} ${operatorLabel(branch.conditionRule.operator)} ${branch.conditionRule.value}`;
};
</script>

<style scoped>
.branch-wrap {
  display: flex;
  flex-direction: column;
  align-items: center;
  width: 220px;
}

.branch-box {
  background: var(--color-bg-container);
  border-radius: 4px;
  cursor: pointer;
  width: 100%;
  position: relative;
  box-shadow: 0 2px 5px 0 rgba(0, 0, 0, 0.1);
}

.branch-title {
  padding: 5px 10px;
  font-size: 12px;
  border-bottom: 1px solid #eee;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.branch-title .name {
  color: #15bc83;
}

.branch-title .priority {
  color: #999;
  font-size: 12px;
}

.branch-content {
  padding: 10px;
  font-size: 12px;
  color: var(--color-text-tertiary);
}

.close-btn {
  display: none;
  font-size: 12px;
  color: #999;
}

.branch-box:hover .close-btn {
  display: block;
}

.close-btn:hover {
  color: var(--color-error);
}
</style>
