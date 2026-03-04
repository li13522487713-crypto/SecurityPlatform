<template>
  <div
    class="dd-node dd-node--approve"
    :class="{ 'is-error': data.error }"
    @click="handleClick"
  >
    <div class="dd-node__header dd-node__header--approve">
      <UserOutlined class="dd-node__icon" />
      <span class="dd-node__title">{{ data.nodeName || '审批人' }}</span>
      <CloseOutlined class="dd-node__delete" @click.stop="handleDelete" />
    </div>
    <div class="dd-node__body">
      <span v-if="assigneeLabel" class="dd-node__text">{{ assigneeLabel }}</span>
      <span v-else class="dd-node__placeholder">请选择审批人</span>
      <RightOutlined class="dd-node__arrow" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { inject, ref, computed, onMounted } from 'vue';
import { UserOutlined, CloseOutlined, RightOutlined } from '@ant-design/icons-vue';
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

const ASSIGNEE_TYPE_MAP: Record<number, string> = {
  0: '指定人员',
  1: '指定角色',
  2: '部门负责人',
  3: '逐级领导',
  4: '指定层级',
  5: '直属领导',
  6: '发起人',
  7: 'HRBP',
  8: '发起人自选',
  9: '业务字段取人',
  10: '外部传入人员',
};

const assigneeLabel = computed(() => {
  const val = data.value.assigneeValue as string;
  const typeNum = (data.value.assigneeType ?? 0) as number;
  const typeName = ASSIGNEE_TYPE_MAP[typeNum] || '指定人员';
  if (!val) {
    if (typeNum === 2 || typeNum === 3 || typeNum === 5 || typeNum === 6 || typeNum === 7 || typeNum === 8) {
      return typeName;
    }
    return '';
  }
  return `${typeName}: ${val}`;
});

const handleClick = () => {
  const node = getNode();
  node.trigger('node:select', { nodeData: data.value });
};

const handleDelete = () => {
  const node = getNode();
  node.trigger('node:delete', { nodeId: data.value.id });
};
</script>
