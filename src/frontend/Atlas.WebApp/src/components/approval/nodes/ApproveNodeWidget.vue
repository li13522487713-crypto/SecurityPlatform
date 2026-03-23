<template>
  <div class="node-wrap">
    <div class="node-wrap-box approve-node" :class="{ 'error': node.error }" @click="handleClick">
      <div class="title">
        <UserOutlined class="icon" />
        <span class="name">{{ node.nodeName }}</span>
        <CloseOutlined class="close-btn" @click.stop="handleDelete" />
      </div>
      <div class="content">
        <span v-if="node.assigneeValue || !needsAssigneeValue(node.assigneeType)" class="text">{{ getAssigneeLabel(node) }}</span>
        <span v-else class="placeholder">{{ t('approvalDesigner.shapePickApprover') }}</span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { useI18n } from 'vue-i18n';
import { UserOutlined, CloseOutlined } from '@ant-design/icons-vue';
import type { ApproveNode } from '@/types/approval-tree';

const { t } = useI18n();

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

const assigneeTypeName = (type: ApproveNode['assigneeType']): string => {
  switch (type) {
    case 0:
      return t('approvalDesigner.widgetAssigneeUser');
    case 1:
      return t('approvalDesigner.widgetAssigneeRole');
    case 2:
      return t('approvalDesigner.assigneeDeptLeader');
    case 3:
      return t('approvalDesigner.assigneeEscalation');
    case 4:
      return t('approvalDesigner.assigneeLevel');
    case 5:
      return t('approvalDesigner.assigneeDirectLeader');
    case 6:
      return t('approvalDesigner.assigneeInitiator');
    case 7:
      return t('approvalDesigner.assigneeHrbp');
    case 8:
      return t('approvalDesigner.assigneeInitiatorPick');
    case 9:
      return t('approvalDesigner.assigneeBizField');
    case 10:
      return t('approvalDesigner.assigneeExternal');
    default:
      return t('approvalDesigner.widgetAssigneeUser');
  }
};

const getAssigneeLabel = (node: ApproveNode) => {
  const name = assigneeTypeName(node.assigneeType);
  if (!node.assigneeValue) {
    return name;
  }
  return `${name}: ${node.assigneeValue}`;
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
