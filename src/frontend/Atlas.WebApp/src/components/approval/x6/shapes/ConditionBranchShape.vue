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
          @click.stop="handleMove('left')"
          title="左移"
        />
      </span>
      <span class="dd-node__title">{{ data.branchName || '条件' }}</span>
      <span class="dd-node__priority" v-if="!data.isDefault">优先级{{ branchIndex }}</span>
      <span class="dd-node__priority" v-else>默认</span>
      <span class="dd-node__header-actions">
        <RightOutlined
          v-if="!isLast"
          class="dd-node__move-btn"
          @click.stop="handleMove('right')"
          title="右移"
        />
        <CloseOutlined class="dd-node__delete" @click.stop="handleDelete" />
      </span>
    </div>
    <div class="dd-node__body">
      <span v-if="conditionLabel" class="dd-node__text">{{ conditionLabel }}</span>
      <span v-else-if="data.isDefault" class="dd-node__placeholder">其他条件均不满足时进入此分支</span>
      <span v-else class="dd-node__placeholder">请设置条件</span>
      <RightOutlined class="dd-node__arrow" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { inject, ref, computed, onMounted } from 'vue';
import { CloseOutlined, RightOutlined, LeftOutlined } from '@ant-design/icons-vue';
import type { Node } from '@antv/x6';

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
  // 新版条件组
  const groups = data.value.conditionGroups as Array<{ conditions: Array<any> }> | undefined;
  if (groups && groups.length > 0) {
    const count = groups.reduce((acc, g) => acc + (g.conditions?.length || 0), 0);
    return `${groups.length}个条件组, 共${count}个条件`;
  }

  // 旧版兼容
  const rule = data.value.conditionRule as
    | { field: string; operator: string; value: unknown }
    | undefined;
  if (!rule || !rule.field) return '';
  const opMap: Record<string, string> = {
    equals: '等于',
    notEquals: '不等于',
    greaterThan: '大于',
    lessThan: '小于',
    contains: '包含',
    greaterThanOrEqual: '大于等于',
    lessThanOrEqual: '小于等于',
    in: '在列表中',
    startsWith: '开头是',
    endsWith: '结尾是',
  };
  return `${rule.field} ${opMap[rule.operator] || rule.operator} ${rule.value}`;
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
