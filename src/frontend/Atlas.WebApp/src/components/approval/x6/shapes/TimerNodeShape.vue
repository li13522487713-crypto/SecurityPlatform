<template>
  <div
    class="dd-node dd-node--timer"
    :class="{ 'is-error': data.error }"
    @click="handleClick"
  >
    <div class="dd-node__header dd-node__header--timer">
      <ClockCircleOutlined class="dd-node__icon" />
      <span class="dd-node__title">{{ data.nodeName || t('approvalDesigner.shapeTimerDefaultName') }}</span>
      <CloseOutlined class="dd-node__delete" @click.stop="handleDelete" />
    </div>
    <div class="dd-node__body">
      <template v-if="data.timerConfig">
        <span v-if="timerConfig?.type === 'duration'" class="dd-node__text">{{ t('approvalDesigner.shapeTimerWait', { seconds: timerConfig.duration }) }}</span>
        <span v-else-if="timerConfig?.type === 'date'" class="dd-node__text">{{ t('approvalDesigner.shapeTimerUntil', { date: timerConfig.date }) }}</span>
        <span v-else class="dd-node__placeholder">{{ t('approvalDesigner.shapeTimerConfigure') }}</span>
      </template>
      <span v-else class="dd-node__placeholder">{{ t('approvalDesigner.shapeTimerConfigure') }}</span>
      <RightOutlined class="dd-node__arrow" />
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, inject, ref, onMounted } from 'vue';
import { useI18n } from 'vue-i18n';
import { ClockCircleOutlined, CloseOutlined, RightOutlined } from '@ant-design/icons-vue';

const { t } = useI18n();
import type { Node } from '@antv/x6';

const getNode = inject<() => Node>('getNode')!;
const data = ref<Record<string, unknown>>({});

interface TimerConfigView {
  type?: string;
  duration?: string | number;
  date?: string;
}

const timerConfig = computed<TimerConfigView | null>(() => {
  const raw = data.value.timerConfig;
  if (!raw) {
    return null;
  }

  if (typeof raw === 'string') {
    try {
      const parsed = JSON.parse(raw) as TimerConfigView;
      return typeof parsed === 'object' && parsed ? parsed : null;
    } catch {
      return null;
    }
  }

  if (typeof raw === 'object') {
    return raw as TimerConfigView;
  }

  return null;
});

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
