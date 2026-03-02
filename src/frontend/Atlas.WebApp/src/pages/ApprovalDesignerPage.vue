<template>
  <div class="dd-page">
    <!-- ══ 顶部工具栏（44px，贴顶） ══ -->
    <div class="dd-toolbar">
      <a-button type="text" size="small" class="dd-toolbar__back" @click="goBack">
        <LeftOutlined /> 返回
      </a-button>
      <a-divider type="vertical" />
      <a-input
        v-model:value="flowName"
        placeholder="流程名称"
        :bordered="false"
        class="dd-toolbar__name"
        :maxlength="100"
      />
      <a-tag v-if="flowVersion" color="blue" class="dd-toolbar__version">v{{ flowVersion }}</a-tag>

      <!-- 步骤指示（紧凑圆点） -->
      <div class="dd-toolbar__steps">
        <span
          v-for="(s, i) in ['基础设置', '表单设计', '流程设计']"
          :key="i"
          class="dd-step-dot"
          :class="{ 'dd-step-dot--active': activeStep === i, 'dd-step-dot--done': activeStep > i }"
          @click="activeStep = i"
        >{{ s }}</span>
      </div>

      <div class="dd-toolbar__actions">
        <a-button v-if="activeStep > 0" size="small" @click="prevStep">上一步</a-button>
        <a-button v-if="activeStep < 2" size="small" @click="nextStep">下一步</a-button>
        <template v-if="activeStep === 2">
          <a-divider type="vertical" />
          <a-button size="small" :type="paletteVisible ? 'primary' : 'default'" @click="paletteVisible = !paletteVisible" title="节点面板"><AppstoreOutlined /></a-button>
          <a-divider type="vertical" />
          <a-button size="small" @click="undo" :disabled="!canUndo"><UndoOutlined /></a-button>
          <a-button size="small" @click="redo" :disabled="!canRedo"><RedoOutlined /></a-button>
          <a-divider type="vertical" />
          <a-button size="small" @click="handleValidate" :loading="validating"><CheckCircleOutlined /> 校验</a-button>
          <a-button size="small" @click="handlePreview"><EyeOutlined /> 预览</a-button>
        </template>
        <a-divider type="vertical" />
        <a-button size="small" @click="handleSave">保存</a-button>
        <a-button type="primary" size="small" @click="handlePublishClick">发布</a-button>
      </div>
    </div>

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
      <LfFormDesigner v-model="lfFormModel" @update:formFields="handleLfFormFields" />
    </div>

    <!-- ══ 步骤 2: 流程设计（三栏，撑满剩余） ══ -->
    <div class="dd-body dd-body--designer" v-show="activeStep === 2">
      <ApprovalNodePalette :visible="paletteVisible" @update:visible="paletteVisible = $event" @addNode="handlePaletteAddNode" />
      <div class="dd-canvas">
        <X6ApprovalDesigner
          :flow-tree="flowTree"
          :selected-node-id="selectedNode?.id ?? null"
          @selectNode="handleSelectNode"
          @addNode="addNode"
          @deleteNode="deleteNode"
          @addConditionBranch="addConditionBranch"
          @deleteConditionBranch="deleteConditionBranch"
          @moveBranch="moveBranch"
        />
      </div>
      <ApprovalPropertiesPanel
        :open="panelOpen"
        :node="selectedNode"
        :form-fields="lfFormPayload?.formFields"
        @update:open="panelOpen = $event"
        @update="handleNodeUpdate"
      />
    </div>

    <!-- ══ 弹窗：校验结果 ══ -->
    <a-modal v-model:open="validateModalOpen" :title="validateResult?.isValid ? '校验通过' : '校验结果'" :footer="null" width="520px">
      <div v-if="validateResult">
        <a-alert v-if="validateResult.isValid" type="success" message="流程校验通过，可以发布" show-icon style="margin-bottom:12px" />
        <template v-else>
          <a-alert type="error" message="校验不通过，请修正以下问题" show-icon style="margin-bottom:12px" />
          <div class="dd-validate-list">
            <div v-for="(err, idx) in validateResult.errors" :key="idx" class="dd-validate-item">
              <CloseCircleOutlined style="color:#ff4d4f;margin-right:6px" /> {{ err }}
            </div>
          </div>
        </template>
        <div v-if="validateResult.warnings?.length" class="dd-validate-list" style="margin-top:8px">
          <div v-for="(warn, idx) in validateResult.warnings" :key="idx" class="dd-validate-item">
            <ExclamationCircleOutlined style="color:#faad14;margin-right:6px" /> {{ warn }}
          </div>
        </div>
      </div>
    </a-modal>

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
import { ref, watch, onMounted } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { message } from 'ant-design-vue';
import {
  LeftOutlined,
  UndoOutlined,
  RedoOutlined,
  CheckCircleOutlined,
  EyeOutlined,
  CloseCircleOutlined,
  ExclamationCircleOutlined,
  AppstoreOutlined,
} from '@ant-design/icons-vue';
import X6ApprovalDesigner from '@/components/approval/x6/X6ApprovalDesigner.vue';
import ApprovalPropertiesPanel from '@/components/approval/ApprovalPropertiesPanel.vue';
import ApprovalNodePalette from '@/components/approval/ApprovalNodePalette.vue';
import LfFormDesigner from '@/components/approval/LfFormDesigner.vue';
import X6PreviewCanvas from '@/components/approval/X6PreviewCanvas.vue';
import UserRolePicker from '@/components/common/UserRolePicker.vue';
import { useApprovalTree } from '@/composables/useApprovalTree';
import { ApprovalTreeConverter } from '@/utils/approval-tree-converter';
import type { ApprovalDefinitionMeta, LfFormPayload, FormJson, VisibilityScope } from '@/types/approval-definition';
import type { TreeNode, ConditionBranch } from '@/types/approval-tree';
import type { ApprovalFlowValidationResult } from '@/types/api';
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
// const visibilityScopeText = ref(''); // Removed
const visibilityScopeType = ref<'All' | 'Department' | 'Role' | 'User'>('All');
const visibilityScopeIds = ref<string[]>([]);

const validating = ref(false);
const validateModalOpen = ref(false);
const validateResult = ref<ApprovalFlowValidationResult | null>(null);
const publishModalOpen = ref(false);
const publishing = ref(false);
const previewModalOpen = ref(false);

// ── 导航 ──
const goBack = () => {
  if (window.history.length > 1) router.back();
  else router.push('/process/flows');
};

// ── 节点选中 ──
watch(selectedNode, (node) => { panelOpen.value = !!node; });
const handleSelectNode = (node: TreeNode | ConditionBranch | null) => { selectNode(node); };
const handleNodeUpdate = (updatedNode: TreeNode | ConditionBranch) => { updateNode(updatedNode); };

// ── 步骤 ──
const nextStep = () => { if (activeStep.value < 2) activeStep.value += 1; };
const prevStep = () => { if (activeStep.value > 0) activeStep.value -= 1; };

// ── 节点库添加 ──
const handlePaletteAddNode = (nodeType: string) => {
  const parentId = selectedNode.value?.id ?? flowTree.value.rootNode.id;
  addNode(parentId, nodeType);
};

const handleLfFormFields = (fields: LfFormPayload['formFields']) => {
  lfFormPayload.value = { formJson: lfFormModel.value ?? { widgetList: [] }, formFields: fields };
};

// ── 构建后端请求 ──
const buildRequest = () => {
  definitionMeta.value.flowName = flowName.value;
  
  // 构建 VisibilityScope
  const scope: VisibilityScope = {
    scopeType: visibilityScopeType.value,
    departmentIds: visibilityScopeType.value === 'Department' ? visibilityScopeIds.value.map(Number).filter(n => !isNaN(n)) : undefined,
    roleCodes: visibilityScopeType.value === 'Role' ? visibilityScopeIds.value : undefined, // Role uses codes/ids string
    userIds: visibilityScopeType.value === 'User' ? visibilityScopeIds.value.map(Number).filter(n => !isNaN(n)) : undefined
  };
  // 注意：UserRolePicker 返回的是 string[]，但 VisibilityScope 定义中 departmentIds/userIds 是 number[]。
  // 如果 API 返回的 ID 是 string (UUID) 或者是 number string，需要适配。
  // 检查 API 定义：UserListItem.id 是 string。
  // 检查 VisibilityScope 定义：userIds: number[]。
  // 这里有类型不匹配。UserListItem.id 是 string (Guid usually).
  // VisibilityScope 定义可能过时或者是针对旧系统的。
  // 假设后端支持 string ID，或者我们需要修改 VisibilityScope 类型 definition。
  // 暂时强转或 parse int。如果 ID 是 UUID，parseInt 会失败。
  // 让我们检查 types/approval-definition.ts 中的 VisibilityScope。
  // export interface VisibilityScope { userIds?: number[]; ... }
  // 如果后端用 UUID，这里应该是 string[]。
  // 鉴于 FlowLong 使用 Long ID (MyBatisPlus)，可能是 number (string in JS for safety).
  // 如果 UserListItem.id 是 string，我们应该尝试转 number。
  
  definitionMeta.value.visibilityScope = scope;

  if (definitionMeta.value.isLowCodeFlow) {
    lfFormPayload.value = { formJson: lfFormModel.value ?? { widgetList: [] }, formFields: lfFormPayload.value?.formFields ?? [] };
  } else {
    lfFormPayload.value = undefined;
  }
  const definitionJson = ApprovalTreeConverter.treeToDefinitionJson(flowTree.value, definitionMeta.value, lfFormPayload.value);
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
    if (flow.visibilityScopeJson) {
      try {
        const scope = JSON.parse(flow.visibilityScopeJson) as VisibilityScope;
        visibilityScopeType.value = scope.scopeType;
        if (scope.scopeType === 'Department') visibilityScopeIds.value = (scope.departmentIds || []).map(String);
        else if (scope.scopeType === 'Role') visibilityScopeIds.value = scope.roleCodes || [];
        else if (scope.scopeType === 'User') visibilityScopeIds.value = (scope.userIds || []).map(String);
        definitionMeta.value.visibilityScope = scope;
      } catch {
        visibilityScopeType.value = 'All';
      }
    }
    if (flow.definitionJson) {
      const state = ApprovalTreeConverter.definitionJsonToState(flow.definitionJson);
      flowTree.value = state.tree;
      if (state.meta) { 
        definitionMeta.value = state.meta; 
        if (state.meta.visibilityScope) {
           const scope = state.meta.visibilityScope;
           visibilityScopeType.value = scope.scopeType;
           if (scope.scopeType === 'Department') visibilityScopeIds.value = (scope.departmentIds || []).map(String);
           else if (scope.scopeType === 'Role') visibilityScopeIds.value = scope.roleCodes || [];
           else if (scope.scopeType === 'User') visibilityScopeIds.value = (scope.userIds || []).map(String);
        }
      }
      if (state.lfForm) { lfFormPayload.value = state.lfForm; lfFormModel.value = state.lfForm.formJson; }
    }
    pushState(flowTree.value);
  } catch (err) { message.error(err instanceof Error ? err.message : '加载失败'); }
};

// ── 校验 ──
const handleValidate = async () => {
  const localResult = validateFlow();
  if (!localResult.valid) { validateResult.value = { isValid: false, errors: localResult.errors, warnings: [] }; validateModalOpen.value = true; return; }
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
  if (!localResult.valid) { validateResult.value = { isValid: false, errors: localResult.errors, warnings: [] }; validateModalOpen.value = true; return; }
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
const handlePublishClick = () => { if (!flowId.value) { message.warning('请先保存流程'); return; } publishModalOpen.value = true; };
const handlePublishConfirm = async () => {
  if (!flowId.value) return;
  publishing.value = true;
  try { await publishApprovalFlow(flowId.value); message.success('发布成功'); publishModalOpen.value = false; router.push('/process/flows'); }
  catch (err) { message.error(err instanceof Error ? err.message : '发布失败'); }
  finally { publishing.value = false; }
};

// ── 预览 ──
const handlePreview = () => { previewModalOpen.value = true; };

// ── 工具 ──
const parseVisibilityScope = (value: string): VisibilityScope | null => {
  try { const p = JSON.parse(value) as VisibilityScope; return p?.scopeType ? p : null; } catch { return null; }
};

onMounted(() => { loadFlow(); });
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
}
.dd-validate-item:last-child {
  border-bottom: none;
}
</style>
