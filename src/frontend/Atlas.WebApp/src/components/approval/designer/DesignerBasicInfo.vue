<template>
  <a-form layout="vertical" class="dd-basic-form">
    <a-form-item label="流程名称">
      <a-input v-model:value="flowNameModel" :maxlength="100" placeholder="请输入流程名称" />
    </a-form-item>
    <a-form-item label="流程分类">
      <a-input v-model:value="definitionMeta.category" placeholder="如：采购/人事/财务" />
    </a-form-item>
    <a-form-item label="流程说明">
      <a-textarea v-model:value="definitionMeta.description" :rows="3" />
    </a-form-item>
    
    <a-form-item label="可见范围">
      <a-radio-group v-model:value="visibilityScopeTypeModel" style="margin-bottom: 12px">
        <a-radio value="All">全部可见</a-radio>
        <a-radio value="Department">指定部门</a-radio>
        <a-radio value="Role">指定角色</a-radio>
        <a-radio value="User">指定人员</a-radio>
      </a-radio-group>
      
      <div v-if="visibilityScopeTypeModel !== 'All'">
        <UserRolePicker
          v-if="visibilityScopeTypeModel === 'Department'"
          mode="department"
          v-model:value="visibilityScopeIdsModel"
          placeholder="请选择部门"
        />
        <UserRolePicker
          v-else-if="visibilityScopeTypeModel === 'Role'"
          mode="role"
          v-model:value="visibilityScopeIdsModel"
          placeholder="请选择角色"
        />
        <UserRolePicker
          v-else-if="visibilityScopeTypeModel === 'User'"
          mode="user"
          v-model:value="visibilityScopeIdsModel"
          placeholder="请选择人员"
        />
      </div>
    </a-form-item>

    <a-space>
      <a-switch v-model:checked="definitionMeta.isQuickEntry" /> <span>快捷入口</span>
      <a-switch v-model:checked="definitionMeta.isLowCodeFlow" /> <span>启用低代码表单</span>
    </a-space>
  </a-form>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import UserRolePicker from '@/components/common/UserRolePicker.vue';
import type { ApprovalDefinitionMeta } from '@/types/approval-definition';

const props = defineProps<{
  flowName: string;
  definitionMeta: ApprovalDefinitionMeta;
  visibilityScopeType: 'All' | 'Department' | 'Role' | 'User';
  visibilityScopeIds: string[];
}>();

const emit = defineEmits<{
  'update:flowName': [value: string];
  'update:visibilityScopeType': [value: 'All' | 'Department' | 'Role' | 'User'];
  'update:visibilityScopeIds': [value: string[]];
}>();

const flowNameModel = computed({
  get: () => props.flowName,
  set: (val) => emit('update:flowName', val)
});

const visibilityScopeTypeModel = computed({
  get: () => props.visibilityScopeType,
  set: (val) => {
    emit('update:visibilityScopeType', val);
    emit('update:visibilityScopeIds', []); // Reset ids when type changes
  }
});

const visibilityScopeIdsModel = computed({
  get: () => props.visibilityScopeIds,
  set: (val) => emit('update:visibilityScopeIds', val)
});
</script>

<style scoped>
.dd-basic-form {
  max-width: 600px;
}
</style>
