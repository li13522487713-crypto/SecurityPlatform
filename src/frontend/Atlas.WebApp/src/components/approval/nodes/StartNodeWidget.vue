<template>
  <div class="node-wrap">
    <div class="node-wrap-box start-node" @click="handleClick">
      <div class="title">
        <span class="name">{{ node.nodeName }}</span>
      </div>
      <div class="content">
        <span class="text">所有人</span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { StartNode } from '@/types/approval-tree';

const props = defineProps<{
  node: StartNode;
}>();

const emit = defineEmits<{
  click: [node: StartNode];
}>();

const handleClick = () => {
  emit('click', props.node);
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
  background: #fff;
  transition: all 0.3s cubic-bezier(0.645, 0.045, 0.355, 1);
  overflow: hidden;
  position: relative;
}

.start-node .title {
  background: #576a95;
}

.title {
  color: #fff;
  padding: 5px 10px;
  font-size: 12px;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.content {
  padding: 10px;
  font-size: 14px;
  color: #191f25;
}
</style>
