<template>
  <div class="node-wrap">
    <div class="node-wrap-box end-node">
      <div class="content">
        <span class="text">流程结束</span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import type { EndNode } from '@/types/approval-tree';

defineProps<{
  node: EndNode;
}>();
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
  border-radius: 50%;
  width: 60px;
  height: 60px;
  cursor: default;
  background: #f5f5f7;
  display: flex;
  justify-content: center;
  align-items: center;
  color: rgba(25, 31, 37, 0.4);
  box-shadow: 0 2px 5px 0 rgba(0, 0, 0, 0.1);
}

.content {
  font-size: 14px;
}
</style>
