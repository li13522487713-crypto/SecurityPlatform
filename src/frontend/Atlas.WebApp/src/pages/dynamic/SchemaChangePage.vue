<template>
  <div class="schema-change-page">
    <!-- 顶部页头 -->
    <div class="page-header">
      <div class="header-left">
        <a-button type="text" @click="goBack">
          <template #icon><ArrowLeftOutlined /></template>
        </a-button>
        <span class="page-title">{{ t("schemaChange.pageTitle") }}</span>
        <a-tag v-if="tableKey" color="blue">{{ tableKey }}</a-tag>
      </div>
      <div class="header-actions">
        <a-button :loading="validating" @click="handleValidateAll">
          <template #icon><CheckCircleOutlined /></template>
          {{ t("schemaChange.validateAll") }}
        </a-button>
        <a-popconfirm :title="t('schemaChange.confirmPublish')" @confirm="handlePublishAll">
          <a-button type="primary" :loading="publishing" :disabled="validatedDraftCount === 0">
            <template #icon><CloudUploadOutlined /></template>
            {{ t("schemaChange.publishAll") }}
            <span v-if="validatedDraftCount > 0"> ({{ validatedDraftCount }})</span>
          </a-button>
        </a-popconfirm>
      </div>
    </div>

    <div class="page-body">
      <!-- 草稿列表 -->
      <div class="section">
        <div class="section-header">
          <span class="section-title">{{ t("schemaChange.draftSection") }}</span>
          <a-tag>{{ draftList.length }}</a-tag>
        </div>
        <a-spin :spinning="draftLoading">
          <a-empty v-if="!draftLoading && draftList.length === 0" :description="t('schemaChange.noDrafts')" style="padding: 24px" :image="false" />
          <a-table
            v-else
            :dataSource="draftList"
            :columns="draftColumns"
            row-key="id"
            :pagination="false"
            size="small"
            class="change-table"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'changeType'">
                <a-tag :color="changeTypeColor(record.changeType)">{{ record.changeType }}</a-tag>
              </template>
              <template v-else-if="column.key === 'riskLevel'">
                <a-tag :color="riskColor(record.riskLevel)">{{ riskLabel(record.riskLevel) }}</a-tag>
              </template>
              <template v-else-if="column.key === 'status'">
                <a-tag :color="draftStatusColor(record.status)">{{ draftStatusLabel(record.status) }}</a-tag>
              </template>
              <template v-else-if="column.key === 'diff'">
                <a-button type="link" size="small" @click="openDiff(record)">{{ t("schemaChange.diffTitle") }}</a-button>
              </template>
              <template v-else-if="column.key === 'actions'">
                <a-space>
                  <a-button type="link" size="small" :loading="validatingId === record.id" @click="handleValidateDraft(record.id)">
                    {{ t("fieldDesign.validateDraft") }}
                  </a-button>
                  <a-popconfirm :title="t('fieldDesign.abandonDraft')" @confirm="handleAbandonDraft(record.id)">
                    <a-button type="link" danger size="small">{{ t("fieldDesign.abandonDraft") }}</a-button>
                  </a-popconfirm>
                </a-space>
              </template>
            </template>
          </a-table>
        </a-spin>
      </div>

      <!-- 变更任务 -->
      <div class="section" style="margin-top: 24px">
        <div class="section-header">
          <span class="section-title">{{ t("schemaChange.taskSection") }}</span>
          <a-tag>{{ taskList.length }}</a-tag>
        </div>
        <a-spin :spinning="taskLoading">
          <a-empty v-if="!taskLoading && taskList.length === 0" :description="t('schemaChange.noTasks')" style="padding: 24px" :image="false" />
          <div v-else>
            <div v-for="task in taskList" :key="task.id" class="task-card">
              <div class="task-card-header">
                <div class="task-card-left">
                  <a-tag :color="taskStateColor(task.currentState)">{{ taskStateLabel(task.currentState) }}</a-tag>
                  <a-badge v-if="task.isHighRisk" color="red" :text="t('schemaChange.highRiskBadge')" />
                  <span class="task-id">{{ task.id.slice(0, 8) }}...</span>
                </div>
                <div class="task-card-right">
                  <span class="task-time">{{ formatTime(task.createdAt) }}</span>
                  <a-popconfirm
                    v-if="task.currentState === 'Pending' || task.currentState === 'WaitingApproval'"
                    :title="t('schemaChange.confirmCancel')"
                    @confirm="handleCancelTask(task.id)"
                  >
                    <a-button size="small" danger>{{ t("schemaChange.cancelTask") }}</a-button>
                  </a-popconfirm>
                </div>
              </div>
              <div v-if="task.errorMessage" class="task-error">{{ task.errorMessage }}</div>
              <a-steps
                v-if="stepsForTask(task).length > 0"
                :current="currentStepIndex(task)"
                :status="task.currentState === 'Failed' ? 'error' : 'process'"
                size="small"
                style="margin-top: 12px"
              >
                <a-step
                  v-for="step in stepsForTask(task)"
                  :key="step.state"
                  :title="step.label"
                />
              </a-steps>
              <div v-if="task.affectedResourcesSummary" class="task-affected">
                <span style="color: #8c8c8c; font-size: 12px">{{ t("schemaChange.affectedResources") }}：</span>
                <span style="font-size: 12px">{{ task.affectedResourcesSummary }}</span>
              </div>
            </div>
          </div>
        </a-spin>
      </div>
    </div>

    <!-- 差异对比弹窗 -->
    <a-modal v-model:open="diffModalOpen" :title="t('schemaChange.diffTitle')" width="700px" :footer="null">
      <a-row :gutter="16" v-if="diffDraft">
        <a-col :span="12">
          <div class="diff-label">{{ t("schemaChange.before") }}</div>
          <pre class="diff-content diff-content--before">{{ formatSnapshot(diffDraft.beforeSnapshot) }}</pre>
        </a-col>
        <a-col :span="12">
          <div class="diff-label">{{ t("schemaChange.after") }}</div>
          <pre class="diff-content diff-content--after">{{ formatSnapshot(diffDraft.afterSnapshot) }}</pre>
        </a-col>
      </a-row>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import { useRoute, useRouter } from "vue-router";
import { message } from "ant-design-vue";
import {
  ArrowLeftOutlined,
  CheckCircleOutlined,
  CloudUploadOutlined
} from "@ant-design/icons-vue";
import {
  listSchemaDrafts,
  validateSchemaDraft,
  publishSchemaDrafts,
  abandonSchemaDraft,
  type SchemaDraftListItem
} from "@/services/schema-drafts";
import {
  listSchemaChangeTasks,
  cancelSchemaChangeTask,
  type SchemaChangeTaskListItem,
  type SchemaChangeTaskState
} from "@/services/schema-change-tasks";

const { t } = useI18n();
const route = useRoute();
const router = useRouter();

const appId = computed(() => (typeof route.params.appId === "string" ? route.params.appId : ""));
const tableKey = computed(() => (typeof route.query.tableKey === "string" ? route.query.tableKey : ""));

const draftLoading = ref(false);
const taskLoading = ref(false);
const validating = ref(false);
const publishing = ref(false);
const validatingId = ref<string | null>(null);
const diffModalOpen = ref(false);
const diffDraft = ref<(SchemaDraftListItem & { beforeSnapshot?: unknown; afterSnapshot?: unknown }) | null>(null);

const draftList = ref<SchemaDraftListItem[]>([]);
const taskList = ref<SchemaChangeTaskListItem[]>([]);

const validatedDraftCount = computed(() =>
  draftList.value.filter((d) => d.status === "Validated").length
);

const draftColumns = computed(() => [
  { title: t("schemaChange.objectType"), dataIndex: "objectType", key: "objectType", width: 110 },
  { title: t("schemaChange.objectKey"), dataIndex: "objectKey", key: "objectKey" },
  { title: t("schemaChange.changeType"), key: "changeType", width: 100 },
  { title: t("schemaChange.riskLevel"), key: "riskLevel", width: 100 },
  { title: t("schemaChange.status"), key: "status", width: 100 },
  { title: "", key: "diff", width: 80 },
  { title: t("common.actions"), key: "actions", width: 160 }
]);

const changeTypeColor = (ct: string): string => {
  const map: Record<string, string> = { Create: "green", Update: "gold", Delete: "red" };
  return map[ct] ?? "default";
};

const riskColor = (risk: string): string => {
  const map: Record<string, string> = { Low: "green", Medium: "gold", High: "red" };
  return map[risk] ?? "default";
};

const riskLabel = (risk: string): string => {
  const map: Record<string, string> = {
    Low: t("fieldDesign.riskLow"),
    Medium: t("fieldDesign.riskMedium"),
    High: t("fieldDesign.riskHigh")
  };
  return map[risk] ?? risk;
};

const draftStatusColor = (status: string): string => {
  const map: Record<string, string> = { Pending: "orange", Validated: "blue", Published: "green", Abandoned: "default" };
  return map[status] ?? "default";
};

const draftStatusLabel = (status: string): string => {
  const map: Record<string, string> = {
    Pending: t("fieldDesign.draftStatusPending"),
    Validated: t("fieldDesign.draftStatusValidated"),
    Published: t("fieldDesign.draftStatusPublished"),
    Abandoned: t("fieldDesign.draftStatusAbandoned")
  };
  return map[status] ?? status;
};

const TASK_STEPS: { state: SchemaChangeTaskState; label: string }[] = [
  { state: "Pending", label: "" },
  { state: "Validating", label: "" },
  { state: "WaitingApproval", label: "" },
  { state: "Applying", label: "" },
  { state: "Applied", label: "" }
];

const stepsForTask = (task: SchemaChangeTaskListItem) => {
  return TASK_STEPS.map((s) => ({ state: s.state, label: taskStateLabel(s.state) }));
};

const currentStepIndex = (task: SchemaChangeTaskListItem): number => {
  const terminalStates: SchemaChangeTaskState[] = ["Applied", "Failed", "RolledBack", "Cancelled"];
  if (terminalStates.includes(task.currentState)) {
    return task.currentState === "Applied" ? 4 : TASK_STEPS.findIndex((s) => s.state === task.currentState);
  }
  return TASK_STEPS.findIndex((s) => s.state === task.currentState);
};

const taskStateColor = (state: SchemaChangeTaskState): string => {
  const map: Record<SchemaChangeTaskState, string> = {
    Pending: "default",
    Validating: "processing",
    WaitingApproval: "orange",
    Applying: "processing",
    Applied: "green",
    Failed: "red",
    RolledBack: "red",
    Cancelled: "default"
  };
  return map[state] ?? "default";
};

const taskStateLabel = (state: SchemaChangeTaskState | string): string => {
  const map: Record<string, string> = {
    Pending: t("schemaChange.taskStatePending"),
    Validating: t("schemaChange.taskStateValidating"),
    WaitingApproval: t("schemaChange.taskStateWaitingApproval"),
    Applying: t("schemaChange.taskStateApplying"),
    Applied: t("schemaChange.taskStateApplied"),
    Failed: t("schemaChange.taskStateFailed"),
    RolledBack: t("schemaChange.taskStateRolledBack"),
    Cancelled: t("schemaChange.taskStateCancelled")
  };
  return map[state] ?? state;
};

const formatTime = (ts?: string | null): string => {
  if (!ts) return "";
  return new Date(ts).toLocaleString();
};

const formatSnapshot = (snap: unknown): string => {
  if (!snap) return "(无)";
  try {
    return JSON.stringify(snap, null, 2);
  } catch {
    return String(snap);
  }
};

const loadDrafts = async () => {
  draftLoading.value = true;
  try {
    draftList.value = await listSchemaDrafts(tableKey.value);
  } catch {
    message.error(t("schemaChange.loadFailed"));
  } finally {
    draftLoading.value = false;
  }
};

const loadTasks = async () => {
  taskLoading.value = true;
  try {
    taskList.value = await listSchemaChangeTasks(tableKey.value || undefined);
  } catch {
    // ignore
  } finally {
    taskLoading.value = false;
  }
};

const handleValidateDraft = async (id: string) => {
  validatingId.value = id;
  try {
    const result = await validateSchemaDraft(id);
    if (result.isValid) {
      message.success(t("schemaChange.validateSuccess"));
    } else {
      message.warning(result.messages.join("; ") || t("schemaChange.validateFailed"));
    }
    await loadDrafts();
  } catch (error) {
    message.error((error as Error).message || t("schemaChange.validateFailed"));
  } finally {
    validatingId.value = null;
  }
};

const handleValidateAll = async () => {
  const pending = draftList.value.filter((d) => d.status === "Pending");
  if (pending.length === 0) {
    message.info(t("schemaChange.noDrafts"));
    return;
  }
  validating.value = true;
  try {
    await Promise.all(pending.map((d) => validateSchemaDraft(d.id)));
    message.success(t("schemaChange.validateSuccess"));
    await loadDrafts();
  } catch (error) {
    message.error((error as Error).message || t("schemaChange.validateFailed"));
  } finally {
    validating.value = false;
  }
};

const handlePublishAll = async () => {
  if (!tableKey.value) {
    message.warning("请先通过表页进入");
    return;
  }
  publishing.value = true;
  try {
    const result = await publishSchemaDrafts(tableKey.value);
    if (result.failedCount === 0) {
      message.success(t("schemaChange.publishSuccess"));
    } else {
      message.warning(`${result.publishedCount} 成功，${result.failedCount} 失败`);
    }
    await loadDrafts();
    await loadTasks();
  } catch (error) {
    message.error((error as Error).message || t("schemaChange.publishFailed"));
  } finally {
    publishing.value = false;
  }
};

const handleAbandonDraft = async (id: string) => {
  try {
    await abandonSchemaDraft(id);
    message.success(t("fieldDesign.abandonSuccess"));
    await loadDrafts();
  } catch (error) {
    message.error((error as Error).message || t("fieldDesign.abandonFailed"));
  }
};

const handleCancelTask = async (taskId: string) => {
  try {
    await cancelSchemaChangeTask(taskId);
    message.success(t("schemaChange.cancelSuccess"));
    await loadTasks();
  } catch (error) {
    message.error((error as Error).message || t("schemaChange.cancelFailed"));
  }
};

const openDiff = (draft: SchemaDraftListItem) => {
  diffDraft.value = draft as SchemaDraftListItem & { beforeSnapshot?: unknown; afterSnapshot?: unknown };
  diffModalOpen.value = true;
};

const goBack = () => {
  if (appId.value) {
    void router.push(`/apps/${appId.value}/data`);
  } else {
    void router.back();
  }
};

onMounted(() => {
  void loadDrafts();
  void loadTasks();
});
</script>

<style scoped>
.schema-change-page {
  display: flex;
  flex-direction: column;
  height: calc(100vh - 120px);
  background: #fff;
  border-radius: 8px;
  box-shadow: 0 1px 2px 0 rgba(0,0,0,.03), 0 1px 6px -1px rgba(0,0,0,.02);
  overflow: hidden;
}

.page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 14px 24px;
  border-bottom: 1px solid #f0f0f0;
  background: #fff;
  flex-shrink: 0;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 10px;
}

.page-title {
  font-size: 16px;
  font-weight: 600;
  color: #1f1f1f;
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

.page-body {
  flex: 1;
  overflow-y: auto;
  padding: 24px;
}

.section {
  background: #fff;
}

.section-header {
  display: flex;
  align-items: center;
  gap: 8px;
  margin-bottom: 12px;
}

.section-title {
  font-size: 15px;
  font-weight: 600;
  color: #1f1f1f;
}

.change-table {
  border: 1px solid #f0f0f0;
  border-radius: 6px;
}

.task-card {
  background: #fafafa;
  border: 1px solid #f0f0f0;
  border-radius: 8px;
  padding: 16px 20px;
  margin-bottom: 12px;
  transition: border-color 0.2s;
}

.task-card:hover {
  border-color: #d0e4ff;
}

.task-card-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.task-card-left {
  display: flex;
  align-items: center;
  gap: 8px;
}

.task-card-right {
  display: flex;
  align-items: center;
  gap: 12px;
}

.task-id {
  font-size: 12px;
  color: #8c8c8c;
  font-family: monospace;
}

.task-time {
  font-size: 12px;
  color: #8c8c8c;
}

.task-error {
  margin-top: 8px;
  padding: 6px 10px;
  background: #fff2f0;
  border: 1px solid #ffccc7;
  border-radius: 4px;
  font-size: 12px;
  color: #ff4d4f;
}

.task-affected {
  margin-top: 8px;
}

.diff-label {
  font-size: 13px;
  font-weight: 600;
  color: #595959;
  margin-bottom: 8px;
}

.diff-content {
  padding: 10px;
  border-radius: 6px;
  font-size: 12px;
  font-family: monospace;
  white-space: pre-wrap;
  word-break: break-all;
  min-height: 100px;
  max-height: 400px;
  overflow-y: auto;
}

.diff-content--before {
  background: #fff2f0;
  border: 1px solid #ffccc7;
  color: #ff4d4f;
}

.diff-content--after {
  background: #f6ffed;
  border: 1px solid #b7eb8f;
  color: #52c41a;
}
</style>
