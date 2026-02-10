<template>
  <div
    class="dd-node dd-node--timer"
    :class="{ 'is-error': data.error }"
    @click="handleClick"
  >
    <div class="dd-node__header dd-node__header--timer">
      <ClockCircleOutlined class="dd-node__icon" />
      <span class="dd-node__title">{{ data.nodeName || '定时器' }}</span>
      <CloseOutlined class="dd-node__delete" @click.stop="handleDelete" />
    </div>
    <div class="dd-node__body">
      <template v-if="data.timerConfig">
        <span class="dd-node__text" v-if="(data.timerConfig as any).type === 'duration'">等待 {{ (data.timerConfig as any).duration }} 秒</span>
        <span class="dd-node__text" v-else-if="(data.timerConfig as any).type === 'date'">至 {{ (data.timerConfig as any).date }}</span>
        <span class="dd-node__placeholder" v-else>请配置时间</span>
      </template>
      <span class="dd-node__placeholder" v-else>请配置时间</span>
      <RightOutlined class="dd-node__arrow" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { inject, ref, onMounted } from 'vue';
import { ClockCircleOutlined, CloseOutlined, RightOutlined } from '@ant-design/icons-vue';
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
.dd-node__header--timer {
  background: #f5222d;
}
</style>
