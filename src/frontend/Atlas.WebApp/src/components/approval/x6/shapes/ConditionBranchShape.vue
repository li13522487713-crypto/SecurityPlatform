<template>
  <div
    class="dd-node dd-node--condition-branch"
    :class="{ 'is-error': data.error }"
    @click="handleClick"
  >
    <div class="dd-node__header dd-node__header--condition">
      <span class="dd-node__move-btns">
        <LeftOutlined
          v-if="!isFirst"
          class="dd-node__move-btn"
          :title="t('approvalDesigner.shapeMoveLeftTitle')"
          @click.stop="handleMove('left')"
        />
      </span>
      <span class="dd-node__title">{{ data.branchName || t('approvalDesigner.nodeWidgetCondition') }}</span>
      <span v-if="!data.isDefault" class="dd-node__priority">{{ t('approvalDesigner.shapePriority', { index: branchIndex }) }}</span>
      <span v-else class="dd-node__priority">{{ t('approvalDesigner.shapeDefaultBranch') }}</span>
      <span class="dd-node__header-actions">
        <RightOutlined
          v-if="!isLast"
          class="dd-node__move-btn"
          :title="t('approvalDesigner.shapeMoveRightTitle')"
          @click.stop="handleMove('right')"
        />
        <CloseOutlined class="dd-node__delete" @click.stop="handleDelete" />
      </span>
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
import { CloseOutlined, RightOutlined, LeftOutlined } from '@ant-design/icons-vue';
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

const totalBranches = computed(() => {
  return (data.value._totalBranches as number) ?? 2;
});

const isFirst = computed(() => branchIndex.value <= 1);
const isLast = computed(() => branchIndex.value >= totalBranches.value);

const conditionLabel = computed(() => {
  // 优先使用 Store 计算的展示标签
  if (data.value._displayLabel) {
    return data.value._displayLabel as string;
  }

  // 回退到本地计算
  // 新版条件组
  const groups = data.value.conditionGroups as Array<{ conditions: Array<Record<string, unknown>> }> | undefined;
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

const handleMove = (direction: 'left' | 'right') => {
  const node = getNode();
  node.trigger('branch:move', {
    conditionNodeId: data.value._conditionNodeId,
    branchId: data.value.id,
    direction,
  });
};
</script>

<style scoped>
.dd-node__move-btns {
  display: inline-flex;
  align-items: center;
  min-width: 14px;
}

.dd-node__header-actions {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  margin-left: auto;
}

.dd-node__move-btn {
  font-size: 10px;
  color: rgba(255, 255, 255, 0.65);
  cursor: pointer;
  padding: 2px;
  border-radius: 2px;
  transition: all 0.2s;
}

.dd-node__move-btn:hover {
  color: #fff;
  background: rgba(255, 255, 255, 0.2);
}
</style>
