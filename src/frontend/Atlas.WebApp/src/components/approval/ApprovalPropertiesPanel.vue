<template>
  <div class="dd-props-panel" :class="{ 'is-open': open }">
    <!-- 面板头部 -->
    <div class="dd-props-header" v-if="formData">
      <div class="dd-props-header__info">
        <div class="dd-props-header__icon" :class="iconClass">
          <component :is="nodeIcon" />
        </div>
        <a-input
          v-if="'nodeName' in formData"
          v-model:value="formData.nodeName"
          class="dd-props-header__name"
          :bordered="false"
          placeholder="节点名称"
        />
        <a-input
          v-else-if="'branchName' in formData"
          v-model:value="(formData as BranchForm).branchName"
          class="dd-props-header__name"
          :bordered="false"
          placeholder="分支名称"
        />
      </div>
      <button class="dd-props-header__close" @click="handleClose">
        <CloseOutlined />
      </button>
    </div>
    <!-- 节点校验错误提示 -->
    <a-alert
      v-if="props.node && 'error' in props.node && props.node.error"
      type="error"
      show-icon
      message="该节点存在配置错误，请检查并修复后再发布"
      style="margin: 0 12px 8px"
      banner
    />

    <!-- 面板内容 -->
    <div class="dd-props-body" v-if="formData">
      <!-- ═══ 审批节点 ═══ -->
      <template v-if="approveForm">
        <a-tabs v-model:activeKey="activeTab" size="small" class="dd-props-tabs">
          <!-- Tab 1: 审批设置 -->
          <a-tab-pane key="approver" tab="审批设置">
            <a-form layout="vertical" class="dd-props-form">
              <a-form-item label="审批人类型">
                <a-select v-model:value="approveForm.assigneeType" @change="onApproverTypeChange">
                  <a-select-option
                    v-for="option in assigneeTypeOptions"
                    :key="option.value"
                    :value="option.value"
                  >
                    {{ option.label }}
                  </a-select-option>
                </a-select>
              </a-form-item>

              <a-form-item
                label="选择审批人"
                v-if="isPickerAssigneeType(approveForm.assigneeType)"
              >
                <UserRolePicker
                  v-if="approveForm.assigneeType === 0"
                  mode="user"
                  v-model:value="approverTargets"
                  placeholder="请选择审批人"
                />
                <UserRolePicker
                  v-else
                  mode="role"
                  v-model:value="approverTargets"
                  placeholder="请选择角色"
                />
              </a-form-item>

              <a-form-item label="审批层级" v-else-if="approveForm.assigneeType === 4">
                <a-input-number
                  v-model:value="assigneeLevel"
                  :min="1"
                  :max="20"
                  style="width: 100%"
                  placeholder="请输入向上审批层级"
                />
              </a-form-item>

              <a-form-item
                label="业务字段路径"
                v-else-if="approveForm.assigneeType === 9"
              >
                <a-input
                  v-model:value="assigneeExpression"
                  placeholder="示例：formData.ownerId"
                />
              </a-form-item>

              <a-form-item
                label="外部人员字段"
                v-else-if="approveForm.assigneeType === 10"
              >
                <a-input
                  v-model:value="assigneeExpression"
                  placeholder="示例：externalApprovers"
                />
              </a-form-item>

              <a-alert
                v-else
                type="info"
                show-icon
                :message="getAssigneeTypeHint(approveForm.assigneeType)"
                style="margin-bottom: 16px"
              />

              <a-form-item label="多人审批方式">
                <div class="dd-radio-cards">
                  <div
                    class="dd-radio-card"
                    :class="{ active: approveForm.approvalMode === 'all' }"
                    @click="approveForm.approvalMode = 'all'"
                  >
                    <div class="dd-radio-card__title">会签</div>
                    <div class="dd-radio-card__desc">需所有审批人同意</div>
                  </div>
                  <div
                    class="dd-radio-card"
                    :class="{ active: approveForm.approvalMode === 'any' }"
                    @click="approveForm.approvalMode = 'any'"
                  >
                    <div class="dd-radio-card__title">或签</div>
                    <div class="dd-radio-card__desc">一人同意即可</div>
                  </div>
                  <div
                    class="dd-radio-card"
                    :class="{ active: approveForm.approvalMode === 'sequential' }"
                    @click="approveForm.approvalMode = 'sequential'"
                  >
                    <div class="dd-radio-card__title">顺序签</div>
                    <div class="dd-radio-card__desc">按顺序依次审批</div>
                  </div>
                  <div
                    class="dd-radio-card"
                    :class="{ active: approveForm.approvalMode === 'vote' }"
                    @click="approveForm.approvalMode = 'vote'"
                  >
                    <div class="dd-radio-card__title">票签</div>
                    <div class="dd-radio-card__desc">按权重投票</div>
                  </div>
                </div>
              </a-form-item>

              <template v-if="approveForm.approvalMode === 'vote'">
                <a-form-item label="节点票权重">
                  <a-input-number
                    v-model:value="approveForm.voteWeight"
                    :min="1"
                    :max="100"
                    :precision="0"
                    style="width: 100%"
                  />
                </a-form-item>
                <a-form-item label="票签通过率 (%)">
                  <a-slider v-model:value="approveForm.votePassRate" :min="1" :max="100" />
                </a-form-item>
              </template>

              <a-form-item label="审批人为空时">
                <a-select v-model:value="approveForm.noHeaderAction">
                  <a-select-option :value="0">不允许发起</a-select-option>
                  <a-select-option :value="1">自动跳过</a-select-option>
                  <a-select-option :value="2">转交管理员</a-select-option>
                </a-select>
              </a-form-item>

              <a-form-item label="审批人与发起人为同一人时">
                <a-select v-model:value="approveForm.approveSelf" :default-value="0">
                  <a-select-option :value="0">由发起人自己审批</a-select-option>
                  <a-select-option :value="1">自动跳过</a-select-option>
                  <a-select-option :value="2">转交直属上级</a-select-option>
                  <a-select-option :value="3">转交部门负责人</a-select-option>
                </a-select>
              </a-form-item>
            </a-form>
          </a-tab-pane>

          <!-- Tab 2: 高级设置 -->
          <a-tab-pane key="advanced" tab="高级设置">
            <a-form layout="vertical" class="dd-props-form">
              <a-form-item label="超时处理">
                <a-switch v-model:checked="approveForm.timeoutEnabled" />
                <span class="dd-switch-label">开启超时自动处理</span>
              </a-form-item>
              
              <template v-if="approveForm.timeoutEnabled">
                <a-form-item label="超时时间">
                  <a-input-group compact>
                    <a-input-number v-model:value="approveForm.timeoutHours" :min="0" style="width: 80px" />
                    <span class="dd-input-addon">小时</span>
                    <a-input-number v-model:value="approveForm.timeoutMinutes" :min="0" :max="59" style="width: 80px" />
                    <span class="dd-input-addon">分钟</span>
                  </a-input-group>
                </a-form-item>
                
                <a-form-item label="超时策略">
                  <a-select v-model:value="approveForm.timeoutAction">
                    <a-select-option value="none">仅提醒</a-select-option>
                    <a-select-option value="autoApprove">自动通过</a-select-option>
                    <a-select-option value="autoReject">自动驳回</a-select-option>
                    <a-select-option value="autoSkip">自动跳过</a-select-option>
                  </a-select>
                </a-form-item>
              </template>

              <a-divider />

              <a-form-item label="去重策略">
                <a-select v-model:value="approveForm.deduplicationType">
                  <a-select-option value="none">不去重</a-select-option>
                  <a-select-option value="skipSame">连续审批人相同时自动跳过</a-select-option>
                  <a-select-option value="global">流程内自动去重</a-select-option>
                </a-select>
              </a-form-item>

              <a-form-item label="驳回策略">
                <a-select v-model:value="approveForm.rejectStrategy">
                  <a-select-option value="toPrevious">退回上一步</a-select-option>
                  <a-select-option value="toInitiator">退回发起人</a-select-option>
                  <a-select-option value="toAnyNode">退回任意节点</a-select-option>
                  <a-select-option value="terminateApproval">终止审批流程</a-select-option>
                  <a-select-option value="toParentNode">退回父级审批节点</a-select-option>
                </a-select>
              </a-form-item>

              <a-form-item label="重新审批策略">
                <a-select v-model:value="approveForm.reApproveStrategy">
                  <a-select-option value="continue">从驳回节点继续往后执行</a-select-option>
                  <a-select-option value="backToRejectNode">重新从驳回目标节点开始审批</a-select-option>
                </a-select>
              </a-form-item>

              <a-form-item label="分组策略">
                <a-select v-model:value="approveForm.groupStrategy">
                  <a-select-option value="claim">认领模式</a-select-option>
                  <a-select-option value="allParticipate">全员审批</a-select-option>
                </a-select>
              </a-form-item>

              <a-divider />

              <a-form-item label="AI 智能审批">
                <a-switch v-model:checked="approveForm.callAi" />
                <span class="dd-switch-label">启用 AI 辅助决策</span>
              </a-form-item>

              <template v-if="approveForm.callAi">
                <a-form-item label="AI 配置 (JSON)">
                  <a-textarea 
                    :value="approveForm.aiConfig"
                    @update:value="(val: string) => approveForm.aiConfig = val"
                    :rows="4" 
                    placeholder="请输入 AI 配置 JSON"
                  />
                </a-form-item>
              </template>
            </a-form>
          </a-tab-pane>

          <!-- Tab 3: 表单权限 -->
          <a-tab-pane key="form-perm" tab="表单权限">
            <div v-if="!formFields || formFields.length === 0" class="dd-empty-state">
              请先在表单设计步骤添加字段
            </div>
            <div v-else class="dd-perm-table">
              <div class="dd-perm-header">
                <div class="dd-perm-col name">字段名称</div>
                <div class="dd-perm-col perm">权限</div>
              </div>
              <div v-for="field in formFields" :key="field.id || field.fieldId" class="dd-perm-row">
                <div class="dd-perm-col name">{{ field.label || field.fieldName }}</div>
                <div class="dd-perm-col perm">
                  <a-radio-group 
                    v-model:value="formPermMap[field.id || field.fieldId]" 
                    size="small"
                    button-style="solid"
                  >
                    <a-radio-button value="E">编辑</a-radio-button>
                    <a-radio-button value="R">只读</a-radio-button>
                    <a-radio-button value="H">隐藏</a-radio-button>
                  </a-radio-group>
                </div>
              </div>
            </div>
          </a-tab-pane>

          <!-- Tab 4: 通知设置 -->
          <a-tab-pane key="notice" tab="通知设置">
             <a-form layout="vertical" class="dd-props-form">
              <a-form-item label="通知渠道">
                <a-checkbox-group v-model:value="noticeChannels">
                  <a-row>
                    <a-col :span="12"><a-checkbox :value="1">站内信</a-checkbox></a-col>
                    <a-col :span="12"><a-checkbox :value="2">邮件</a-checkbox></a-col>
                    <a-col :span="12"><a-checkbox :value="3">短信</a-checkbox></a-col>
                    <a-col :span="12"><a-checkbox :value="4">企业微信</a-checkbox></a-col>
                    <a-col :span="12"><a-checkbox :value="5">钉钉</a-checkbox></a-col>
                  </a-row>
                </a-checkbox-group>
              </a-form-item>
              <a-form-item label="通知模板">
                <a-select v-model:value="noticeTemplateId" placeholder="请选择模板">
                  <a-select-option value="default">默认模板</a-select-option>
                  <a-select-option value="urgent">加急模板</a-select-option>
                </a-select>
              </a-form-item>
            </a-form>
          </a-tab-pane>
        </a-tabs>
      </template>

      <!-- ═══ 抄送节点 ═══ -->
      <template v-else-if="copyForm">
        <a-form layout="vertical" class="dd-props-form">
          <a-form-item label="抄送人">
            <UserRolePicker
              mode="user"
              v-model:value="copyForm.copyToUsers"
              placeholder="请选择抄送人"
            />
          </a-form-item>
          <a-divider />
          <div class="dd-section-title">表单权限</div>
          <div v-if="!formFields || formFields.length === 0" class="dd-empty-state">
            请先在表单设计步骤添加字段
          </div>
          <div v-else class="dd-perm-table">
             <div class="dd-perm-header">
                <div class="dd-perm-col name">字段名称</div>
                <div class="dd-perm-col perm">权限</div>
              </div>
              <div v-for="field in formFields" :key="field.id || field.fieldId" class="dd-perm-row">
                <div class="dd-perm-col name">{{ field.label || field.fieldName }}</div>
                <div class="dd-perm-col perm">
                  <a-radio-group 
                    v-model:value="formPermMap[field.id || field.fieldId]" 
                    size="small"
                    button-style="solid"
                  >
                    <a-radio-button value="R">只读</a-radio-button>
                    <a-radio-button value="H">隐藏</a-radio-button>
                  </a-radio-group>
                </div>
              </div>
          </div>
        </a-form>
      </template>

      <!-- ═══ 条件分支 ═══ -->
      <template v-else-if="branchForm">
        <a-form layout="vertical" class="dd-props-form">
          <a-form-item label="是否默认分支">
            <a-switch v-model:checked="branchForm.isDefault" />
            <span class="dd-switch-hint" v-if="branchForm.isDefault">
              其他条件均不满足时，默认走此分支
            </span>
          </a-form-item>

          <template v-if="!branchForm.isDefault">
            <a-divider>条件规则</a-divider>
            <a-form-item label="CEL 表达式（可选）">
              <ExpressionEditorCel
                v-model="branchForm.conditionExpr"
                @validate="onExpressionValidate"
              />
            </a-form-item>
            <ConditionGroupEditor 
              v-model="branchForm.conditionGroups" 
              :form-fields="formFields" 
            />
          </template>
        </a-form>
      </template>

      <!-- ═══ 包容分支 ═══ -->
      <template v-else-if="inclusiveForm">
        <a-form layout="vertical" class="dd-props-form">
          <a-alert message="包容分支会同时执行所有满足条件的分支" type="info" show-icon style="margin-bottom: 16px" />
          <a-divider>条件规则</a-divider>
          <!-- 包容分支通常是多条路径，每条路径有条件。这里的 inclusiveForm 对应的是 InclusiveNode，它包含 conditionNodes -->
          <!-- 但 PropertiesPanel 是针对单个节点的。如果是 InclusiveNode，我们可能需要展示它的分支列表？ -->
          <!-- 或者，如果选中的是 InclusiveNode 下的某个分支（ConditionBranch），则展示分支配置（同 branchForm） -->
          <!-- 这里假设 inclusiveForm 是 InclusiveNode 本身，通常不需要配置太多，主要是分支的条件 -->
          <div class="dd-empty-state">
            请点击具体的分支线条进行条件配置
          </div>
        </a-form>
      </template>

      <!-- ═══ 路由分支 ═══ -->
      <template v-else-if="routeForm">
        <a-form layout="vertical" class="dd-props-form">
          <a-form-item label="目标节点ID">
            <a-input v-model:value="routeForm.routeTargetNodeId" placeholder="请输入目标节点ID" />
          </a-form-item>
        </a-form>
      </template>

      <!-- ═══ 子流程节点 ═══ -->
      <template v-else-if="callProcessForm">
        <a-form layout="vertical" class="dd-props-form">
          <a-form-item label="子流程定义ID">
            <a-input v-model:value="callProcessForm.callProcessId" placeholder="请输入子流程定义ID" />
          </a-form-item>
          <a-form-item label="执行方式">
            <a-radio-group v-model:value="callProcessForm.callAsync">
              <a-radio :value="false">同步（等待子流程结束）</a-radio>
              <a-radio :value="true">异步（不等待）</a-radio>
            </a-radio-group>
          </a-form-item>
        </a-form>
      </template>

      <!-- ═══ 定时器节点 ═══ -->
      <template v-else-if="timerForm">
        <a-form layout="vertical" class="dd-props-form">
          <a-form-item label="定时类型">
            <a-select v-model:value="timerForm.timerConfig.type">
              <a-select-option value="duration">等待时长</a-select-option>
              <a-select-option value="date">指定时间</a-select-option>
            </a-select>
          </a-form-item>
          <a-form-item label="等待时长（秒）" v-if="timerForm.timerConfig.type === 'duration'">
            <a-input-number v-model:value="timerForm.timerConfig.duration" :min="0" style="width: 100%" />
          </a-form-item>
          <a-form-item label="指定时间" v-if="timerForm.timerConfig.type === 'date'">
            <a-date-picker v-model:value="timerForm.timerConfig.date" show-time value-format="YYYY-MM-DD HH:mm:ss" style="width: 100%" />
          </a-form-item>
        </a-form>
      </template>

      <!-- ═══ 触发器节点 ═══ -->
      <template v-else-if="triggerForm">
        <a-form layout="vertical" class="dd-props-form">
          <a-form-item label="触发类型">
            <a-select v-model:value="triggerForm.triggerType">
              <a-select-option value="immediate">立即触发</a-select-option>
              <a-select-option value="scheduled">定时触发</a-select-option>
            </a-select>
          </a-form-item>
          <a-form-item label="触发器配置 (JSON)">
            <a-textarea 
              :value="JSON.stringify(triggerForm.triggerConfig, null, 2)"
              @update:value="(val: string) => applyJsonConfig(val, '触发器配置', (v) => triggerForm.triggerConfig = v)"
              :rows="4" 
            />
          </a-form-item>
        </a-form>
      </template>

      <!-- ═══ 发起人节点 ═══ -->
      <template v-else-if="startForm">
        <a-form layout="vertical" class="dd-props-form">
          <a-form-item label="发起条件">
            <a-alert message="所有人都可以作为发起人" type="info" show-icon />
          </a-form-item>
        </a-form>
      </template>
    </div>

    <!-- 底部按钮 -->
    <div class="dd-props-footer" v-if="formData">
      <a-button type="primary" block @click="handleSave">确 定</a-button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, computed } from 'vue';
import {
  CloseOutlined,
  PlusOutlined,
  DeleteOutlined,
  UserOutlined,
  SendOutlined,
  BranchesOutlined,
  PlayCircleOutlined,
  NodeIndexOutlined,
  SwapOutlined,
  SubnodeOutlined,
  ClockCircleOutlined,
  ThunderboltOutlined,
} from '@ant-design/icons-vue';
import { message } from 'ant-design-vue';
import UserRolePicker from '@/components/common/UserRolePicker.vue';
import ConditionGroupEditor from './ConditionGroupEditor.vue';
import ExpressionEditorCel from './ExpressionEditorCel.vue';
import type { 
  TreeNode, 
  ConditionBranch, 
  ApproveNode, 
  CopyNode, 
  StartNode, 
  ConditionGroup,
  InclusiveNode,
  RouteNode,
  CallProcessNode,
  TimerNode,
  TriggerNode
} from '@/types/approval-tree';
import type { LfFormField } from '@/types/approval-definition';

// ── 内部类型 ──
interface BranchForm {
  id: string;
  branchName: string;
  isDefault?: boolean;
  conditionRule?: {
    field: string;
    operator: string;
    value: unknown;
  };
  conditionGroups?: ConditionGroup[];
  conditionExpr?: string;
}

// ── Props / Emits ──
const props = defineProps<{
  open: boolean;
  node: TreeNode | ConditionBranch | null;
  formFields?: LfFormField[];
}>();

const emit = defineEmits<{
  'update:open': [value: boolean];
  update: [node: TreeNode | ConditionBranch];
}>();

// ── 状态 ──
const formData = ref<any>(null);
const approveForm = ref<any>(null);
const copyForm = ref<any>(null);
const branchForm = ref<any>(null);
const inclusiveForm = ref<any>(null);
const routeForm = ref<any>(null);
const callProcessForm = ref<any>(null);
const timerForm = ref<any>(null);
const triggerForm = ref<any>(null);
const startForm = ref<any>(null);
const activeTab = ref('approver');

const approverTargets = ref<string[]>([]);
const assigneeExpression = ref('');
const assigneeLevel = ref<number | null>(null);
// const noticeConfigText = ref('');
const noticeChannels = ref<number[]>([]);
const noticeTemplateId = ref<string | undefined>(undefined);
const formPermMap = ref<Record<string, string>>({});
const expressionValid = ref(true);
const assigneeTypeOptions: Array<{ value: ApproveNode['assigneeType']; label: string }> = [
  { value: 0, label: '指定人员' },
  { value: 1, label: '指定角色' },
  { value: 2, label: '部门负责人' },
  { value: 3, label: '逐级领导（Loop）' },
  { value: 4, label: '指定层级（Level）' },
  { value: 5, label: '直属领导' },
  { value: 6, label: '发起人' },
  { value: 7, label: 'HRBP' },
  { value: 8, label: '发起人自选' },
  { value: 9, label: '业务字段取人' },
  { value: 10, label: '外部传入人员' },
];

// ── 计算属性 ──
const iconClass = computed(() => {
  if (approveForm.value) return 'dd-props-header__icon--approve';
  if (copyForm.value) return 'dd-props-header__icon--copy';
  if (branchForm.value) return 'dd-props-header__icon--condition';
  if (inclusiveForm.value) return 'dd-props-header__icon--condition';
  if (routeForm.value) return 'dd-props-header__icon--route';
  if (callProcessForm.value) return 'dd-props-header__icon--call-process';
  if (timerForm.value) return 'dd-props-header__icon--timer';
  if (triggerForm.value) return 'dd-props-header__icon--trigger';
  return 'dd-props-header__icon--start';
});

const nodeIcon = computed(() => {
  if (approveForm.value) return UserOutlined;
  if (copyForm.value) return SendOutlined;
  if (branchForm.value) return BranchesOutlined;
  if (inclusiveForm.value) return NodeIndexOutlined;
  if (routeForm.value) return SwapOutlined;
  if (callProcessForm.value) return SubnodeOutlined;
  if (timerForm.value) return ClockCircleOutlined;
  if (triggerForm.value) return ThunderboltOutlined;
  return PlayCircleOutlined;
});

// ── Watch props.node ──
watch(
  () => props.node,
  (newNode) => {
    if (newNode) {
      formData.value = structuredClone(newNode);
      syncNodeRefs();
    } else {
      formData.value = null;
      clearRefs();
    }
  },
  { immediate: true },
);

function syncNodeRefs() {
  const current = formData.value;
  clearRefs();

  if (!current) return;

  if (isApproveNode(current)) {
    const node = current as ApproveNode;
    approveForm.value = node;

    // 初始化审批人配置
    if (isPickerAssigneeType(node.assigneeType)) {
      approverTargets.value = node.assigneeValue ? node.assigneeValue.split(',').filter(Boolean) : [];
      assigneeExpression.value = '';
      assigneeLevel.value = null;
    } else if (node.assigneeType === 4) {
      approverTargets.value = [];
      assigneeExpression.value = '';
      const parsedLevel = Number.parseInt(node.assigneeValue, 10);
      assigneeLevel.value = Number.isFinite(parsedLevel) && parsedLevel > 0 ? parsedLevel : null;
    } else {
      approverTargets.value = [];
      assigneeExpression.value = node.assigneeValue ?? '';
      assigneeLevel.value = null;
    }

    if (!node.voteWeight || node.voteWeight < 1) {
      node.voteWeight = 1;
    }
    if (!node.votePassRate || node.votePassRate < 1 || node.votePassRate > 100) {
      node.votePassRate = 60;
    }

    // 初始化表单权限
    formPermMap.value = {};
    if (props.formFields) {
      props.formFields.forEach(f => {
        const fieldId = f.id || f.fieldId;
        const perm = node.formPermissionConfig?.fields?.find(p => p.fieldId === fieldId)?.perm;
        formPermMap.value[fieldId] = perm || 'E'; // 默认可编辑
      });
    }

    // 初始化通知配置
    if (node.noticeConfig) {
      noticeChannels.value = node.noticeConfig.channelIds || [];
      noticeTemplateId.value = node.noticeConfig.templateId;
    } else {
      noticeChannels.value = [1]; // 默认站内信
      noticeTemplateId.value = undefined;
    }

    // 初始化 AI 配置 (如果 approveForm.value 是 reactive 的，应该已经有了)
    if (!approveForm.value.callAi) approveForm.value.callAi = false;
    if (!approveForm.value.aiConfig) approveForm.value.aiConfig = '';
      
  } else if (isCopyNode(current)) {
    const node = current as CopyNode;
    copyForm.value = node;
    
    // 初始化表单权限
    formPermMap.value = {};
    if (props.formFields) {
      props.formFields.forEach(f => {
        const fieldId = f.id || f.fieldId;
        const perm = node.formPermissionConfig?.fields?.find(p => p.fieldId === fieldId)?.perm;
        formPermMap.value[fieldId] = perm || 'R'; // 默认只读
      });
    }

  } else if (isConditionBranch(current)) {
    // 迁移旧版 conditionRule 到 conditionGroups
    let groups = current.conditionGroups ? structuredClone(current.conditionGroups) : [];
    if (groups.length === 0 && current.conditionRule) {
      groups = [{
        conditions: [{
          field: current.conditionRule.field,
          operator: current.conditionRule.operator,
          value: current.conditionRule.value as any
        }]
      }];
    }

    branchForm.value = {
      id: current.id,
      branchName: current.branchName,
      isDefault: current.isDefault,
      conditionRule: current.conditionRule
        ? { ...current.conditionRule, value: current.conditionRule.value as unknown }
        : undefined,
      conditionGroups: groups,
      conditionExpr: extractConditionExpr(current.conditionRule)
    };
  } else if (isInclusiveNode(current)) {
    inclusiveForm.value = current;
  } else if (isRouteNode(current)) {
    routeForm.value = current;
  } else if (isCallProcessNode(current)) {
    callProcessForm.value = current;
  } else if (isTimerNode(current)) {
    timerForm.value = current;
    if (!timerForm.value.timerConfig) {
        timerForm.value.timerConfig = { type: 'duration', duration: 0 };
    }
  } else if (isTriggerNode(current)) {
    triggerForm.value = current;
  } else if (isStartNode(current)) {
    startForm.value = current;
  }
}

function clearRefs() {
  approveForm.value = null;
  copyForm.value = null;
  branchForm.value = null;
  inclusiveForm.value = null;
  routeForm.value = null;
  callProcessForm.value = null;
  timerForm.value = null;
  triggerForm.value = null;
  startForm.value = null;
  activeTab.value = 'approver';
  approverTargets.value = [];
  assigneeExpression.value = '';
  assigneeLevel.value = null;
  formPermMap.value = {};
  noticeChannels.value = [];
  noticeTemplateId.value = undefined;
}

// ── Event handlers ──
function onApproverTypeChange() {
  // 切换类型时清空审批人配置
  approverTargets.value = [];
  assigneeExpression.value = '';
  assigneeLevel.value = null;
}

function handleClose() {
  emit('update:open', false);
}

function handleSave() {
  if (!formData.value) return;

  if (approveForm.value) {
    // 保存审批人配置
    if (isPickerAssigneeType(approveForm.value.assigneeType)) {
      approveForm.value.assigneeValue = approverTargets.value.join(',');
    } else if (approveForm.value.assigneeType === 4) {
      approveForm.value.assigneeValue = assigneeLevel.value ? String(assigneeLevel.value) : '';
    } else {
      approveForm.value.assigneeValue = assigneeExpression.value.trim();
    }

    if (approveForm.value.approvalMode === 'vote') {
      if (!approveForm.value.voteWeight || approveForm.value.voteWeight < 1) {
        approveForm.value.voteWeight = 1;
      }
      if (!approveForm.value.votePassRate || approveForm.value.votePassRate < 1 || approveForm.value.votePassRate > 100) {
        approveForm.value.votePassRate = 60;
      }
    }
    
    // 保存表单权限
    const fields = Object.entries(formPermMap.value).map(([fieldId, perm]) => ({ fieldId, perm }));
    approveForm.value.formPermissionConfig = { fields };

    // 保存通知配置
    approveForm.value.noticeConfig = {
      channelIds: noticeChannels.value,
      templateId: noticeTemplateId.value
    };

    // 保存 AI 配置
    // aiConfig 已经在 v-model 中绑定到 approveForm.value.aiConfig
  }

  if (copyForm.value) {
     // 保存表单权限
    const fields = Object.entries(formPermMap.value).map(([fieldId, perm]) => ({ fieldId, perm }));
    copyForm.value.formPermissionConfig = { fields };
  }

  // 如果是分支，合并回 formData
  if (branchForm.value) {
    if (!expressionValid.value) {
      message.warning('请先修复 CEL 表达式错误');
      return;
    }
    const branch = formData.value as ConditionBranch;
    branch.branchName = branchForm.value.branchName;
    branch.isDefault = branchForm.value.isDefault;
    // 保存 conditionGroups
    branch.conditionGroups = branchForm.value.conditionGroups;
    // 同时也更新旧版字段以保持兼容（取第一个条件的第一个规则）
    const celExpr = branchForm.value.conditionExpr?.trim();
    if (celExpr) {
      branch.conditionRule = {
        exprType: 'cel',
        expression: celExpr
      } as unknown as typeof branch.conditionRule;
    } else if (branch.conditionGroups && branch.conditionGroups.length > 0 && branch.conditionGroups[0].conditions.length > 0) {
      const first = branch.conditionGroups[0].conditions[0];
      branch.conditionRule = {
        field: first.field,
        operator: first.operator,
        value: first.value
      };
    } else {
      branch.conditionRule = undefined;
    }
  }

  emit('update', formData.value);
  handleClose();
}

function applyJsonConfig(text: string, label: string, setter: (v: unknown) => void): boolean {
  if (!text.trim()) {
    setter(undefined);
    return true;
  }
  try {
    setter(JSON.parse(text));
    return true;
  } catch {
    message.error(`${label} JSON 格式不正确`);
    return false;
  }
}

function initConditionRule() {
  if (!branchForm.value) return;
  branchForm.value.conditionRule = {
    field: '',
    operator: 'equals',
    value: '',
  };
}

function removeConditionRule() {
  if (!branchForm.value) return;
  branchForm.value.conditionRule = undefined;
}

function onExpressionValidate(valid: boolean) {
  expressionValid.value = valid;
}

function extractConditionExpr(conditionRule: unknown): string | undefined {
  if (!conditionRule || typeof conditionRule !== 'object') {
    return undefined;
  }
  const value = conditionRule as { exprType?: unknown; expression?: unknown };
  if (value.exprType === 'cel' && typeof value.expression === 'string') {
    return value.expression;
  }
  return undefined;
}

function isPickerAssigneeType(type: ApproveNode['assigneeType']): boolean {
  return type === 0 || type === 1;
}

function getAssigneeTypeHint(type: ApproveNode['assigneeType']): string {
  switch (type) {
    case 2:
      return '系统会自动解析发起人所在部门负责人作为审批人。';
    case 3:
      return '系统会沿组织架构逐级向上查找审批人。';
    case 5:
      return '系统会自动解析发起人的直属领导。';
    case 6:
      return '当前流程发起人将作为审批人。';
    case 7:
      return '系统会自动匹配当前组织下配置的 HRBP。';
    case 8:
      return '由发起人在提交时手动选择审批人。';
    default:
      return '请根据审批场景配置该类型的参数。';
  }
}

// ── 类型守卫 ──
function isApproveNode(n: TreeNode | ConditionBranch): n is ApproveNode {
  return 'nodeType' in n && n.nodeType === 'approve';
}
function isCopyNode(n: TreeNode | ConditionBranch): n is CopyNode {
  return 'nodeType' in n && n.nodeType === 'copy';
}
function isStartNode(n: TreeNode | ConditionBranch): n is StartNode {
  return 'nodeType' in n && n.nodeType === 'start';
}
function isConditionBranch(n: TreeNode | ConditionBranch): n is ConditionBranch {
  return 'branchName' in n;
}
function isInclusiveNode(n: TreeNode | ConditionBranch): n is InclusiveNode {
  return 'nodeType' in n && n.nodeType === 'inclusive';
}
function isRouteNode(n: TreeNode | ConditionBranch): n is RouteNode {
  return 'nodeType' in n && n.nodeType === 'route';
}
function isCallProcessNode(n: TreeNode | ConditionBranch): n is CallProcessNode {
  return 'nodeType' in n && n.nodeType === 'callProcess';
}
function isTimerNode(n: TreeNode | ConditionBranch): n is TimerNode {
  return 'nodeType' in n && n.nodeType === 'timer';
}
function isTriggerNode(n: TreeNode | ConditionBranch): n is TriggerNode {
  return 'nodeType' in n && n.nodeType === 'trigger';
}
</script>

<style scoped>
.dd-props-panel {
  position: absolute;
  right: 0;
  top: 0;
  bottom: 0;
  width: 0;
  background: #fff;
  box-shadow: -4px 0 12px rgba(0, 0, 0, 0.08);
  display: flex;
  flex-direction: column;
  overflow: hidden;
  transition: width 0.3s cubic-bezier(0.645, 0.045, 0.355, 1);
  z-index: 20;
}

.dd-props-panel.is-open {
  width: 400px;
}

/* ── Header ── */
.dd-props-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px 16px 12px;
  border-bottom: 1px solid #f0f0f0;
  flex-shrink: 0;
}

.dd-props-header__info {
  display: flex;
  align-items: center;
  gap: 8px;
  flex: 1;
  min-width: 0;
}

.dd-props-header__icon {
  width: 32px;
  height: 32px;
  border-radius: 6px;
  display: flex;
  align-items: center;
  justify-content: center;
  color: #fff;
  font-size: 16px;
  flex-shrink: 0;
}

.dd-props-header__icon--approve { background: #ff943e; }
.dd-props-header__icon--copy { background: #3296fa; }
.dd-props-header__icon--condition { background: #15bc83; }
.dd-props-header__icon--route { background: #718dff; }
.dd-props-header__icon--call-process { background: #faad14; }
.dd-props-header__icon--timer { background: #f5222d; }
.dd-props-header__icon--trigger { background: #722ed1; }
.dd-props-header__icon--start { background: #576a95; }

.dd-props-header__name {
  font-size: 16px;
  font-weight: 600;
  padding: 0;
}

.dd-props-header__close {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 28px;
  height: 28px;
  border: none;
  background: transparent;
  cursor: pointer;
  border-radius: 4px;
  color: #8c8c8c;
  font-size: 14px;
  flex-shrink: 0;
}

.dd-props-header__close:hover {
  background: #f5f5f5;
  color: #1a1a1a;
}

/* ── Body ── */
.dd-props-body {
  flex: 1;
  overflow-y: auto;
  padding: 0;
}

.dd-props-tabs {
  height: 100%;
}
.dd-props-tabs :deep(.ant-tabs-content) {
  height: 100%;
}
.dd-props-tabs :deep(.ant-tabs-tabpane) {
  padding: 12px 16px;
  overflow-y: auto;
}

.dd-props-form {
  padding: 4px 0;
}

/* ── Radio Cards ── */
.dd-radio-cards {
  display: flex;
  gap: 8px;
}
.dd-radio-card {
  flex: 1;
  border: 1px solid #d9d9d9;
  border-radius: 4px;
  padding: 8px;
  cursor: pointer;
  transition: all 0.2s;
  text-align: center;
}
.dd-radio-card:hover {
  border-color: #1677ff;
}
.dd-radio-card.active {
  border-color: #1677ff;
  background: #e6f4ff;
  color: #1677ff;
}
.dd-radio-card__title {
  font-size: 13px;
  font-weight: 500;
  margin-bottom: 2px;
}
.dd-radio-card__desc {
  font-size: 11px;
  color: #8c8c8c;
}
.dd-radio-card.active .dd-radio-card__desc {
  color: #1677ff;
}

/* ── Switch & Input Addon ── */
.dd-switch-label {
  margin-left: 8px;
  font-size: 13px;
}
.dd-input-addon {
  display: inline-flex;
  align-items: center;
  padding: 0 8px;
  background: #fafafa;
  border: 1px solid #d9d9d9;
  border-left: none;
  color: #8c8c8c;
  font-size: 12px;
}

/* ── Permission Table ── */
.dd-empty-state {
  text-align: center;
  color: #8c8c8c;
  padding: 24px 0;
  font-size: 13px;
}
.dd-perm-table {
  border: 1px solid #f0f0f0;
  border-radius: 4px;
}
.dd-perm-header {
  display: flex;
  background: #fafafa;
  border-bottom: 1px solid #f0f0f0;
  padding: 8px 12px;
  font-weight: 500;
  font-size: 12px;
  color: #595959;
}
.dd-perm-row {
  display: flex;
  align-items: center;
  padding: 8px 12px;
  border-bottom: 1px solid #f0f0f0;
  font-size: 13px;
}
.dd-perm-row:last-child {
  border-bottom: none;
}
.dd-perm-col.name {
  flex: 1;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  margin-right: 8px;
}
.dd-perm-col.perm {
  flex-shrink: 0;
}

.dd-switch-hint {
  display: inline-block;
  margin-left: 8px;
  font-size: 12px;
  color: #8c8c8c;
}
.dd-empty-rule {
  padding: 8px 0;
}
.dd-section-title {
  font-weight: 500;
  margin-bottom: 12px;
  font-size: 14px;
}

/* ── Footer ── */
.dd-props-footer {
  padding: 12px 16px;
  border-top: 1px solid #f0f0f0;
  flex-shrink: 0;
}
</style>