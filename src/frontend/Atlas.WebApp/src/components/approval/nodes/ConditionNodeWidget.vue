<template>
  <div class="condition-node-box">
    <button class="add-branch-btn" @click="handleAddBranch">添加条件</button>
  </div>
</template>

<script setup lang="ts">
import type { ConditionNode } from '@/types/approval-tree';

const props = defineProps<{
  node: ConditionNode;
}>();

const emit = defineEmits<{
  addBranch: [nodeId: string];
}>();

const handleAddBranch = () => {
  emit('addBranch', props.node.id);
};
</script>

<style scoped>
.condition-node-box {
  padding: 10px 0;
  display: flex;
  justify-content: center;
  position: relative;
  z-index: 1;
}
.add-branch-btn {
  background: #fff;
  border: 1px solid #15bc83;
  color: #15bc83;
  border-radius: 15px;
  padding: 5px 15px;
  font-size: 12px;
  cursor: pointer;
  box-shadow: 0 2px 4px 0 rgba(0, 0, 0, 0.1);
  transition: all 0.3s;
}
.add-branch-btn:hover {
  background: #15bc83;
  color: #fff;
}
</style>
