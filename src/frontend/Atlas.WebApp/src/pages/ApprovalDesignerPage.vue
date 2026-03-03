<template>
  <div class="dd-page">
    <DesignerToolbar
      v-model:flowName="flowName"
      v-model:activeStep="activeStep"
      v-model:paletteVisible="paletteVisible"
      :flow-version="flowVersion"
      :can-undo="canUndo"
      :can-redo="canRedo"
      :validating="validating"
      @back="goBack"
      @prev-step="prevStep"
      @next-step="nextStep"
      @zoom-out="zoomOutDesigner"
      @zoom-fit="zoomFitDesigner"
      @zoom-in="zoomInDesigner"
      @undo="undo"
      @redo="redo"
      @validate="handleValidate"
      @preview="handlePreview"
      @save="handleSave"
      @publish="handlePublishClick"
    />

    <!-- ══ 步骤 0: 基础设置 ══ -->
    <div class="dd-body dd-body--scroll" v-show="activeStep === 0">
      <a-form layout="vertical" class="dd-basic-form">
        <a-form-item label="流程名称">
          <a-input v-model:value="flowName" :maxlength="100" placeholder="请输入流程名称" />
        </a-form-item>
        <a-form-item label="流程分类">
          <a-input v-model:value="definitionMeta.category" placeholder="如：采购/人事/财务" />
        </a-form-item>
        <a-form-item label="流程说明">
          <a-textarea v-model:value="definitionMeta.description" :rows="3" />
        </a-form-item>
        
        <a-form-item label="可见范围">
          <a-radio-group v-model:value="visibilityScopeType" style="margin-bottom: 12px">
            <a-radio value="All">全部可见</a-radio>
            <a-radio value="Department">指定部门</a-radio>
            <a-radio value="Role">指定角色</a-radio>
            <a-radio value="User">指定人员</a-radio>
          </a-radio-group>
          
          <div v-if="visibilityScopeType !== 'All'">
            <UserRolePicker
              v-if="visibilityScopeType === 'Department'"
              mode="department"
              v-model:value="visibilityScopeIds"
              placeholder="请选择部门"
            />
            <UserRolePicker
              v-else-if="visibilityScopeType === 'Role'"
              mode="role"
              v-model:value="visibilityScopeIds"
              placeholder="请选择角色"
            />
            <UserRolePicker
              v-else-if="visibilityScopeType === 'User'"
              mode="user"
              v-model:value="visibilityScopeIds"
              placeholder="请选择人员"
            />
          </div>
        </a-form-item>

        <a-space>
          <a-switch v-model:checked="definitionMeta.isQuickEntry" /> <span>快捷入口</span>
          <a-switch v-model:checked="definitionMeta.isLowCodeFlow" /> <span>启用低代码表单</span>
        </a-space>
      </a-form>
    </div>

    <!-- ══ 步骤 1: 表单设计 ══ -->
    <div class="dd-body dd-body--scroll" v-show="activeStep === 1">
      <a-tabs v-model:activeKey="formEngine">
        <a-tab-pane key="lf" tab="LF(vform3) 表单">
          <LfFormDesigner v-model="lfFormModel" @update:formFields="handleLfFormFields" />
        </a-tab-pane>
        <a-tab-pane key="amis" tab="AMIS 表单 Schema">
          <a-alert
            type="info"
            show-icon
            style="margin-bottom: 12px"
            message="请输入 AMIS Schema（JSON），系统会自动提取字段供条件与权限配置使用"
          />
          <a-textarea
            v-model:value="amisSchemaText"
            :rows="20"
            placeholder='{"type":"form","body":[{"type":"input-text","name":"title","label":"标题"}]}'
          />
          <div style="margin-top: 8px; display: flex; gap: 8px">
            <a-button size="small" @click="formatAmisSchema">格式化 JSON</a-button>
            <a-button size="small" @click="applyAmisSchema">应用并提取字段</a-button>
          </div>
        </a-tab-pane>
      </a-tabs>
    </div>

    <!-- ══ 步骤 2: 流程设计（三栏，撑满剩余） ══ -->
    <div class="dd-body dd-body--designer" v-show="activeStep === 2">
      <ApprovalNodePalette :visible="paletteVisible" @update:visible="paletteVisible = $event" @addNode="handlePaletteAddNode" />
      <div class="dd-canvas">
        <X6ApprovalDesigner
          ref="designerRef"
          :flow-tree="flowTree"
          :selected-node-id="selectedNode?.id ?? null"
          @selectNode="handleSelectNode"
          @addNode="addNode"
          @deleteNode="deleteNode"
          @addConditionBranch="addConditionBranch"
          @deleteConditionBranch="deleteConditionBranch"
          @moveBranch="moveBranch"
          @updateRouteTarget="handleRouteTargetUpdate"
        />
      </div>
      <ApprovalPropertiesPanel
        :open="panelOpen"
        :node="selectedNode"
        :form-fields="effectiveFormFields"
        @update:open="panelOpen = $event"
        @update="handleNodeUpdate"
      />
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
    <a-modal v-model:open="publishModalOpen" title="确认发布" ok-text="确认发布" cancel-text="取消" @ok="handlePublishConfirm" :confirm-loading="publishing">
      <a-descriptions :column="1" bordered size="small">
        <a-descriptions-item label="流程名称">{{ flowName }}</a-descriptions-item>
        <a-descriptions-item label="当前版本">
          <a-tag color="blue">v{{ flowVersion || 0 }}</a-tag>
          <span style="margin-left:4px;color:#8c8c8c">发布后版本号将自动递增</span>
        </a-descriptions-item>
        <a-descriptions-item label="分类">{{ definitionMeta.category || '未设置' }}</a-descriptions-item>
      </a-descriptions>
      <a-alert style="margin-top:12px" type="info" message="发布后新版本将立即生效，运行中的流程实例不受影响。" show-icon />
    </a-modal>

    <!-- ══ 弹窗：仿真预览 ══ -->
    <a-modal v-model:open="previewModalOpen" title="流程预览" :footer="null" width="85vw" :body-style="{ height: '80vh', padding: 0 }" destroy-on-close>
      <X6PreviewCanvas v-if="previewModalOpen" :flow-tree="flowTree" />
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch, onMounted, onBeforeUnmount } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { message } from 'ant-design-vue';
import X6ApprovalDesigner from '@/components/approval/x6/X6ApprovalDesigner.vue';
import ApprovalPropertiesPanel from '@/components/approval/ApprovalPropertiesPanel.vue';
import ApprovalNodePalette from '@/components/approval/ApprovalNodePalette.vue';
import LfFormDesigner from '@/components/approval/LfFormDesigner.vue';
import X6PreviewCanvas from '@/components/approval/X6PreviewCanvas.vue';
import UserRolePicker from '@/components/common/UserRolePicker.vue';
import DesignerToolbar from '@/components/approval/designer/DesignerToolbar.vue';
import ValidationErrorPanel from '@/components/approval/designer/ValidationErrorPanel.vue';
import { useApprovalTree } from '@/composables/useApprovalTree';
import { ApprovalTreeConverter } from '@/utils/approval-tree-converter';
import { extractAmisFields } from '@/utils/amis-field-extractor';
import type { ApprovalDefinitionMeta, LfFormPayload, FormJson, VisibilityScope } from '@/types/approval-definition';
import type { TreeNode, ConditionBranch } from '@/types/approval-tree';
import type { ApprovalFlowValidationIssue, ApprovalFlowValidationResult } from '@/types/api';
import {
  getApprovalFlowById,
  createApprovalFlow,
  updateApprovalFlow,
  publishApprovalFlow,
  validateApprovalFlow,
} from '@/services/api';

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
const activeStep = ref(0);
const definitionMeta = ref<ApprovalDefinitionMeta>({ flowName: '', isLowCodeFlow: true });
const lfFormPayload = ref<LfFormPayload | undefined>(undefined);
const lfFormModel = ref<FormJson | undefined>(undefined);
const formEngine = ref<'lf' | 'amis'>('lf');
const amisSchemaText = ref('');
const amisSchemaModel = ref<unknown | undefined>(undefined);
const amisFormFields = ref<LfFormPayload['formFields']>([]);
const effectiveFormFields = computed(() => (formEngine.value === 'amis' ? amisFormFields.value : (lfFormPayload.value?.formFields ?? [])));
// const visibilityScopeText = ref(''); // Removed
const visibilityScopeType = ref<'All' | 'Department' | 'Role' | 'User'>('All');
const visibilityScopeIds = ref<string[]>([]);

const validating = ref(false);
const validateModalOpen = ref(false);
const validateResult = ref<ApprovalFlowValidationResult | null>(null);
const publishModalOpen = ref(false);
const publishing = ref(false);
const previewModalOpen = ref(false);
const designerRef = ref<InstanceType<typeof X6ApprovalDesigner> | null>(null);
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
  else router.push('/approval/flows');
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
  activeStep.value = 2;
  message.info(`已定位到节点：${target.nodeName}`);
};

// ── 步骤 ──
const nextStep = () => { if (activeStep.value < 2) activeStep.value += 1; };
const prevStep = () => { if (activeStep.value > 0) activeStep.value -= 1; };
const zoomInDesigner = () => designerRef.value?.zoomIn();
const zoomOutDesigner = () => designerRef.value?.zoomOut();
const zoomFitDesigner = () => designerRef.value?.zoomFit();

// ── 节点库添加 ──
const handlePaletteAddNode = (nodeType: string) => {
  const parentId = selectedNode.value?.id ?? flowTree.value.rootNode.id;
  addNode(parentId, nodeType);
};

const handleLfFormFields = (fields: LfFormPayload['formFields']) => {
  lfFormPayload.value = { formJson: lfFormModel.value ?? { widgetList: [] }, formFields: fields };
};

const formatAmisSchema = () => {
  if (!amisSchemaText.value.trim()) {
    return;
  }
  try {
    const parsed = JSON.parse(amisSchemaText.value);
    amisSchemaText.value = JSON.stringify(parsed, null, 2);
  } catch {
    message.error('AMIS Schema JSON 格式不正确');
  }
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
    message.success(`已提取 ${amisFormFields.value.length} 个 AMIS 字段`);
  } catch {
    message.error('AMIS Schema JSON 解析失败');
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

  if (definitionMeta.value.isLowCodeFlow) {
    if (formEngine.value === 'lf') {
      lfFormPayload.value = { formJson: lfFormModel.value ?? { widgetList: [] }, formFields: lfFormPayload.value?.formFields ?? [] };
    }
  } else {
    lfFormPayload.value = undefined;
  }
  const amisFormPayload = formEngine.value === 'amis' && amisSchemaModel.value
    ? { schema: amisSchemaModel.value as Record<string, unknown>, schemaVersion: '1.0.0', formFields: amisFormFields.value }
    : undefined;
  const definitionJson = ApprovalTreeConverter.treeToDefinitionJson(flowTree.value, definitionMeta.value, lfFormPayload.value, amisFormPayload);
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
      flowName.value = '未命名流程';
    }
    pushState(flowTree.value);
    return;
  }
  try {
    const flow = await getApprovalFlowById(id);
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
      if (state.lfForm) {
        formEngine.value = 'lf';
        lfFormPayload.value = state.lfForm;
        lfFormModel.value = state.lfForm.formJson;
      }
      if (state.amisForm?.schema) {
        formEngine.value = 'amis';
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
  } catch (err) { message.error(err instanceof Error ? err.message : '加载失败'); }
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
  if (!flowName.value.trim()) { message.warning('请输入流程名称'); return; }
  const request = buildRequest();
  if (!request) return;
  validating.value = true;
  try {
    const result = await validateApprovalFlow(request);
    validateResult.value = result;
    validateModalOpen.value = true;
    if (result.isValid) message.success('校验通过');
  } catch (err) { message.error(err instanceof Error ? err.message : '校验失败'); }
  finally { validating.value = false; }
};

// ── 保存 ──
const handleSave = async () => {
  if (!flowName.value.trim()) { message.warning('请输入流程名称'); return; }
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
      const result = await updateApprovalFlow(flowId.value, payload);
      flowVersion.value = result.version;
      message.success('保存成功');
    } else {
      const result = await createApprovalFlow(payload);
      flowId.value = result.id;
      flowVersion.value = result.version;
      router.replace(`/process/designer/${result.id}`);
      message.success('创建成功');
    }
  } catch (err) { message.error(err instanceof Error ? err.message : '保存失败'); }
};

// ── 发布 ──
const handlePublishClick = async () => {
  if (!flowId.value) { message.warning('请先保存流程'); return; }

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
  if (!flowName.value.trim()) { message.warning('请输入流程名称'); return; }

  // 2. 服务端校验（防止绕过前端直接发布）
  const payload = buildRequest();
  if (!payload) return;
  validating.value = true;
  try {
    const result = await validateApprovalFlow(payload);
    if (!result.isValid) {
      validateResult.value = result;
      validateModalOpen.value = true;
      return;
    }
  } catch (err) {
    message.error(err instanceof Error ? err.message : '校验失败');
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
  try { await publishApprovalFlow(flowId.value); message.success('发布成功'); publishModalOpen.value = false; router.push('/approval/flows'); }
  catch (err) { message.error(err instanceof Error ? err.message : '发布失败'); }
  finally { publishing.value = false; }
};

// ── 预览 ──
const handlePreview = () => { previewModalOpen.value = true; };

const focusNodeByErrors = (errors: string[]) => {
  if (errors.length === 0) return;
  const firstError = errors[0];
  const idMatch = firstError.match(/node[_-]?[a-z0-9-]+/i);
  if (!idMatch) return;
  const target = findNodeById(flowTree.value.rootNode, idMatch[0]);
  if (target) {
    selectNode(target);
    activeStep.value = 2;
    message.warning(`已定位到异常节点：${target.nodeName}`);
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

  if (activeStep.value !== 2) {
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

/* ═══ 步骤内容（基础设置/表单设计） ═══ */
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

/* ═══ 流程设计三栏（撑满剩余高度） ═══ */
.dd-body--designer {
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
</style>
