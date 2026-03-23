<template>
  <div class="dd-props-panel" :class="{ 'is-open': open }">
    <!-- 面板头部 -->
    <div v-if="formData" class="dd-props-header">
      <div class="dd-props-header__info">
        <div class="dd-props-header__icon" :class="iconClass">
          <component :is="nodeIcon" />
        </div>
        <a-input
          v-if="'nodeName' in formData"
          v-model:value="formData.nodeName"
          class="dd-props-header__name"
          :bordered="false"
          :placeholder="t('approvalDesigner.propsPhNodeName')"
        />
        <a-input
          v-else-if="'branchName' in formData"
          v-model:value="(formData as BranchForm).branchName"
          class="dd-props-header__name"
          :bordered="false"
          :placeholder="t('approvalDesigner.propsPhBranchName')"
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
      :message="t('approvalDesigner.propsNodeConfigError')"
      style="margin: 0 12px 8px"
      banner
    />

    <!-- 面板内容 -->
    <div v-if="formData" class="dd-props-body">
      <!-- ═══ 审批节点 ═══ -->
      <template v-if="approveForm">
        <a-tabs v-model:active-key="activeTab" size="small" class="dd-props-tabs">
          <!-- Tab 1: 审批设置 -->
          <a-tab-pane key="approver" :tab="t('approvalDesigner.propsTabApprover')">
            <a-form layout="vertical" class="dd-props-form">
              <a-form-item :label="t('approvalDesigner.propsLabelAssigneeType')">
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
                v-if="isPickerAssigneeType(approveForm.assigneeType)"
                :label="t('approvalDesigner.propsLabelPickApprover')"
              >
                <UserRolePicker
                  v-if="approveForm.assigneeType === 0"
                  v-model:value="approverTargets"
                  mode="user"
                  :placeholder="t('approvalDesigner.propsPhPickUser')"
                />
                <UserRolePicker
                  v-else
                  v-model:value="approverTargets"
                  mode="role"
                  :placeholder="t('approvalDesigner.propsPhPickRole')"
                />
              </a-form-item>

              <a-form-item v-else-if="approveForm.assigneeType === 3" :label="t('approvalDesigner.propsLabelEscalationMax')">
                <a-input-number
                  v-model:value="assigneeLevel"
                  :min="1"
                  :max="20"
                  style="width: 100%"
                  :placeholder="t('approvalDesigner.propsPhEscalationMax')"
                />
                <div class="dd-form-hint">{{ t('approvalDesigner.propsHintEscalationLoop') }}</div>
              </a-form-item>

              <a-form-item v-else-if="approveForm.assigneeType === 4" :label="t('approvalDesigner.propsLabelApproveLevel')">
                <a-input-number
                  v-model:value="assigneeLevel"
                  :min="1"
                  :max="20"
                  style="width: 100%"
                  :placeholder="t('approvalDesigner.propsPhApproveLevel')"
                />
                <div class="dd-form-hint">{{ t('approvalDesigner.propsHintApproveLevelFixed') }}</div>
              </a-form-item>

              <template v-else-if="approveForm.assigneeType === 9">
                <a-form-item :label="t('approvalDesigner.propsLabelPersonField')">
                  <a-select
                    v-if="formFields && formFields.length > 0"
                    v-model:value="assigneeExpression"
                    :placeholder="t('approvalDesigner.propsPhPersonFieldSelect')"
                    allow-clear
                    show-search
                  >
                    <a-select-option
                      v-for="field in formFields"
                      :key="field.id || field.fieldId"
                      :value="field.id || field.fieldId"
                    >
                      {{ field.label || field.fieldName }}
                    </a-select-option>
                  </a-select>
                  <a-input
                    v-else
                    v-model:value="assigneeExpression"
                    :placeholder="t('approvalDesigner.propsPhPersonFieldExample')"
                  />
                  <div class="dd-form-hint">{{ t('approvalDesigner.propsHintPersonField') }}</div>
                </a-form-item>
              </template>

              <a-form-item
                v-else-if="approveForm.assigneeType === 10"
                :label="t('approvalDesigner.propsLabelExternalField')"
              >
                <a-input
                  v-model:value="assigneeExpression"
                  :placeholder="t('approvalDesigner.propsPhExternalField')"
                />
                <div class="dd-form-hint">{{ t('approvalDesigner.propsHintExternalField') }}</div>
              </a-form-item>

              <a-alert
                v-else
                type="info"
                show-icon
                :message="getAssigneeTypeHint(approveForm.assigneeType)"
                style="margin-bottom: 16px"
              />

              <a-form-item :label="t('approvalDesigner.propsLabelMultiMode')">
                <div class="dd-radio-cards">
                  <div
                    class="dd-radio-card"
                    :class="{ active: approveForm.approvalMode === 'all' }"
                    @click="approveForm.approvalMode = 'all'"
                  >
                    <div class="dd-radio-card__title">{{ t('approvalDesigner.propsModeAllTitle') }}</div>
                    <div class="dd-radio-card__desc">{{ t('approvalDesigner.propsModeAllDesc') }}</div>
                  </div>
                  <div
                    class="dd-radio-card"
                    :class="{ active: approveForm.approvalMode === 'any' }"
                    @click="approveForm.approvalMode = 'any'"
                  >
                    <div class="dd-radio-card__title">{{ t('approvalDesigner.propsModeAnyTitle') }}</div>
                    <div class="dd-radio-card__desc">{{ t('approvalDesigner.propsModeAnyDesc') }}</div>
                  </div>
                  <div
                    class="dd-radio-card"
                    :class="{ active: approveForm.approvalMode === 'sequential' }"
                    @click="approveForm.approvalMode = 'sequential'"
                  >
                    <div class="dd-radio-card__title">{{ t('approvalDesigner.propsModeSeqTitle') }}</div>
                    <div class="dd-radio-card__desc">{{ t('approvalDesigner.propsModeSeqDesc') }}</div>
                  </div>
                  <div
                    class="dd-radio-card"
                    :class="{ active: approveForm.approvalMode === 'vote' }"
                    @click="approveForm.approvalMode = 'vote'"
                  >
                    <div class="dd-radio-card__title">{{ t('approvalDesigner.propsModeVoteTitle') }}</div>
                    <div class="dd-radio-card__desc">{{ t('approvalDesigner.propsModeVoteDesc') }}</div>
                  </div>
                </div>
              </a-form-item>

              <template v-if="approveForm.approvalMode === 'vote'">
                <a-form-item :label="t('approvalDesigner.propsLabelVoteWeight')">
                  <a-input-number
                    v-model:value="approveForm.voteWeight"
                    :min="1"
                    :max="100"
                    :precision="0"
                    style="width: 100%"
                  />
                </a-form-item>
                <a-form-item :label="t('approvalDesigner.propsLabelVotePassRate')">
                  <a-slider v-model:value="approveForm.votePassRate" :min="1" :max="100" />
                </a-form-item>
              </template>

              <a-form-item :label="t('approvalDesigner.propsLabelNoHeader')">
                <a-select v-model:value="approveForm.noHeaderAction">
                  <a-select-option :value="0">{{ t('approvalDesigner.propsNoHeader0') }}</a-select-option>
                  <a-select-option :value="1">{{ t('approvalDesigner.propsNoHeader1') }}</a-select-option>
                  <a-select-option :value="2">{{ t('approvalDesigner.propsNoHeader2') }}</a-select-option>
                </a-select>
              </a-form-item>

              <a-form-item :label="t('approvalDesigner.propsLabelApproveSelf')">
                <a-select v-model:value="approveForm.approveSelf" :default-value="0">
                  <a-select-option :value="0">{{ t('approvalDesigner.propsApproveSelf0') }}</a-select-option>
                  <a-select-option :value="1">{{ t('approvalDesigner.propsApproveSelf1') }}</a-select-option>
                  <a-select-option :value="2">{{ t('approvalDesigner.propsApproveSelf2') }}</a-select-option>
                  <a-select-option :value="3">{{ t('approvalDesigner.propsApproveSelf3') }}</a-select-option>
                </a-select>
              </a-form-item>
            </a-form>
          </a-tab-pane>

          <!-- Tab 2: 高级设置 -->
          <a-tab-pane key="advanced" :tab="t('approvalDesigner.propsTabAdvanced')">
            <a-form layout="vertical" class="dd-props-form">
              <a-form-item :label="t('approvalDesigner.propsLabelTimeout')">
                <a-switch v-model:checked="approveForm.timeoutEnabled" />
                <span class="dd-switch-label">{{ t('approvalDesigner.propsTimeoutSwitch') }}</span>
              </a-form-item>
              
              <template v-if="approveForm.timeoutEnabled">
                <a-form-item :label="t('approvalDesigner.propsLabelTimeoutDuration')">
                  <a-input-group compact>
                    <a-input-number v-model:value="approveForm.timeoutHours" :min="0" style="width: 80px" />
                    <span class="dd-input-addon">{{ t('approvalDesigner.propsUnitHour') }}</span>
                    <a-input-number v-model:value="approveForm.timeoutMinutes" :min="0" :max="59" style="width: 80px" />
                    <span class="dd-input-addon">{{ t('approvalDesigner.propsUnitMinute') }}</span>
                  </a-input-group>
                </a-form-item>
                
                <a-form-item :label="t('approvalDesigner.propsLabelTimeoutAction')">
                  <a-select v-model:value="approveForm.timeoutAction">
                    <a-select-option value="none">{{ t('approvalDesigner.propsTimeoutNone') }}</a-select-option>
                    <a-select-option value="autoApprove">{{ t('approvalDesigner.propsTimeoutAutoApprove') }}</a-select-option>
                    <a-select-option value="autoReject">{{ t('approvalDesigner.propsTimeoutAutoReject') }}</a-select-option>
                    <a-select-option value="autoSkip">{{ t('approvalDesigner.propsTimeoutAutoSkip') }}</a-select-option>
                  </a-select>
                </a-form-item>
              </template>

              <a-divider />

              <a-form-item :label="t('approvalDesigner.propsLabelDeduplication')">
                <a-select v-model:value="approveForm.deduplicationType">
                  <a-select-option value="none">{{ t('approvalDesigner.propsDedupNone') }}</a-select-option>
                  <a-select-option value="skipSame">{{ t('approvalDesigner.propsDedupSkipSame') }}</a-select-option>
                  <a-select-option value="global">{{ t('approvalDesigner.propsDedupGlobal') }}</a-select-option>
                </a-select>
              </a-form-item>

              <a-form-item :label="t('approvalDesigner.propsLabelRejectStrategy')">
                <a-select v-model:value="approveForm.rejectStrategy">
                  <a-select-option value="toPrevious">{{ t('approvalDesigner.propsRejectToPrevious') }}</a-select-option>
                  <a-select-option value="toInitiator">{{ t('approvalDesigner.propsRejectToInitiator') }}</a-select-option>
                  <a-select-option value="toAnyNode">{{ t('approvalDesigner.propsRejectToAny') }}</a-select-option>
                  <a-select-option value="terminateApproval">{{ t('approvalDesigner.propsRejectTerminate') }}</a-select-option>
                  <a-select-option value="toParentNode">{{ t('approvalDesigner.propsRejectToParent') }}</a-select-option>
                </a-select>
              </a-form-item>

              <a-form-item :label="t('approvalDesigner.propsLabelReapprove')">
                <a-select v-model:value="approveForm.reApproveStrategy">
                  <a-select-option value="continue">{{ t('approvalDesigner.propsReapproveContinue') }}</a-select-option>
                  <a-select-option value="backToRejectNode">{{ t('approvalDesigner.propsReapproveBackToReject') }}</a-select-option>
                </a-select>
              </a-form-item>

              <a-form-item :label="t('approvalDesigner.propsLabelGroupStrategy')">
                <a-select v-model:value="approveForm.groupStrategy">
                  <a-select-option value="claim">{{ t('approvalDesigner.propsGroupClaim') }}</a-select-option>
                  <a-select-option value="allParticipate">{{ t('approvalDesigner.propsGroupAll') }}</a-select-option>
                </a-select>
              </a-form-item>

              <a-divider />

              <a-form-item :label="t('approvalDesigner.propsLabelAi')">
                <a-switch v-model:checked="approveForm.callAi" />
                <span class="dd-switch-label">{{ t('approvalDesigner.propsAiSwitch') }}</span>
              </a-form-item>

              <template v-if="approveForm.callAi">
                <a-form-item :label="t('approvalDesigner.propsLabelAiJson')">
                  <a-textarea 
                    :value="approveForm.aiConfig"
                    :rows="4"
                    :placeholder="t('approvalDesigner.propsAiJsonPh')" 
                    @update:value="(val: string) => { if (approveForm) approveForm.aiConfig = val }"
                  />
                </a-form-item>
              </template>
            </a-form>
          </a-tab-pane>

          <!-- Tab 3: 表单权限 -->
          <a-tab-pane key="form-perm" :tab="t('approvalDesigner.propsTabFormPerm')">
            <div v-if="!formFields || formFields.length === 0" class="dd-empty-state">
              {{ t('approvalDesigner.propsEmptyFormFields') }}
            </div>
            <div v-else class="dd-perm-table">
              <div class="dd-perm-header">
                <div class="dd-perm-col name">{{ t('approvalDesigner.propsPermColName') }}</div>
                <div class="dd-perm-col perm">{{ t('approvalDesigner.propsPermColPerm') }}</div>
              </div>
              <div v-for="field in formFields" :key="field.id || field.fieldId" class="dd-perm-row">
                <div class="dd-perm-col name">{{ field.label || field.fieldName }}</div>
                <div class="dd-perm-col perm">
                  <a-radio-group 
                    v-model:value="formPermMap[field.id || field.fieldId]" 
                    size="small"
                    button-style="solid"
                  >
                    <a-radio-button value="E">{{ t('approvalDesigner.propsPermEdit') }}</a-radio-button>
                    <a-radio-button value="R">{{ t('approvalDesigner.propsPermRead') }}</a-radio-button>
                    <a-radio-button value="H">{{ t('approvalDesigner.propsPermHide') }}</a-radio-button>
                  </a-radio-group>
                </div>
              </div>
            </div>
          </a-tab-pane>

          <!-- Tab 4: 通知设置 -->
          <a-tab-pane key="notice" :tab="t('approvalDesigner.propsTabNotice')">
             <a-form layout="vertical" class="dd-props-form">
              <a-form-item :label="t('approvalDesigner.propsLabelNoticeChannels')">
                <a-checkbox-group v-model:value="noticeChannels">
                  <a-row>
                    <a-col :span="12"><a-checkbox :value="1">{{ t('approvalDesigner.propsChannelInbox') }}</a-checkbox></a-col>
                    <a-col :span="12"><a-checkbox :value="2">{{ t('approvalDesigner.propsChannelEmail') }}</a-checkbox></a-col>
                    <a-col :span="12"><a-checkbox :value="3">{{ t('approvalDesigner.propsChannelSms') }}</a-checkbox></a-col>
                    <a-col :span="12"><a-checkbox :value="4">{{ t('approvalDesigner.propsChannelWecom') }}</a-checkbox></a-col>
                    <a-col :span="12"><a-checkbox :value="5">{{ t('approvalDesigner.propsChannelDing') }}</a-checkbox></a-col>
                  </a-row>
                </a-checkbox-group>
              </a-form-item>
              <a-form-item :label="t('approvalDesigner.propsLabelNoticeTemplate')">
                <a-select v-model:value="noticeTemplateId" :placeholder="t('approvalDesigner.propsPhNoticeTemplate')">
                  <a-select-option value="default">{{ t('approvalDesigner.propsTemplateDefault') }}</a-select-option>
                  <a-select-option value="urgent">{{ t('approvalDesigner.propsTemplateUrgent') }}</a-select-option>
                </a-select>
              </a-form-item>
            </a-form>
          </a-tab-pane>
        </a-tabs>
      </template>

      <!-- ═══ 抄送节点 ═══ -->
      <template v-else-if="copyForm">
        <a-form layout="vertical" class="dd-props-form">
          <a-form-item :label="t('approvalDesigner.propsCopyRecipients')">
            <UserRolePicker
              v-model:value="copyForm.copyToUsers"
              mode="user"
              :placeholder="t('approvalDesigner.propsPhCopyRecipients')"
            />
          </a-form-item>
          <a-divider />
          <div class="dd-section-title">{{ t('approvalDesigner.propsSectionFormPerm') }}</div>
          <div v-if="!formFields || formFields.length === 0" class="dd-empty-state">
            {{ t('approvalDesigner.propsEmptyFormFields') }}
          </div>
          <div v-else class="dd-perm-table">
             <div class="dd-perm-header">
                <div class="dd-perm-col name">{{ t('approvalDesigner.propsPermColName') }}</div>
                <div class="dd-perm-col perm">{{ t('approvalDesigner.propsPermColPerm') }}</div>
              </div>
              <div v-for="field in formFields" :key="field.id || field.fieldId" class="dd-perm-row">
                <div class="dd-perm-col name">{{ field.label || field.fieldName }}</div>
                <div class="dd-perm-col perm">
                  <a-radio-group 
                    v-model:value="formPermMap[field.id || field.fieldId]" 
                    size="small"
                    button-style="solid"
                  >
                    <a-radio-button value="R">{{ t('approvalDesigner.propsPermRead') }}</a-radio-button>
                    <a-radio-button value="H">{{ t('approvalDesigner.propsPermHide') }}</a-radio-button>
                  </a-radio-group>
                </div>
              </div>
          </div>
        </a-form>
      </template>

      <!-- ═══ 条件分支 ═══ -->
      <template v-else-if="branchForm">
        <a-form layout="vertical" class="dd-props-form">
          <a-form-item :label="t('approvalDesigner.propsBranchDefault')">
            <a-switch v-model:checked="branchForm.isDefault" />
            <span v-if="branchForm.isDefault" class="dd-switch-hint">
              {{ t('approvalDesigner.propsBranchDefaultHint') }}
            </span>
          </a-form-item>

          <template v-if="!branchForm.isDefault">
            <a-divider>{{ t('approvalDesigner.propsDividerConditionRules') }}</a-divider>
            <a-form-item :label="t('approvalDesigner.propsLabelCelExpr')">
              <ExpressionEditorCel
                :model-value="branchForm.conditionExpr ?? ''"
                @update:model-value="(v: string) => { if (branchForm) branchForm.conditionExpr = v }"
                @validate="onExpressionValidate"
              />
            </a-form-item>
            <ConditionGroupEditor 
              :model-value="branchForm.conditionGroups ?? []"
              :form-fields="formFields"
              @update:model-value="(v: ConditionGroup[]) => { if (branchForm) branchForm.conditionGroups = v }" 
            />
          </template>
        </a-form>
      </template>

      <!-- ═══ 包容分支 ═══ -->
      <template v-else-if="inclusiveForm">
        <a-form layout="vertical" class="dd-props-form">
          <a-alert :message="t('approvalDesigner.propsInclusiveAlert')" type="info" show-icon style="margin-bottom: 16px" />
          <a-divider>{{ t('approvalDesigner.propsDividerConditionRules') }}</a-divider>
          <div class="dd-empty-state">
            {{ t('approvalDesigner.propsInclusiveHint') }}
          </div>
        </a-form>
      </template>

      <!-- ═══ 路由分支 ═══ -->
      <template v-else-if="routeForm">
        <a-form layout="vertical" class="dd-props-form">
          <a-form-item :label="t('approvalDesigner.propsRouteTargetId')">
            <a-input v-model:value="routeForm.routeTargetNodeId" :placeholder="t('approvalDesigner.propsPhRouteTargetId')" />
          </a-form-item>
        </a-form>
      </template>

      <!-- ═══ 子流程节点 ═══ -->
      <template v-else-if="callProcessForm">
        <a-form layout="vertical" class="dd-props-form">
          <a-form-item :label="t('approvalDesigner.propsSubflowDefId')">
            <a-input v-model:value="callProcessForm.callProcessId" :placeholder="t('approvalDesigner.propsPhSubflowDefId')" />
          </a-form-item>
          <a-form-item :label="t('approvalDesigner.propsCallExecMode')">
            <a-radio-group v-model:value="callProcessForm.callAsync">
              <a-radio :value="false">{{ t('approvalDesigner.propsCallSync') }}</a-radio>
              <a-radio :value="true">{{ t('approvalDesigner.propsCallAsync') }}</a-radio>
            </a-radio-group>
          </a-form-item>
        </a-form>
      </template>

      <!-- ═══ 定时器节点 ═══ -->
      <template v-else-if="timerForm && timerForm.timerConfig">
        <a-form layout="vertical" class="dd-props-form">
          <a-form-item :label="t('approvalDesigner.propsTimerType')">
            <a-select v-model:value="timerForm.timerConfig.type">
              <a-select-option value="duration">{{ t('approvalDesigner.propsTimerDurationOpt') }}</a-select-option>
              <a-select-option value="date">{{ t('approvalDesigner.propsTimerDateOpt') }}</a-select-option>
            </a-select>
          </a-form-item>
          <a-form-item v-if="timerForm.timerConfig.type === 'duration'" :label="t('approvalDesigner.propsLabelWaitSeconds')">
            <a-input-number v-model:value="timerForm.timerConfig.duration" :min="0" style="width: 100%" />
          </a-form-item>
          <a-form-item v-if="timerForm.timerConfig.type === 'date'" :label="t('approvalDesigner.propsLabelSpecifiedTime')">
            <a-date-picker v-model:value="timerForm.timerConfig.date" show-time value-format="YYYY-MM-DD HH:mm:ss" style="width: 100%" />
          </a-form-item>
        </a-form>
      </template>

      <!-- ═══ 触发器节点 ═══ -->
      <template v-else-if="triggerForm">
        <a-form layout="vertical" class="dd-props-form">
          <a-form-item :label="t('approvalDesigner.propsTriggerType')">
            <a-select v-model:value="triggerForm.triggerType">
              <a-select-option value="immediate">{{ t('approvalDesigner.propsTriggerImmediate') }}</a-select-option>
              <a-select-option value="scheduled">{{ t('approvalDesigner.propsTriggerScheduled') }}</a-select-option>
            </a-select>
          </a-form-item>
          <a-form-item :label="t('approvalDesigner.propsTriggerConfigJson')">
            <a-textarea 
              :value="JSON.stringify(triggerForm.triggerConfig, null, 2)"
              :rows="4"
              @update:value="(val: string) => applyJsonConfig(val, t('approvalDesigner.propsTriggerConfigJson'), (v) => { if (triggerForm) triggerForm.triggerConfig = v as TriggerNode['triggerConfig'] })" 
            />
          </a-form-item>
        </a-form>
      </template>

      <!-- ═══ 发起人节点 ═══ -->
      <template v-else-if="startForm">
        <a-form layout="vertical" class="dd-props-form">
          <a-form-item :label="t('approvalDesigner.propsStartCondition')">
            <a-alert :message="t('approvalDesigner.propsStartEveryone')" type="info" show-icon />
          </a-form-item>
        </a-form>
      </template>
    </div>

    <!-- 底部按钮 -->
    <div v-if="formData" class="dd-props-footer">
      <a-button type="primary" block @click="handleSave">{{ t('approvalDesigner.propsFooterOk') }}</a-button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, computed } from 'vue';
import { useI18n } from 'vue-i18n';
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
import { ASSIGNEE_TYPE_OPTIONS } from '@/constants/approval';

const { t } = useI18n();
import {
  isApproveNode,
  isCopyNode,
  isStartNode,
  isConditionBranch,
  isInclusiveNode,
  isRouteNode,
  isCallProcessNode,
  isTimerNode,
  isTriggerNode
} from '@/utils/workflow-node-guards';
import { useNodeFormSync } from '@/composables/useNodeFormSync';

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
const formData = ref<TreeNode | ConditionBranch | null>(null);
const approveForm = ref<ApproveNode | null>(null);
const copyForm = ref<CopyNode | null>(null);
const branchForm = ref<BranchForm | null>(null);
const inclusiveForm = ref<InclusiveNode | null>(null);
const routeForm = ref<RouteNode | null>(null);
const callProcessForm = ref<CallProcessNode | null>(null);
const timerForm = ref<TimerNode | null>(null);
const triggerForm = ref<TriggerNode | null>(null);
const startForm = ref<StartNode | null>(null);
const activeTab = ref('approver');

const approverTargets = ref<string[]>([]);
const assigneeExpression = ref('');
const assigneeLevel = ref<number | null>(null);
// const noticeConfigText = ref('');
const noticeChannels = ref<number[]>([]);
const noticeTemplateId = ref<string | undefined>(undefined);
const formPermMap = ref<Record<string, 'R' | 'E' | 'H'>>({});
const expressionValid = ref(true);

function assigneeTypeLabel(v: ApproveNode['assigneeType']): string {
  switch (v) {
    case 0:
      return t('approvalDesigner.assigneeUser');
    case 1:
      return t('approvalDesigner.assigneeRole');
    case 2:
      return t('approvalDesigner.assigneeDeptLeader');
    case 3:
      return t('approvalDesigner.assigneeOptLoop');
    case 4:
      return t('approvalDesigner.assigneeOptLevel');
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
      return String(v);
  }
}

const assigneeTypeOptions = computed(() =>
  ASSIGNEE_TYPE_OPTIONS.map((o) => ({ value: o.value, label: assigneeTypeLabel(o.value) })),
);

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

// ── useNodeFormSync composable ──
const { syncNodeRefs, clearRefs, isPickerAssigneeType } = useNodeFormSync(
  {
    formData, approveForm, copyForm, branchForm, inclusiveForm, routeForm,
    callProcessForm, timerForm, triggerForm, startForm, activeTab,
    approverTargets, assigneeExpression, assigneeLevel,
    noticeChannels, noticeTemplateId, formPermMap
  },
  () => props.formFields
);

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
      message.warning(t('approvalDesigner.propsWarnFixCel'));
      return;
    }
    const branch = formData.value as ConditionBranch;
    branch.branchName = branchForm.value.branchName;
    branch.isDefault = branchForm.value.isDefault;
    // 保存 conditionGroups
    branch.conditionGroups = (branchForm.value.conditionGroups ?? []) as ConditionGroup[];
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

  if (!formData.value) return;
  emit('update', formData.value as TreeNode | ConditionBranch);
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
    message.error(t('approvalDesigner.propsJsonInvalid', { label }));
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


function getAssigneeTypeHint(type: ApproveNode['assigneeType']): string {
  switch (type) {
    case 2:
      return t('approvalDesigner.propsHintAssignee2');
    case 3:
      return t('approvalDesigner.propsHintAssignee3');
    case 5:
      return t('approvalDesigner.propsHintAssignee5');
    case 6:
      return t('approvalDesigner.propsHintAssignee6');
    case 7:
      return t('approvalDesigner.propsHintAssignee7');
    case 8:
      return t('approvalDesigner.propsHintAssignee8');
    default:
      return t('approvalDesigner.propsHintAssigneeDefault');
  }
}

// ── 类型守卫（来自 @/utils/workflow-node-guards）──
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
.dd-form-hint {
  font-size: 12px;
  color: #8c8c8c;
  margin-top: 4px;
  line-height: 1.5;
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
