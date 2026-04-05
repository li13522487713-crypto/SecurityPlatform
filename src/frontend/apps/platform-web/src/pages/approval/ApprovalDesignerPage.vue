<template>
  <div class="dd-page">
    <DesignerToolbar
      v-model:flow-name="flowName"
      v-model:active-menu="activeMenu"
      v-model:palette-visible="paletteVisible"
      :flow-version="flowVersion"
      :can-undo="canUndo"
      :can-redo="canRedo"
      :validating="validating"
      @back="goBack"
      @zoom-out="zoomOutDesigner"
      @zoom-fit="zoomFitDesigner"
      @zoom-in="zoomInDesigner"
      @undo="undo"
      @redo="redo"
      @validate="handleValidate"
      @preview="handlePreview"
      @save="handleSave"
      @publish="handlePublishClick"
      @history="openFlowVersionHistory"
    />

    <div class="dd-main-container">
      <div class="dd-sidebar">
        <a-menu
          v-model:selected-keys="sidebarKeys"
          mode="vertical"
          style="height: 100%; border-right: 0"
        >
          <a-menu-item key="basic">{{ t('approvalDesigner.menuBasic') }}</a-menu-item>
          <a-menu-item key="form">{{ t('approvalDesigner.menuForm') }}</a-menu-item>
          <a-menu-item key="process">{{ t('approvalDesigner.menuProcess') }}</a-menu-item>
        </a-menu>
      </div>

      <div class="dd-content">
        <div v-show="activeMenu === 'basic'" class="dd-content-panel dd-body--scroll">
          <DesignerBasicInfo
            v-model:flow-name="flowName"
            v-model:definition-meta="definitionMeta"
            v-model:visibility-scope-type="visibilityScopeType"
            v-model:visibility-scope-ids="visibilityScopeIds"
          />
        </div>

        <div v-show="activeMenu === 'form'" class="dd-content-panel">
          <DesignerFormSchema
            v-model:schema-text="amisSchemaText"
            @apply="applyAmisSchema"
          />
        </div>

        <div v-show="activeMenu === 'process'" class="dd-content-panel">
          <DesignerFlowProcess
            ref="processRef"
            v-model:palette-visible="paletteVisible"
            v-model:panel-open="panelOpen"
            :flow-tree="flowTree"
            :selected-node="selectedNode"
            :effective-form-fields="effectiveFormFields"
            @add-palette-node="handlePaletteAddNode"
            @select-node="handleSelectNode"
            @add-node="addNode"
            @delete-node="deleteNode"
            @add-condition-branch="addConditionBranch"
            @delete-condition-branch="deleteConditionBranch"
            @move-branch="moveBranch"
            @update-route-target="handleRouteTargetUpdate"
            @update-node="handleNodeUpdate"
            @undo="undo"
            @redo="redo"
          />
        </div>
      </div>
    </div>

    <!-- ══ 弹窗：校验结果 ══ -->
    <ValidationErrorPanel
      v-model:open="validateModalOpen"
      :result="validateResult"
      :error-issues="errorIssues"
      :warning-issues="warningIssues"
      @locate="handleValidationIssueClick"
    />

    <!-- ══ 弹窗：发布确认 ══ -->
    <a-modal v-model:open="publishModalOpen" :title="t('approvalDesigner.publishConfirmTitle')" :ok-text="t('approvalDesigner.publishOk')" :cancel-text="t('common.cancel')" :confirm-loading="publishing" @ok="handlePublishConfirm">
      <a-descriptions :column="1" bordered size="small">
        <a-descriptions-item :label="t('approvalDesigner.labelFlowName')">{{ flowName }}</a-descriptions-item>
        <a-descriptions-item :label="t('approvalDesigner.labelCurrentVersion')">
          <a-tag color="blue">v{{ flowVersion || 0 }}</a-tag>
          <span style="margin-left:4px;color:#8c8c8c">{{ t('approvalDesigner.versionHint') }}</span>
        </a-descriptions-item>
        <a-descriptions-item :label="t('approvalDesigner.labelCategory')">{{ definitionMeta.category || t('approvalDesigner.categoryUnset') }}</a-descriptions-item>
      </a-descriptions>
      <a-alert style="margin-top:12px" type="info" :message="t('approvalDesigner.publishAlert')" show-icon />
    </a-modal>

    <!-- ══ 弹窗：仿真预览 ══ -->
    <a-modal v-model:open="previewModalOpen" :title="t('approvalDesigner.previewTitle')" :footer="null" width="85vw" :body-style="{ height: '80vh', padding: 0 }" destroy-on-close>
      <X6PreviewCanvas v-if="previewModalOpen" :flow-tree="flowTree" />
    </a-modal>

    <!-- ══ 版本历史抽屉 ══ -->
    <a-drawer
      v-model:open="versionHistoryVisible"
      :title="t('approvalDesigner.versionHistoryTitle')"
      :width="480"
      placement="right"
    >
      <div v-if="loadingVersions" class="version-loading">
        <a-spin :tip="t('approvalDesigner.versionHistoryLoading')" />
      </div>
      <a-empty v-else-if="flowVersionList.length === 0" :description="t('approvalDesigner.versionHistoryEmpty')" />
      <a-list
        v-else
        :data-source="flowVersionList"
        item-layout="horizontal"
      >
        <template #renderItem="{ item }">
          <a-list-item>
            <a-list-item-meta>
              <template #title>
                <span>v{{ item.snapshotVersion }} — {{ item.name }}</span>
              </template>
              <template #description>
                <a-space direction="vertical" :size="2">
                  <span style="color: #666; font-size: 12px">
                    {{ new Date(item.createdAt).toLocaleString() }}
                  </span>
                  <span v-if="item.category" style="color: #999; font-size: 12px">
                    {{ t('approvalDesigner.categoryLabel') }}{{ item.category }}
                  </span>
                </a-space>
              </template>
            </a-list-item-meta>
            <template #actions>
              <a-popconfirm
                :title="t('approvalDesigner.rollbackConfirm', { version: item.snapshotVersion })"
                :ok-text="t('approvalDesigner.rollbackOk')"
                :cancel-text="t('common.cancel')"
                @confirm="handleFlowRollback(item.id)"
              >
                <a-button type="link" size="small" :loading="rollingBackFlow === item.id">{{ t('approvalDesigner.rollbackBtn') }}</a-button>
              </a-popconfirm>
            </template>
          </a-list-item>
        </template>
      </a-list>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch, onMounted, onBeforeUnmount, onUnmounted } from 'vue';
import { useI18n } from 'vue-i18n';

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { useRoute, useRouter } from 'vue-router';
import { message } from 'ant-design-vue';
import X6PreviewCanvas from '@/components/approval/X6PreviewCanvas.vue';
import DesignerToolbar from '@/components/approval/designer/DesignerToolbar.vue';
import DesignerBasicInfo from '@/components/approval/designer/DesignerBasicInfo.vue';
import DesignerFormSchema from '@/components/approval/designer/DesignerFormSchema.vue';
import DesignerFlowProcess from '@/components/approval/designer/DesignerFlowProcess.vue';
import ValidationErrorPanel from '@/components/approval/designer/ValidationErrorPanel.vue';
import { useApprovalTree } from '@/composables/useApprovalTree';
import { ApprovalTreeConverter } from '@/utils/approval-tree-converter';
import { extractAmisFields } from '@/utils/amis-field-extractor';
import type { ApprovalDefinitionMeta, LfFormField, LfFormPayload, FormJson, VisibilityScope } from '@/types/approval-definition';
import type { TreeNode, ConditionBranch } from '@/types/approval-tree';
import type { ApprovalFlowValidationIssue, ApprovalFlowValidationResult, ApprovalFlowVersionListItem } from '@atlas/shared-core';
import {
  getApprovalFlowById,
  createApprovalFlow,
  updateApprovalFlow,
  publishApprovalFlow,
  validateApprovalFlow,
  getApprovalFlowVersions,
  getApprovalFlowVersionDetail,
  rollbackApprovalFlowVersion,
} from '@/services/api-approval';

const { t } = useI18n();
const route = useRoute();
const router = useRouter();

const {
  flowTree, selectedNode, addNode, deleteNode, updateNode,
  addConditionBranch, deleteConditionBranch, moveBranch, selectNode,
  validateFlow, undo, redo, canUndo, canRedo, pushState
} = useApprovalTree();

// ── 状态 ──
const flowName = ref('');
const flowId = ref<string | null>(null);
const flowVersion = ref<number>(0);
const panelOpen = ref(false);
const paletteVisible = ref(false);

const sidebarKeys = ref<string[]>(['basic']);
const activeMenu = computed({
  get: () => sidebarKeys.value[0] || 'basic',
  set: (val) => { sidebarKeys.value = [val]; }
});

const definitionMeta = ref<ApprovalDefinitionMeta>({ flowName: '', isLowCodeFlow: true });
const amisSchemaText = ref('');
const amisSchemaModel = ref<unknown | undefined>(undefined);
const amisFormFields = ref<LfFormField[]>([]);
const effectiveFormFields = computed(() => amisFormFields.value);

const visibilityScopeType = ref<'All' | 'Department' | 'Role' | 'User'>('All');
const visibilityScopeIds = ref<string[]>([]);

const validating = ref(false);
const validateModalOpen = ref(false);
const validateResult = ref<ApprovalFlowValidationResult | null>(null);
const publishModalOpen = ref(false);
const publishing = ref(false);
const previewModalOpen = ref(false);
const versionHistoryVisible = ref(false);
const loadingVersions = ref(false);
const flowVersionList = ref<ApprovalFlowVersionListItem[]>([]);
const rollingBackFlow = ref<string | null>(null);
const processRef = ref<InstanceType<typeof DesignerFlowProcess> | null>(null);
type ValidationIssueView = ApprovalFlowValidationIssue & { severity: 'error' | 'warning' };
const normalizeValidationIssues = (result: ApprovalFlowValidationResult | null): ValidationIssueView[] => {
  if (!result) {
    return [];
  }
  if (result.details && result.details.length > 0) {
    return result.details.map((detail) => ({
      ...detail,
      severity: detail.severity === 'warning' ? 'warning' : 'error',
    }));
  }
  const errorIssues = result.errors.map((message) => ({
    code: 'LOCAL_ERROR',
    message,
    severity: 'error' as const,
  }));
  const warningIssues = result.warnings.map((message) => ({
    code: 'LOCAL_WARNING',
    message,
    severity: 'warning' as const,
  }));
  return [...errorIssues, ...warningIssues];
};
const allValidationIssues = computed(() => normalizeValidationIssues(validateResult.value));
const errorIssues = computed(() => allValidationIssues.value.filter((issue) => issue.severity === 'error'));
const warningIssues = computed(() => allValidationIssues.value.filter((issue) => issue.severity === 'warning'));

// ── 导航 ──
const goBack = () => {
  if (window.history.length > 1) router.back();
  else router.push('/console');
};

// ── 节点选中 ──
watch(selectedNode, (node) => { panelOpen.value = !!node; });
const handleSelectNode = (node: TreeNode | ConditionBranch | null) => { selectNode(node); };
const handleNodeUpdate = (updatedNode: TreeNode | ConditionBranch) => { updateNode(updatedNode); };
const handleRouteTargetUpdate = (routeNodeId: string, targetNodeId: string) => {
  const target = findNodeById(flowTree.value.rootNode, routeNodeId);
  if (!target || target.nodeType !== 'route') {
    return;
  }

  updateNode({
    ...target,
    routeTargetNodeId: targetNodeId,
  });
};

const handleValidationIssueClick = (issue: ValidationIssueView) => {
  if (!issue.nodeId) {
    return;
  }
  const target = findNodeById(flowTree.value.rootNode, issue.nodeId);
  if (!target) {
    return;
  }
  selectNode(target);
  activeMenu.value = 'process';
  message.info(t('approvalDesigner.locateNodeOk', { name: target.nodeName }));
};
// ── 子组件代理/处理 ──
const zoomInDesigner = () => processRef.value?.zoomIn();
const zoomOutDesigner = () => processRef.value?.zoomOut();
const zoomFitDesigner = () => processRef.value?.zoomFit();

const handlePaletteAddNode = (nodeType: string) => {
  const parentId = selectedNode.value?.id ?? flowTree.value.rootNode.id;
  addNode(parentId, nodeType);
};

const applyAmisSchema = () => {
  if (!amisSchemaText.value.trim()) {
    amisSchemaModel.value = undefined;
    amisFormFields.value = [];
    return;
  }
  try {
    const parsed = JSON.parse(amisSchemaText.value);
    amisSchemaModel.value = parsed;
    amisFormFields.value = extractAmisFields(parsed);
    message.success(t('approvalDesigner.amisExtractOk', { count: amisFormFields.value.length }));
  } catch {
    message.error(t('approvalDesigner.amisExtractFail'));
  }
};

// ── 构建后端请求 ──
const buildRequest = () => {
  definitionMeta.value.flowName = flowName.value;
  
  // 构建 VisibilityScope（ID 保持字符串形式，避免 Snowflake ID 超出 JS Number.MAX_SAFE_INTEGER 导致精度丢失）
  const scope: VisibilityScope = {
    scopeType: visibilityScopeType.value,
    departmentIds: visibilityScopeType.value === 'Department' ? visibilityScopeIds.value.filter(Boolean) : undefined,
    roleCodes: visibilityScopeType.value === 'Role' ? visibilityScopeIds.value : undefined,
    userIds: visibilityScopeType.value === 'User' ? visibilityScopeIds.value.filter(Boolean) : undefined
  };
  
  definitionMeta.value.visibilityScope = scope;

  const amisFormPayload = amisSchemaModel.value
    ? { schema: amisSchemaModel.value as Record<string, unknown>, schemaVersion: '1.0.0', formFields: amisFormFields.value }
    : undefined;
  const definitionJson = ApprovalTreeConverter.treeToDefinitionJson(flowTree.value, definitionMeta.value, undefined, amisFormPayload);
  const visibilityScopeJson = definitionMeta.value.visibilityScope ? JSON.stringify(definitionMeta.value.visibilityScope) : undefined;
  return { name: flowName.value, definitionJson, description: definitionMeta.value.description, category: definitionMeta.value.category, visibilityScopeJson, isQuickEntry: !!definitionMeta.value.isQuickEntry };
};

// ── 加载 ──
const loadFlow = async () => {
  const id = route.params.id as string;
  // 如果没有 ID，说明是新建流程
  if (!id || id === 'undefined') {
    // 设置默认名称
    if (!flowName.value) {
      flowName.value = t('approvalDesigner.defaultFlowName');
    }
    pushState(flowTree.value);
    return;
  }
  try {
    const flow  = await getApprovalFlowById(id);

    if (!isMounted.value) return;
    flowName.value = flow.name;
    flowId.value = flow.id;
    flowVersion.value = flow.version;
    definitionMeta.value.description = flow.description;
    definitionMeta.value.category = flow.category;
    definitionMeta.value.isQuickEntry = flow.isQuickEntry;
    if (flow.definitionJson) {
      const state = ApprovalTreeConverter.definitionJsonToState(flow.definitionJson);
      flowTree.value = state.tree;
      if (state.meta) {
        // 合并 meta，但不覆盖 visibilityScope（由顶层 visibilityScopeJson 权威管理）
        definitionMeta.value = { ...state.meta, visibilityScope: undefined };
      }
      if (state.amisForm?.schema) {
        amisSchemaModel.value = state.amisForm.schema;
        amisSchemaText.value = JSON.stringify(state.amisForm.schema, null, 2);
        amisFormFields.value = state.amisForm.formFields ?? extractAmisFields(state.amisForm.schema);
      }
    }
    // 顶层 visibilityScopeJson 为权威来源，最后加载确保不被 definitionJson.meta 覆盖
    if (flow.visibilityScopeJson) {
      try {
        const scope = JSON.parse(flow.visibilityScopeJson) as VisibilityScope;
        visibilityScopeType.value = scope.scopeType;
        if (scope.scopeType === 'Department') visibilityScopeIds.value = scope.departmentIds ?? [];
        else if (scope.scopeType === 'Role') visibilityScopeIds.value = scope.roleCodes ?? [];
        else if (scope.scopeType === 'User') visibilityScopeIds.value = scope.userIds ?? [];
        definitionMeta.value.visibilityScope = scope;
      } catch {
        visibilityScopeType.value = 'All';
      }
    } else if (definitionMeta.value.visibilityScope) {
      // 兼容旧数据：仅 definitionJson.meta 中有 visibilityScope 时回退读取
      const scope = definitionMeta.value.visibilityScope;
      visibilityScopeType.value = scope.scopeType;
      if (scope.scopeType === 'Department') visibilityScopeIds.value = scope.departmentIds ?? [];
      else if (scope.scopeType === 'Role') visibilityScopeIds.value = scope.roleCodes ?? [];
      else if (scope.scopeType === 'User') visibilityScopeIds.value = scope.userIds ?? [];
    }
    pushState(flowTree.value);
  } catch (err) { message.error(err instanceof Error ? err.message : t('approvalDesigner.loadFailed')); }
};

// ── 校验 ──
const handleValidate = async () => {
  const localResult = validateFlow();
  if (!localResult.valid) {
    focusNodeByErrors(localResult.errors);
    validateResult.value = {
      isValid: false,
      errors: localResult.errors,
      warnings: [],
      details: localResult.issues.map((issue) => ({
        code: issue.code,
        message: issue.message,
        severity: issue.severity,
        nodeId: issue.nodeId,
      })),
    };
    validateModalOpen.value = true;
    return;
  }
  if (!flowName.value.trim()) { message.warning(t('approvalDesigner.warnFlowName')); return; }
  const request = buildRequest();
  if (!request) return;
  validating.value = true;
  try {
    const result  = await validateApprovalFlow(request);

    if (!isMounted.value) return;
    validateResult.value = result;
    validateModalOpen.value = true;
    if (result.isValid) message.success(t('approvalDesigner.validateOk'));
  } catch (err) { message.error(err instanceof Error ? err.message : t('approvalDesigner.validateFailed')); }
  finally { validating.value = false; }
};

// ── 保存 ──
const handleSave = async () => {
  if (!flowName.value.trim()) { message.warning(t('approvalDesigner.warnFlowName')); return; }
  const localResult = validateFlow();
  if (!localResult.valid) {
    focusNodeByErrors(localResult.errors);
    validateResult.value = {
      isValid: false,
      errors: localResult.errors,
      warnings: [],
      details: localResult.issues.map((issue) => ({
        code: issue.code,
        message: issue.message,
        severity: issue.severity,
        nodeId: issue.nodeId,
      })),
    };
    validateModalOpen.value = true;
    return;
  }
  const payload = buildRequest();
  if (!payload) return;
  try {
    if (flowId.value) {
      const result  = await updateApprovalFlow(flowId.value, payload);

      if (!isMounted.value) return;
      flowVersion.value = result.version;
      message.success(t('approvalDesigner.saveOk'));
    } else {
      const result  = await createApprovalFlow(payload);

      if (!isMounted.value) return;
      flowId.value = result.id;
      flowVersion.value = result.version;
      router.replace(`/approval/designer/${result.id}`);
      message.success(t('approvalDesigner.createOk'));
    }
  } catch (err) { message.error(err instanceof Error ? err.message : t('approvalDesigner.saveFailed')); }
};

// ── 发布 ──
const handlePublishClick = async () => {
  if (!flowId.value) { message.warning(t('approvalDesigner.warnSaveFirst')); return; }

  // 1. 本地校验
  const localResult = validateFlow();
  if (!localResult.valid) {
    focusNodeByErrors(localResult.errors);
    validateResult.value = {
      isValid: false,
      errors: localResult.errors,
      warnings: [],
      details: localResult.issues.map((issue) => ({
        code: issue.code,
        message: issue.message,
        severity: issue.severity,
        nodeId: issue.nodeId,
      })),
    };
    validateModalOpen.value = true;
    return;
  }
  if (!flowName.value.trim()) { message.warning(t('approvalDesigner.warnFlowName')); return; }

  // 2. 服务端校验（防止绕过前端直接发布）
  const payload = buildRequest();
  if (!payload) return;
  validating.value = true;
  try {
    const result  = await validateApprovalFlow(payload);

    if (!isMounted.value) return;
    if (!result.isValid) {
      validateResult.value = result;
      validateModalOpen.value = true;
      return;
    }
  } catch (err) {
    message.error(err instanceof Error ? err.message : t('approvalDesigner.validateFailed'));
    return;
  } finally {
    validating.value = false;
  }

  // 3. 两级校验均通过，弹出发布确认框
  publishModalOpen.value = true;
};
const handlePublishConfirm = async () => {
  if (!flowId.value) return;
  publishing.value = true;
  try { await publishApprovalFlow(flowId.value); message.success(t('approvalDesigner.publishOkMsg')); publishModalOpen.value = false; router.push('/console'); }
  catch (err) { message.error(err instanceof Error ? err.message : t('approvalDesigner.publishFailed')); }
  finally { publishing.value = false; }
};

// ── 预览 ──
const handlePreview = () => { previewModalOpen.value = true; };

// ── 版本历史 ──
const openFlowVersionHistory = async () => {
  versionHistoryVisible.value = true;
  if (!flowId.value) return;
  loadingVersions.value = true;
  try {
    flowVersionList.value = await getApprovalFlowVersions(flowId.value);

    if (!isMounted.value) return;
  } catch (error) {
    message.error((error as Error).message || t('approvalDesigner.versionHistoryLoadFailed'));
  } finally {
    loadingVersions.value = false;
  }
};

const handleFlowRollback = async (versionId: string) => {
  if (!flowId.value) return;
  rollingBackFlow.value = versionId;
  try {
    const versionDetail  = await getApprovalFlowVersionDetail(flowId.value, versionId);

    if (!isMounted.value) return;
    await rollbackApprovalFlowVersion(flowId.value, versionId);

    if (!isMounted.value) return;

    // 重新加载流程定义
    try {
      const loadedTree = ApprovalTreeConverter.definitionJsonToTree(versionDetail.definitionJson);
      if (loadedTree) {
        flowTree.value = loadedTree;
        pushState(flowTree.value);
      }
    } catch {
      // 保持现有状态
    }
    versionHistoryVisible.value = false;
    flowVersionList.value = await getApprovalFlowVersions(flowId.value);

    if (!isMounted.value) return;
    message.success(t('approvalDesigner.rollbackOkMsg', { version: versionDetail.snapshotVersion }));
  } catch (error) {
    message.error((error as Error).message || t('approvalDesigner.rollbackFailed'));
  } finally {
    rollingBackFlow.value = null;
  }
};

const focusNodeByErrors = (errors: string[]) => {
  if (errors.length === 0) return;
  const firstError = errors[0];
  const idMatch = firstError.match(/node[_-]?[a-z0-9-]+/i);
  if (!idMatch) return;
  const target = findNodeById(flowTree.value.rootNode, idMatch[0]);
  if (target) {
    selectNode(target);
    activeMenu.value = 'process';
    message.warning(t('approvalDesigner.locateNodeWarn', { name: target.nodeName }));
  }
};

const findNodeById = (node: TreeNode | undefined, nodeId: string): TreeNode | null => {
  if (!node) return null;
  if (node.id === nodeId) return node;
  if ('childNode' in node && node.childNode) {
    const found = findNodeById(node.childNode, nodeId);
    if (found) return found;
  }
  if ('conditionNodes' in node && Array.isArray(node.conditionNodes)) {
    for (const branch of node.conditionNodes) {
      if (branch.childNode) {
        const found = findNodeById(branch.childNode, nodeId);
        if (found) return found;
      }
    }
  }
  if ('parallelNodes' in node && Array.isArray(node.parallelNodes)) {
    for (const child of node.parallelNodes) {
      const found = findNodeById(child, nodeId);
      if (found) return found;
    }
  }
  return null;
};

const handleGlobalShortcut = (event: KeyboardEvent) => {
  const target = event.target as HTMLElement | null;
  if (!target) {
    return;
  }

  const tagName = target.tagName.toUpperCase();
  if (tagName === 'INPUT' || tagName === 'TEXTAREA' || target.isContentEditable) {
    return;
  }

  const isCtrlOrCmd = event.ctrlKey || event.metaKey;
  if (!isCtrlOrCmd) {
    return;
  }

  const key = event.key.toLowerCase();
  if (key === 's') {
    event.preventDefault();
    void handleSave();
    return;
  }

  if (activeMenu.value !== 'process') {
    return;
  }

  if (key === 'z' && !event.shiftKey) {
    event.preventDefault();
    undo();
    return;
  }

  if (key === 'y' || (key === 'z' && event.shiftKey)) {
    event.preventDefault();
    redo();
    return;
  }

  if (key === '=' || key === '+') {
    event.preventDefault();
    zoomInDesigner();
    return;
  }

  if (key === '-' || key === '_') {
    event.preventDefault();
    zoomOutDesigner();
    return;
  }

  if (key === '0') {
    event.preventDefault();
    zoomFitDesigner();
  }
};

onMounted(() => {
  loadFlow();
  window.addEventListener('keydown', handleGlobalShortcut);
});

onBeforeUnmount(() => {
  window.removeEventListener('keydown', handleGlobalShortcut);
});
</script>

<style scoped>
/* ═══════════════════════════
   整体：撑满视口，零外边距
   ═══════════════════════════ */
.dd-page {
  display: flex;
  flex-direction: column;
  width: 100%;
  height: 100vh;
  overflow: hidden;
  background: #f5f5f7;
}

/* ═══ 工具栏 44px ═══ */
.dd-toolbar {
  display: flex;
  align-items: center;
  height: 44px;
  flex-shrink: 0;
  padding: 0 10px;
  background: #fff;
  border-bottom: 1px solid #e8e8e8;
  gap: 4px;
}
.dd-toolbar__back {
  font-size: 13px;
  color: #595959;
  padding: 0 6px;
}
.dd-toolbar__name {
  width: 180px;
  font-weight: 600;
  font-size: 14px;
}
.dd-toolbar__version {
  margin-right: 4px;
}

/* 步骤指示：紧凑文字标签 */
.dd-toolbar__steps {
  display: flex;
  align-items: center;
  gap: 2px;
  margin: 0 auto;
}
.dd-step-dot {
  padding: 2px 10px;
  font-size: 12px;
  color: #8c8c8c;
  cursor: pointer;
  border-radius: 10px;
  transition: all 0.2s;
  white-space: nowrap;
}
.dd-step-dot--active {
  background: #1677ff;
  color: #fff;
  font-weight: 500;
}
.dd-step-dot--done {
  color: #1677ff;
}
.dd-step-dot:hover:not(.dd-step-dot--active) {
  background: #f0f0f0;
}

.dd-toolbar__actions {
  display: flex;
  align-items: center;
  gap: 4px;
  flex-shrink: 0;
}

.dd-body {
  flex: 1;
  min-height: 0;
}
.dd-body--scroll {
  overflow-y: auto;
  padding: 16px 24px;
}
.dd-basic-form {
  background: #fff;
  padding: 16px;
  border: 1px solid #f0f0f0;
  border-radius: 6px;
  max-width: 640px;
}

/* ═══ 主内容结构（Sidebar 布局） ═══ */
.dd-main-container {
  display: flex;
  flex: 1;
  overflow: hidden;
}
.dd-sidebar {
  width: 160px;
  background: #fff;
  border-right: 1px solid #e8e8e8;
  flex-shrink: 0;
}
.dd-content {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
  position: relative;
}
.dd-content-panel {
  flex: 1;
  display: flex;
  flex-direction: column;
  overflow: hidden;
}

/* ═══ 流程设计三栏（撑满剩余高度） ═══ */
.dd-body--designer {
  flex: 1;
  display: flex;
  overflow: hidden;
}
.dd-canvas {
  flex: 1;
  min-width: 0;
  position: relative;
  overflow: hidden;
}

/* ═══ 弹窗辅助 ═══ */
.dd-validate-list {
  max-height: 280px;
  overflow-y: auto;
}
.dd-validate-item {
  padding: 4px 0;
  font-size: 13px;
  line-height: 1.5;
  border-bottom: 1px solid #fafafa;
  display: flex;
  align-items: center;
}
.dd-validate-item:last-child {
  border-bottom: none;
}
.dd-validate-item--locatable {
  cursor: pointer;
  transition: background 0.2s;
}
.dd-validate-item--locatable:hover {
  background: #f5f5f5;
}
.version-loading {
  display: flex;
  justify-content: center;
  padding: 40px 0;
}
</style>
