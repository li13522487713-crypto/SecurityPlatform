<template>
  <a-drawer
    :open="open"
    :title="t('approvalDesigner.drawerPropsTitle')"
    placement="right"
    width="400"
    @close="handleClose"
  >
    <a-form v-if="formData" :model="formData" layout="vertical">
      <a-form-item v-if="'nodeName' in formData" :label="t('approvalDesigner.propsPhNodeName')">
        <a-input v-model:value="formData.nodeName" />
      </a-form-item>
      
      <template v-if="approveNode && approverConfig">
        <a-tabs>
          <a-tab-pane key="approver" :tab="t('approvalDesigner.propsTabApprover')">
            <a-form-item :label="t('approvalDesigner.propsLabelAssigneeType')">
              <a-select v-model:value="approverConfig.setType">
                <a-select-option :value="0">{{ t('approvalDesigner.assigneeUser') }}</a-select-option>
                <a-select-option :value="1">{{ t('approvalDesigner.assigneeRole') }}</a-select-option>
                <a-select-option :value="2">{{ t('approvalDesigner.assigneeDeptLeader') }}</a-select-option>
                <a-select-option :value="3">{{ t('approvalDesigner.assigneeHrbp') }}</a-select-option>
                <a-select-option :value="4">{{ t('approvalDesigner.assigneeDirectLeader') }}</a-select-option>
                <a-select-option :value="5">{{ t('approvalDesigner.drawerAssigneeLevelLeader') }}</a-select-option>
                <a-select-option :value="6">{{ t('approvalDesigner.assigneeInitiator') }}</a-select-option>
                <a-select-option :value="7">{{ t('approvalDesigner.assigneeInitiatorPick') }}</a-select-option>
              </a-select>
            </a-form-item>
            <a-form-item :label="t('approvalDesigner.drawerApproverList')">
              <a-select
                v-model:value="approverTargets"
                mode="tags"
                :placeholder="t('approvalDesigner.drawerPhApproverIds')"
                @change="syncApproverTargets"
              />
            </a-form-item>
            <a-form-item :label="t('approvalDesigner.drawerSignType')">
              <a-select v-model:value="approverConfig.signType">
                <a-select-option :value="1">{{ t('approvalDesigner.propsModeAllTitle') }}</a-select-option>
                <a-select-option :value="2">{{ t('approvalDesigner.propsModeAnyTitle') }}</a-select-option>
                <a-select-option :value="3">{{ t('approvalDesigner.drawerSignSequential') }}</a-select-option>
              </a-select>
            </a-form-item>
            <a-form-item :label="t('approvalDesigner.drawerNoHeaderPolicy')">
              <a-select v-model:value="approverConfig.noHeaderAction">
                <a-select-option :value="0">{{ t('approvalDesigner.propsNoHeader0') }}</a-select-option>
                <a-select-option :value="1">{{ t('approvalDesigner.drawerSkip') }}</a-select-option>
                <a-select-option :value="2">{{ t('approvalDesigner.propsNoHeader2') }}</a-select-option>
              </a-select>
            </a-form-item>
          </a-tab-pane>
          <a-tab-pane key="permissions" :tab="t('approvalDesigner.drawerExtTab')">
            <a-form-item :label="t('approvalDesigner.drawerBtnPermJson')">
              <a-textarea v-model:value="buttonPermissionText" :rows="4" />
            </a-form-item>
            <a-form-item :label="t('approvalDesigner.drawerFormPermJson')">
              <a-textarea v-model:value="formPermissionText" :rows="4" />
            </a-form-item>
            <a-form-item :label="t('approvalDesigner.drawerNoticeJson')">
              <a-textarea v-model:value="noticeConfigText" :rows="3" />
            </a-form-item>
          </a-tab-pane>
          <a-tab-pane key="legacy" :tab="t('approvalDesigner.drawerLegacyTab')">
            <a-form-item :label="t('approvalDesigner.drawerLegacyAssigneeType')">
              <a-select v-model:value="approveNode.assigneeType">
                <a-select-option :value="0">{{ t('approvalDesigner.drawerSpecifyUser') }}</a-select-option>
                <a-select-option :value="1">{{ t('approvalDesigner.drawerByRole') }}</a-select-option>
                <a-select-option :value="2">{{ t('approvalDesigner.assigneeDeptLeader') }}</a-select-option>
              </a-select>
            </a-form-item>
            <a-form-item :label="t('approvalDesigner.drawerLegacyAssigneeValue')">
              <a-input v-model:value="approveNode.assigneeValue" :placeholder="t('approvalDesigner.drawerPhLegacyAssignee')" />
            </a-form-item>
            <a-form-item :label="t('approvalDesigner.drawerLegacyMode')">
              <a-select v-model:value="approveNode.approvalMode">
                <a-select-option value="all">{{ t('approvalDesigner.drawerModeAllLong') }}</a-select-option>
                <a-select-option value="any">{{ t('approvalDesigner.drawerModeAnyLong') }}</a-select-option>
                <a-select-option value="sequential">{{ t('approvalDesigner.drawerModeSeqLong') }}</a-select-option>
              </a-select>
            </a-form-item>
          </a-tab-pane>
        </a-tabs>
      </template>

      <template v-if="copyNode">
        <a-form-item :label="t('approvalDesigner.propsCopyRecipients')">
           <a-select v-model:value="copyNode.copyToUsers" mode="tags" :placeholder="t('approvalDesigner.drawerPhUserIds')" />
        </a-form-item>
      </template>

      <template v-if="branchNode">
         <a-form-item :label="t('approvalDesigner.drawerBranchName')">
            <a-input v-model:value="branchNode.branchName" />
         </a-form-item>
         <a-form-item :label="t('approvalDesigner.drawerDefaultBranch')">
            <a-switch v-model:checked="branchNode.isDefault" />
         </a-form-item>
         <template v-if="!branchNode.isDefault">
             <a-divider>{{ t('approvalDesigner.propsDividerConditionRules') }}</a-divider>
             <div v-if="!branchNode.conditionRule">
                 <a-button type="dashed" block @click="initConditionRule">{{ t('approvalDesigner.drawerAddRule') }}</a-button>
             </div>
             <div v-else>
                 <a-form-item :label="t('approvalDesigner.drawerField')">
                    <a-input v-model:value="branchNode.conditionRule.field" />
                 </a-form-item>
                 <a-form-item :label="t('approvalDesigner.phOperator')">
                    <a-select v-model:value="branchNode.conditionRule.operator">
                        <a-select-option value="equals">{{ t('approvalDesigner.condOpEquals') }}</a-select-option>
                        <a-select-option value="notEquals">{{ t('approvalDesigner.condOpNotEquals') }}</a-select-option>
                        <a-select-option value="greaterThan">{{ t('approvalDesigner.condOpGreaterThan') }}</a-select-option>
                        <a-select-option value="lessThan">{{ t('approvalDesigner.condOpLessThan') }}</a-select-option>
                        <a-select-option value="contains">{{ t('approvalDesigner.condOpContains') }}</a-select-option>
                    </a-select>
                 </a-form-item>
                 <a-form-item :label="t('approvalDesigner.phCompareValue')">
                    <a-input v-model:value="branchNode.conditionRule.value" />
                 </a-form-item>
                 <a-button type="link" danger @click="removeConditionRule">{{ t('approvalDesigner.drawerRemoveRule') }}</a-button>
             </div>
         </template>
      </template>

      <a-form-item>
        <a-button type="primary" @click="handleSave">{{ t('approvalDesigner.propsFooterOk') }}</a-button>
      </a-form-item>
    </a-form>
  </a-drawer>
</template>

<script setup lang="ts">
import { computed, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { message } from 'ant-design-vue';
import type { TreeNode, ConditionBranch, ApproveNode, CopyNode } from '@/types/approval-tree';
import type { ButtonPermissionConfig, FormPermissionConfig, NoticeConfig } from '@/types/approval-definition';

const { t } = useI18n();

const props = defineProps<{
  open: boolean;
  node: TreeNode | ConditionBranch | null;
}>();

const emit = defineEmits<{
  'update:open': [value: boolean];
  'update': [node: TreeNode | ConditionBranch];
}>();

const formData = ref<TreeNode | ConditionBranch | null>(null);
const approverTargets = ref<string[]>([]);
const buttonPermissionText = ref('');
const formPermissionText = ref('');
const noticeConfigText = ref('');
const approveNode = ref<ApproveNode | null>(null);
const copyNode = ref<CopyNode | null>(null);
const branchNode = ref<ConditionBranch | null>(null);
const approverConfig = computed(() => approveNode.value?.approverConfig ?? null);

watch(() => props.node, (newNode) => {
  if (newNode) {
    formData.value = structuredClone(newNode);
    syncNodeRefs();
    if (approveNode.value) {
      ensureApproverConfig();
      approverTargets.value = (approveNode.value.approverConfig?.nodeApproveList ?? []).map((item) => item.targetId);
      buttonPermissionText.value = approveNode.value.buttonPermissionConfig
        ? JSON.stringify(approveNode.value.buttonPermissionConfig, null, 2)
        : '';
      formPermissionText.value = approveNode.value.formPermissionConfig
        ? JSON.stringify(approveNode.value.formPermissionConfig, null, 2)
        : '';
      noticeConfigText.value = approveNode.value.noticeConfig
        ? JSON.stringify(approveNode.value.noticeConfig, null, 2)
        : '';
    }
  } else {
    formData.value = null;
    syncNodeRefs();
  }
}, { immediate: true });

const handleClose = () => {
  emit('update:open', false);
};

const handleSave = () => {
  if (formData.value) {
    if (isApproveNode(formData.value)) {
      syncApproverTargets();
      if (!applyExtraConfigs()) {
        return;
      }
    }
    emit('update', formData.value);
    handleClose();
  }
};

const ensureApproverConfig = () => {
  const current = formData.value;
  if (!current || !isApproveNode(current)) return;
  if (!current.approverConfig) {
    current.approverConfig = {
      setType: current.assigneeType ?? 0,
      signType: current.approvalMode === 'sequential' ? 3 : current.approvalMode === 'any' ? 2 : 1,
      noHeaderAction: 0,
      nodeApproveList: current.assigneeValue
        ? [{ targetId: current.assigneeValue, name: current.assigneeValue }]
        : []
    };
  }
};

const syncApproverTargets = () => {
  const current = formData.value;
  if (!current || !isApproveNode(current)) return;
  if (!current.approverConfig) return;
  current.approverConfig.nodeApproveList = approverTargets.value.map((targetId) => ({
    targetId,
    name: targetId
  }));
};

const applyExtraConfigs = () => {
  const current = formData.value;
  if (!current || !isApproveNode(current)) return false;

  const parseJson = <T>(text: string, label: string): T | null => {
    if (!text.trim()) return null;
    try {
      return JSON.parse(text) as T;
    } catch {
      message.error(t('approvalDesigner.drawerJsonInvalid', { label }));
      return null;
    }
  };

  const buttonConfig = parseJson<ButtonPermissionConfig>(buttonPermissionText.value, t('approvalDesigner.drawerLabelButtonPerm'));
  if (buttonConfig === null) return false;
  const formPermConfig = parseJson<FormPermissionConfig>(formPermissionText.value, t('approvalDesigner.drawerLabelFormPerm'));
  if (formPermConfig === null) return false;
  const noticeConfig = parseJson<NoticeConfig>(noticeConfigText.value, t('approvalDesigner.drawerLabelNoticeShort'));
  if (noticeConfig === null) return false;

  current.buttonPermissionConfig = buttonConfig;
  current.formPermissionConfig = formPermConfig;
  current.noticeConfig = noticeConfig;
  return true;
};

const isApproveNode = (node: TreeNode | ConditionBranch): node is ApproveNode => {
  return 'nodeType' in node && node.nodeType === 'approve';
};

const isCopyNode = (node: TreeNode | ConditionBranch): node is CopyNode => {
  return 'nodeType' in node && node.nodeType === 'copy';
};

const isConditionBranch = (node: TreeNode | ConditionBranch): node is ConditionBranch => {
  return 'branchName' in node;
};

const syncNodeRefs = () => {
  const current = formData.value;
  approveNode.value = current && isApproveNode(current) ? current : null;
  copyNode.value = current && isCopyNode(current) ? current : null;
  branchNode.value = current && isConditionBranch(current) ? current : null;
};

const initConditionRule = () => {
  const current = formData.value;
  if (!current || !isConditionBranch(current)) return;
  current.conditionRule = {
    field: '',
    operator: 'equals',
    value: ''
  };
};

const removeConditionRule = () => {
  const current = formData.value;
  if (!current || !isConditionBranch(current)) return;
  current.conditionRule = undefined;
};
</script>
