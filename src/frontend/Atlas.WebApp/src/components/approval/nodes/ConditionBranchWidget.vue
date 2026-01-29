<template>
  <div class="branch-wrap">
    <div class="branch-box" @click="handleClick">
      <div class="branch-title">
        <span class="name">{{ branch.branchName }}</span>
        <span class="priority" v-if="branch.isDefault">默认</span>
        <CloseOutlined class="close-btn" @click.stop="handleDelete" v-if="!branch.isDefault" />
      </div>
      <div class="branch-content">
        <span v-if="branch.isDefault">其他情况进入此流程</span>
        <span v-else>{{ getConditionLabel(branch) }}</span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { CloseOutlined } from '@ant-design/icons-vue';
import type { ConditionBranch } from '@/types/approval-tree';

const props = defineProps<{
  branch: ConditionBranch;
}>();

const emit = defineEmits<{
  click: [branch: ConditionBranch];
  delete: [branchId: string];
}>();

const handleClick = () => {
  emit('click', props.branch);
};

const handleDelete = () => {
  emit('delete', props.branch.id);
};

const getConditionLabel = (branch: ConditionBranch) => {
  if (!branch.conditionRule) return '请设置条件';
  return `${branch.conditionRule.field} ${branch.conditionRule.operator} ${branch.conditionRule.value}`;
};
</script>

<style scoped>
.branch-wrap {
  display: flex;
  flex-direction: column;
  align-items: center;
  width: 220px;
}

.branch-box {
  background: #fff;
  border-radius: 4px;
  cursor: pointer;
  width: 100%;
  position: relative;
  box-shadow: 0 2px 5px 0 rgba(0, 0, 0, 0.1);
}

.branch-title {
  padding: 5px 10px;
  font-size: 12px;
  border-bottom: 1px solid #eee;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.branch-title .name {
  color: #15bc83;
}

.branch-title .priority {
  color: #999;
  font-size: 12px;
}

.branch-content {
  padding: 10px;
  font-size: 12px;
  color: #666;
}

.close-btn {
  display: none;
  font-size: 12px;
  color: #999;
}

.branch-box:hover .close-btn {
  display: block;
}

.close-btn:hover {
  color: #ff4d4f;
}
</style>
