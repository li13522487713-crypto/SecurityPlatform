<template>
  <div class="node-wrap">
    <div class="node-wrap-box approve-node" :class="{ 'error': node.error }" @click="handleClick">
      <div class="title">
        <UserOutlined class="icon" />
        <span class="name">{{ node.nodeName }}</span>
        <CloseOutlined class="close-btn" @click.stop="handleDelete" />
      </div>
      <div class="content">
        <span class="text" v-if="node.assigneeValue || !needsAssigneeValue(node.assigneeType)">{{ getAssigneeLabel(node) }}</span>
        <span class="placeholder" v-else>请选择审批人</span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { UserOutlined, CloseOutlined } from '@ant-design/icons-vue';
import type { ApproveNode } from '@/types/approval-tree';

const props = defineProps<{
  node: ApproveNode;
}>();

const emit = defineEmits<{
  click: [node: ApproveNode];
  delete: [nodeId: string];
}>();

const handleClick = () => {
  emit('click', props.node);
};

const handleDelete = () => {
  emit('delete', props.node.id);
};

const getAssigneeLabel = (node: ApproveNode) => {
  const typeMap: Record<ApproveNode['assigneeType'], string> = {
    0: '指定用户',
    1: '角色',
    2: '部门负责人',
    3: '逐级领导',
    4: '指定层级',
    5: '直属领导',
    6: '发起人',
    7: 'HRBP',
    8: '发起人自选',
    9: '业务字段取人',
    10: '外部传入人员'
  };
  if (!node.assigneeValue) {
    return typeMap[node.assigneeType];
  }
  return `${typeMap[node.assigneeType]}: ${node.assigneeValue}`;
};

const needsAssigneeValue = (assigneeType: ApproveNode['assigneeType']): boolean => {
  return assigneeType === 0 || assigneeType === 1 || assigneeType === 4 || assigneeType === 9 || assigneeType === 10;
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

.approve-node .title {
  background: #ff943e;
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
