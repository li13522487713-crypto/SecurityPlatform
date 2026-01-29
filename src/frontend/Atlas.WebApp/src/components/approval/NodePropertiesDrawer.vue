<template>
  <a-drawer
    :open="open"
    title="节点属性"
    placement="right"
    width="400"
    @close="handleClose"
  >
    <a-form :model="formData" layout="vertical" v-if="formData">
      <a-form-item label="节点名称" v-if="'nodeName' in formData">
        <a-input v-model:value="formData.nodeName" />
      </a-form-item>
      
      <!-- 审批节点属性 -->
      <template v-if="formData.nodeType === 'approve'">
        <a-form-item label="审批人类型">
          <a-select v-model:value="formData.assigneeType">
            <a-select-option :value="0">指定用户</a-select-option>
            <a-select-option :value="1">按角色</a-select-option>
            <a-select-option :value="2">部门负责人</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item label="审批人值">
          <a-input v-model:value="formData.assigneeValue" placeholder="用户ID/角色代码/部门ID" />
        </a-form-item>
        <a-form-item label="审批模式">
          <a-select v-model:value="formData.approvalMode">
            <a-select-option value="all">会签（全部通过）</a-select-option>
            <a-select-option value="any">或签（任一通过）</a-select-option>
          </a-select>
        </a-form-item>
      </template>

      <!-- 抄送节点属性 -->
      <template v-if="formData.nodeType === 'copy'">
        <a-form-item label="抄送人">
           <a-select mode="tags" v-model:value="formData.copyToUsers" placeholder="输入用户ID" />
        </a-form-item>
      </template>

      <!-- 条件分支属性 -->
      <template v-if="'branchName' in formData">
         <a-form-item label="分支名称">
            <a-input v-model:value="formData.branchName" />
         </a-form-item>
         <a-form-item label="默认分支">
            <a-switch v-model:checked="formData.isDefault" />
         </a-form-item>
         <template v-if="!formData.isDefault">
             <a-divider>条件规则</a-divider>
             <div v-if="!formData.conditionRule">
                 <a-button type="dashed" block @click="initConditionRule">添加规则</a-button>
             </div>
             <div v-else>
                 <a-form-item label="字段">
                    <a-input v-model:value="formData.conditionRule.field" />
                 </a-form-item>
                 <a-form-item label="运算符">
                    <a-select v-model:value="formData.conditionRule.operator">
                        <a-select-option value="equals">等于</a-select-option>
                        <a-select-option value="notEquals">不等于</a-select-option>
                        <a-select-option value="greaterThan">大于</a-select-option>
                        <a-select-option value="lessThan">小于</a-select-option>
                        <a-select-option value="contains">包含</a-select-option>
                    </a-select>
                 </a-form-item>
                 <a-form-item label="值">
                    <a-input v-model:value="formData.conditionRule.value" />
                 </a-form-item>
                 <a-button type="link" danger @click="removeConditionRule">删除规则</a-button>
             </div>
         </template>
      </template>

      <a-form-item>
        <a-button type="primary" @click="handleSave">确定</a-button>
      </a-form-item>
    </a-form>
  </a-drawer>
</template>

<script setup lang="ts">
import { ref, watch } from 'vue';
import type { TreeNode, ConditionBranch } from '@/types/approval-tree';

const props = defineProps<{
  open: boolean;
  node: TreeNode | ConditionBranch | null;
}>();

const emit = defineEmits<{
  'update:open': [value: boolean];
  'update': [node: TreeNode | ConditionBranch];
}>();

const formData = ref<any>(null);

watch(() => props.node, (newNode) => {
  if (newNode) {
    formData.value = JSON.parse(JSON.stringify(newNode));
  } else {
    formData.value = null;
  }
}, { immediate: true });

const handleClose = () => {
  emit('update:open', false);
};

const handleSave = () => {
  if (formData.value) {
    emit('update', formData.value);
    handleClose();
  }
};

const initConditionRule = () => {
    formData.value.conditionRule = {
        field: '',
        operator: 'equals',
        value: ''
    };
};

const removeConditionRule = () => {
    formData.value.conditionRule = undefined;
};
</script>
