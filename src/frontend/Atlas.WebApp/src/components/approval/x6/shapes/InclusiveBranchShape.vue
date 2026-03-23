<template>
  <div
    class="dd-node dd-node--condition-branch"
    :class="{ 'is-error': data.error }"
    @click="handleClick"
  >
    <div class="dd-node__header dd-node__header--inclusive">
      <span class="dd-node__title">{{ data.branchName || t('approvalDesigner.shapeInclusiveDefault') }}</span>
      <span v-if="!data.isDefault" class="dd-node__priority">{{ t('approvalDesigner.shapePriority', { index: branchIndex }) }}</span>
      <span v-else class="dd-node__priority">{{ t('approvalDesigner.shapeDefaultBranch') }}</span>
      <CloseOutlined class="dd-node__delete" @click.stop="handleDelete" />
    </div>
    <div class="dd-node__body">
      <span v-if="conditionLabel" class="dd-node__text">{{ conditionLabel }}</span>
      <span v-else-if="data.isDefault" class="dd-node__placeholder">{{ t('approvalDesigner.shapeBranchFallbackHint') }}</span>
      <span v-else class="dd-node__placeholder">{{ t('approvalDesigner.shapeSetConditionHint') }}</span>
      <RightOutlined class="dd-node__arrow" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { inject, ref, computed, onMounted } from 'vue';
import { useI18n } from 'vue-i18n';
import { CloseOutlined, RightOutlined } from '@ant-design/icons-vue';
import type { Node } from '@antv/x6';

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

const getNode = inject<() => Node>('getNode')!;
const data = ref<Record<string, unknown>>({});

onMounted(() => {
  const node = getNode();
  data.value = node.getData() || {};
  node.on('change:data', ({ current }: { current: Record<string, unknown> }) => {
    data.value = { ...current };
  });
});

const branchIndex = computed(() => {
  return (data.value._branchIndex as number) ?? 1;
});

interface ConditionGroupView {
  conditions?: unknown[];
}

const conditionLabel = computed(() => {
  // 新版条件组
  const groupsRaw = data.value.conditionGroups;
  const groups = Array.isArray(groupsRaw) ? (groupsRaw as ConditionGroupView[]) : undefined;
  if (groups && groups.length > 0) {
    const count = groups.reduce((acc, g) => acc + (g.conditions?.length || 0), 0);
    return t('approvalDesigner.condGroupSummary', { groups: groups.length, count });
  }

  // 旧版兼容
  const rule = data.value.conditionRule as
    | { field: string; operator: string; value: unknown }
    | undefined;
  if (!rule || !rule.field) return '';
  return `${rule.field} ${operatorLabel(rule.operator)} ${rule.value}`;
});

const handleClick = () => {
  const node = getNode();
  node.trigger('branch:select', { branchData: data.value });
};

const handleDelete = () => {
  const node = getNode();
  node.trigger('branch:delete', { branchId: data.value.id });
};
</script>

<style scoped>
.dd-node__header--inclusive {
  background: #15bc83; /* 保持绿色，与条件分支一致 */
}
</style>
