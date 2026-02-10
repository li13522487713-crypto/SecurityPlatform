<template>
  <div class="node-wrap">
    <div class="node-wrap-box call-process-node" :class="{ 'error': node.error }" @click="handleClick">
      <div class="title">
        <SubnodeOutlined class="icon" />
        <span class="name">{{ node.nodeName }}</span>
        <CloseOutlined class="close-btn" @click.stop="handleDelete" />
      </div>
      <div class="content">
        <span class="text" v-if="node.callProcessId">流程ID: {{ node.callProcessId }}</span>
        <span class="placeholder" v-else>请配置子流程</span>
        <div class="sub-text" v-if="node.callProcessId">
          {{ node.callAsync ? '异步执行' : '同步等待' }}
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { SubnodeOutlined, CloseOutlined } from '@ant-design/icons-vue';
import type { CallProcessNode } from '@/types/approval-tree';

const props = defineProps<{
  node: CallProcessNode;
}>();

const emit = defineEmits<{
  click: [node: CallProcessNode];
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

.call-process-node .title {
  background: #faad14;
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

.sub-text {
  font-size: 12px;
  color: #8c8c8c;
  margin-top: 4px;
}

.placeholder {
  color: var(--color-text-quaternary);
}

.error {
  border: 1px solid #f00;
  box-shadow: 0 2px 5px 0 rgba(255, 0, 0, 0.1);
}
</style>
