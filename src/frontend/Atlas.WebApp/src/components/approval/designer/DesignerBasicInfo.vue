<template>
  <a-form layout="vertical" class="dd-basic-form">
    <a-form-item :label="t('approvalDesigner.basicFlowName')">
      <a-input v-model:value="flowNameModel" :maxlength="100" :placeholder="t('approvalDesigner.basicFlowNamePh')" />
    </a-form-item>
    <a-form-item :label="t('approvalDesigner.basicCategory')">
      <a-input v-model:value="categoryModel" :placeholder="t('approvalDesigner.basicCategoryPh')" />
    </a-form-item>
    <a-form-item :label="t('approvalDesigner.basicDescription')">
      <a-textarea v-model:value="descriptionModel" :rows="3" />
    </a-form-item>

    <a-form-item :label="t('approvalDesigner.basicVisibility')">
      <a-radio-group v-model:value="visibilityScopeTypeModel" style="margin-bottom: 12px">
        <a-radio value="All">{{ t('approvalDesigner.visAll') }}</a-radio>
        <a-radio value="Department">{{ t('approvalDesigner.visDept') }}</a-radio>
        <a-radio value="Role">{{ t('approvalDesigner.visRole') }}</a-radio>
        <a-radio value="User">{{ t('approvalDesigner.visUser') }}</a-radio>
      </a-radio-group>

      <div v-if="visibilityScopeTypeModel !== 'All'">
        <UserRolePicker
          v-if="visibilityScopeTypeModel === 'Department'"
          v-model:value="visibilityScopeIdsModel"
          mode="department"
          :placeholder="t('approvalDesigner.phSelectDept')"
        />
        <UserRolePicker
          v-else-if="visibilityScopeTypeModel === 'Role'"
          v-model:value="visibilityScopeIdsModel"
          mode="role"
          :placeholder="t('approvalDesigner.phSelectRole')"
        />
        <UserRolePicker
          v-else-if="visibilityScopeTypeModel === 'User'"
          v-model:value="visibilityScopeIdsModel"
          mode="user"
          :placeholder="t('approvalDesigner.phSelectUser')"
        />
      </div>
    </a-form-item>

    <a-space>
      <a-switch v-model:checked="quickEntryModel" /> <span>{{ t('approvalDesigner.quickEntry') }}</span>
      <a-switch v-model:checked="lowCodeFlowModel" /> <span>{{ t('approvalDesigner.lowCodeForm') }}</span>
    </a-space>
  </a-form>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import UserRolePicker from '@/components/common/UserRolePicker.vue';
import type { ApprovalDefinitionMeta } from '@/types/approval-definition';

const { t } = useI18n();

const props = defineProps<{
  flowName: string;
  definitionMeta: ApprovalDefinitionMeta;
  visibilityScopeType: 'All' | 'Department' | 'Role' | 'User';
  visibilityScopeIds: string[];
}>();

const emit = defineEmits<{
  'update:flowName': [value: string];
  'update:definitionMeta': [value: ApprovalDefinitionMeta];
  'update:visibilityScopeType': [value: 'All' | 'Department' | 'Role' | 'User'];
  'update:visibilityScopeIds': [value: string[]];
}>();

const updateDefinitionMeta = (patch: Partial<ApprovalDefinitionMeta>) => {
  emit('update:definitionMeta', {
    ...props.definitionMeta,
    ...patch
  });
};

const flowNameModel = computed({
  get: () => props.flowName,
  set: (val) => emit('update:flowName', val)
});

const categoryModel = computed({
  get: () => props.definitionMeta.category ?? '',
  set: (val: string) => updateDefinitionMeta({ category: val || undefined })
});

const descriptionModel = computed({
  get: () => props.definitionMeta.description ?? '',
  set: (val: string) => updateDefinitionMeta({ description: val || undefined })
});

const quickEntryModel = computed({
  get: () => Boolean(props.definitionMeta.isQuickEntry),
  set: (val: boolean) => updateDefinitionMeta({ isQuickEntry: val })
});

const lowCodeFlowModel = computed({
  get: () => Boolean(props.definitionMeta.isLowCodeFlow),
  set: (val: boolean) => updateDefinitionMeta({ isLowCodeFlow: val })
});

const visibilityScopeTypeModel = computed({
  get: () => props.visibilityScopeType,
  set: (val) => {
    emit('update:visibilityScopeType', val);
    emit('update:visibilityScopeIds', []);
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
