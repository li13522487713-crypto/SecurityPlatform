<template>
  <div
    class="dd-node dd-node--route"
    :class="{ 'is-error': data.error }"
    @click="handleClick"
  >
    <div class="dd-node__header dd-node__header--route">
      <SwapOutlined class="dd-node__icon" />
      <span class="dd-node__title">{{ data.nodeName || '路由节点' }}</span>
      <CloseOutlined class="dd-node__delete" @click.stop="handleDelete" />
    </div>
    <div class="dd-node__body">
      <span v-if="data.routeTargetNodeId" class="dd-node__text">跳转至: {{ data.routeTargetNodeId }}</span>
      <span v-else class="dd-node__placeholder">请选择目标节点</span>
      <RightOutlined class="dd-node__arrow" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { inject, ref, onMounted } from 'vue';
import { SwapOutlined, CloseOutlined, RightOutlined } from '@ant-design/icons-vue';
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

const handleClick = () => {
  const node = getNode();
  node.trigger('node:select', { nodeData: data.value });
};

const handleDelete = () => {
  const node = getNode();
  node.trigger('node:delete', { nodeId: data.value.id });
};
</script>

<style scoped>
.dd-node__header--route {
  background: #718dff;
}
</style>
