<template>
  <div class="node-wrap">
    <div class="node-wrap-box timer-node" :class="{ 'error': node.error }" @click="handleClick">
      <div class="title">
        <ClockCircleOutlined class="icon" />
        <span class="name">{{ node.nodeName }}</span>
        <CloseOutlined class="close-btn" @click.stop="handleDelete" />
      </div>
      <div class="content">
        <template v-if="node.timerConfig">
          <span class="text" v-if="node.timerConfig.type === 'duration'">等待 {{ node.timerConfig.duration }} 秒</span>
          <span class="text" v-else-if="node.timerConfig.type === 'date'">至 {{ node.timerConfig.date }}</span>
          <span class="placeholder" v-else>请配置时间</span>
        </template>
        <span class="placeholder" v-else>请配置时间</span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ClockCircleOutlined, CloseOutlined } from '@ant-design/icons-vue';
import type { TimerNode } from '@/types/approval-tree';

const props = defineProps<{
  node: TimerNode;
}>();

const emit = defineEmits<{
  click: [node: TimerNode];
  delete: [nodeId: string];
}>();

const handleClick = () => {
  emit('click', props.node);
};

const handleDelete = () => {
  emit('delete', props.node.id);
};
</script>

<style scoped>
.node-wrap {
  display: flex;
  flex-direction: column;
  justify-content: center;
  align-items: center;
  width: 220px;
  min-height: 72px;
}

.node-wrap-box {
  width: 100%;
  border-radius: 4px;
  cursor: pointer;
  box-shadow: 0 2px 5px 0 rgba(0, 0, 0, 0.1);
  background: var(--color-bg-container);
  transition: all 0.3s;
  overflow: hidden;
  position: relative;
}

.timer-node .title {
  background: #f5222d;
}

.title {
  color: var(--color-text-white);
  padding: 5px 10px;
  font-size: 12px;
  display: flex;
  align-items: center;
}

.title .icon {
  margin-right: 5px;
}

.title .name {
  flex: 1;
}

.title .close-btn {
  display: none;
  font-size: 12px;
}

.node-wrap-box:hover .close-btn {
  display: block;
}

.content {
  padding: 10px;
  font-size: 14px;
  color: #191f25;
}

.placeholder {
  color: var(--color-text-quaternary);
}

.error {
  border: 1px solid #f00;
  box-shadow: 0 2px 5px 0 rgba(255, 0, 0, 0.1);
}
</style>
