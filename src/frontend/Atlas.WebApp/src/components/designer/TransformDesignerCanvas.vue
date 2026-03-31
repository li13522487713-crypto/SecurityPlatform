<template>
  <div class="transform-designer-canvas">
    <a-alert type="info" show-icon :message="t('dynamicDesigner.transformHint')" />
    <a-card size="small" :title="t('dynamicDesigner.transformDesign')" style="margin-top: 8px">
      <template #extra>
        <a-space>
          <a-button type="primary" @click="openCreate">{{ t("common.create", "新增") }}</a-button>
          <a-button @click="loadJobs">{{ t("common.refresh", "刷新") }}</a-button>
        </a-space>
      </template>

      <a-table :data-source="jobs" :columns="columns" row-key="id" size="small" :pagination="false">
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'enabled'">
            <a-tag :color="record.enabled ? 'green' : 'default'">
              {{ record.enabled ? t("common.statusEnabled", "启用") : t("common.statusDisabled", "停用") }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'lastRun'">
            <div>{{ record.lastRunAt ?? "-" }}</div>
            <a-tag v-if="record.lastRunStatus" :color="statusColor(record.lastRunStatus)" size="small">
              {{ record.lastRunStatus }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'actions'">
            <a-space :size="8">
              <a-button type="link" size="small" @click="openEdit(record.jobKey)">{{ t("common.edit", "编辑") }}</a-button>
              <a-button type="link" size="small" @click="runJob(record.jobKey)">运行</a-button>
              <a-button v-if="record.enabled" type="link" size="small" @click="pauseJob(record.jobKey)">暂停</a-button>
              <a-button v-else type="link" size="small" @click="resumeJob(record.jobKey)">恢复</a-button>
              <a-button type="link" size="small" @click="openHistory(record.jobKey)">历史</a-button>
              <a-popconfirm :title="t('common.delete', '删除')" @confirm="removeJob(record.jobKey)">
                <a-button type="link" danger size="small">{{ t("common.delete", "删除") }}</a-button>
              </a-popconfirm>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <a-drawer v-model:open="editorOpen" :title="editorTitle" width="720" @close="resetEditor">
      <a-form layout="vertical">
        <a-form-item label="jobKey">
          <a-input v-model:value="form.jobKey" :disabled="editMode" />
        </a-form-item>
        <a-form-item :label="t('common.name', '名称')">
          <a-input v-model:value="form.name" />
        </a-form-item>
        <a-form-item label="Cron">
          <a-input v-model:value="form.cronExpression" placeholder="0 */5 * * *" />
        </a-form-item>
        <a-form-item :label="t('common.statusEnabled', '启用')">
          <a-switch v-model:checked="form.enabled" />
        </a-form-item>
        <a-form-item label="Source Config JSON">
          <a-textarea v-model:value="form.sourceConfigJson" :rows="4" />
        </a-form-item>
        <a-form-item label="Target Config JSON">
          <a-textarea v-model:value="form.targetConfigJson" :rows="4" />
        </a-form-item>
        <a-form-item label="Definition JSON">
          <a-textarea v-model:value="form.definitionJson" :rows="6" />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-space>
          <a-button @click="editorOpen = false">{{ t("common.cancel", "取消") }}</a-button>
          <a-button type="primary" @click="saveJob">{{ t("common.save", "保存") }}</a-button>
        </a-space>
      </template>
    </a-drawer>

    <a-modal v-model:open="historyOpen" :title="historyTitle" :footer="null" width="980">
      <a-table :data-source="historyRows" :columns="historyColumns" row-key="id" size="small" :pagination="false">
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'actions'">
            <a-button type="link" size="small" @click="openExecution(record.id)">详情</a-button>
          </template>
        </template>
      </a-table>
    </a-modal>

    <a-modal v-model:open="executionOpen" :title="t('dynamicDesigner.executionDetail', '执行详情')" :footer="null" width="820">
      <a-descriptions bordered :column="1" size="small">
        <a-descriptions-item label="ID">{{ executionDetail?.id }}</a-descriptions-item>
        <a-descriptions-item :label="t('common.status', '状态')">{{ executionDetail?.status }}</a-descriptions-item>
        <a-descriptions-item :label="t('dynamicDesigner.triggerType', '触发方式')">{{ executionDetail?.triggerType }}</a-descriptions-item>
        <a-descriptions-item :label="t('dynamicDesigner.inputRows', '输入行数')">{{ executionDetail?.inputRows }}</a-descriptions-item>
        <a-descriptions-item :label="t('dynamicDesigner.outputRows', '输出行数')">{{ executionDetail?.outputRows }}</a-descriptions-item>
        <a-descriptions-item :label="t('dynamicDesigner.failedRows', '失败行数')">{{ executionDetail?.failedRows }}</a-descriptions-item>
        <a-descriptions-item :label="t('dynamicDesigner.duration', '耗时(ms)')">{{ executionDetail?.durationMs }}</a-descriptions-item>
        <a-descriptions-item :label="t('common.description', '说明')">{{ executionDetail?.message }}</a-descriptions-item>
        <a-descriptions-item label="Error Json">{{ executionDetail?.errorDetailJson ?? "-" }}</a-descriptions-item>
      </a-descriptions>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from "vue";
import { message } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import {
  createDynamicTransformJob,
  deleteDynamicTransformJob,
  getDynamicTransformExecution,
  getDynamicTransformJob,
  listDynamicTransformJobHistory,
  listDynamicTransformJobs,
  pauseDynamicTransformJob,
  resumeDynamicTransformJob,
  runDynamicTransformJob,
  updateDynamicTransformJob
} from "@/services/dynamic-views";
import type { DynamicTransformExecutionDto, DynamicTransformJobDto } from "@/types/dynamic-dataflow";
import { getCurrentAppIdFromStorage } from "@/utils/app-context";

const { t } = useI18n();
const jobs = ref<DynamicTransformJobDto[]>([]);
const historyRows = ref<DynamicTransformExecutionDto[]>([]);
const historyOpen = ref(false);
const historyTitle = ref(t("dynamicDesigner.history", "历史版本"));
const historyJobKey = ref("");
const editorOpen = ref(false);
const editMode = ref(false);
const executionOpen = ref(false);
const executionDetail = ref<DynamicTransformExecutionDto | null>(null);

const form = reactive({
  jobKey: "",
  name: "",
  cronExpression: "",
  enabled: false,
  sourceConfigJson: "{\"type\":\"view\",\"viewKey\":\"\"}",
  targetConfigJson: "{\"type\":\"table\",\"tableKey\":\"\"}",
  definitionJson: "{}"
});

const columns = computed(() => [
  { title: "jobKey", dataIndex: "jobKey", key: "jobKey", width: 180 },
  { title: t("common.name", "名称"), dataIndex: "name", key: "name", width: 180 },
  { title: "Cron", dataIndex: "cronExpression", key: "cronExpression", width: 200 },
  { title: t("common.status", "状态"), dataIndex: "status", key: "status", width: 120 },
  { title: t("dynamicDesigner.enabled", "启用"), key: "enabled", width: 100 },
  { title: t("dynamicDesigner.lastRun", "最近执行"), key: "lastRun", width: 220 },
  { title: t("common.actions"), key: "actions", width: 340 }
]);

const historyColumns = computed(() => [
  { title: "ID", dataIndex: "id", key: "id", width: 180 },
  { title: t("common.status", "状态"), dataIndex: "status", key: "status", width: 130 },
  { title: t("dynamicDesigner.triggerType", "触发方式"), dataIndex: "triggerType", key: "triggerType", width: 120 },
  { title: t("dynamicDesigner.inputRows", "输入"), dataIndex: "inputRows", key: "inputRows", width: 90 },
  { title: t("dynamicDesigner.outputRows", "输出"), dataIndex: "outputRows", key: "outputRows", width: 90 },
  { title: t("dynamicDesigner.failedRows", "失败"), dataIndex: "failedRows", key: "failedRows", width: 90 },
  { title: t("dynamicDesigner.duration", "耗时(ms)"), dataIndex: "durationMs", key: "durationMs", width: 100 },
  { title: "StartedAt", dataIndex: "startedAt", key: "startedAt" },
  { title: t("common.actions"), key: "actions", width: 80 }
]);

const editorTitle = computed(() => (editMode.value ? t("common.edit", "编辑") : t("common.create", "新增")));

onMounted(() => {
  void loadJobs();
});

async function loadJobs() {
  jobs.value = await listDynamicTransformJobs();
}

function openCreate() {
  editMode.value = false;
  editorOpen.value = true;
}

async function openEdit(jobKey: string) {
  const detail = await getDynamicTransformJob(jobKey);
  if (!detail) {
    message.error(t("crud.loadDetailFailed", "加载详情失败"));
    return;
  }

  editMode.value = true;
  form.jobKey = detail.jobKey;
  form.name = detail.name;
  form.cronExpression = detail.cronExpression ?? "";
  form.enabled = detail.enabled;
  form.sourceConfigJson = detail.sourceConfigJson;
  form.targetConfigJson = detail.targetConfigJson;
  form.definitionJson = detail.definitionJson;
  editorOpen.value = true;
}

function resetEditor() {
  form.jobKey = "";
  form.name = "";
  form.cronExpression = "";
  form.enabled = false;
  form.sourceConfigJson = "{\"type\":\"view\",\"viewKey\":\"\"}";
  form.targetConfigJson = "{\"type\":\"table\",\"tableKey\":\"\"}";
  form.definitionJson = "{}";
}

async function saveJob() {
  const appId = getCurrentAppIdFromStorage();
  if (!appId) {
    message.warning(t("dynamic.selectAppFirst", "请先选择应用"));
    return;
  }
  if (!form.jobKey.trim() || !form.name.trim()) {
    message.warning(t("validation.required", "请完善必填项"));
    return;
  }

  if (!editMode.value) {
    await createDynamicTransformJob({
      appId,
      jobKey: form.jobKey.trim(),
      name: form.name.trim(),
      definitionJson: form.definitionJson,
      cronExpression: form.cronExpression.trim() || null,
      enabled: form.enabled,
      sourceConfigJson: form.sourceConfigJson,
      targetConfigJson: form.targetConfigJson
    });
  } else {
    await updateDynamicTransformJob(form.jobKey.trim(), {
      name: form.name.trim(),
      definitionJson: form.definitionJson,
      cronExpression: form.cronExpression.trim() || null,
      enabled: form.enabled,
      sourceConfigJson: form.sourceConfigJson,
      targetConfigJson: form.targetConfigJson
    });
  }

  message.success(t("common.save", "保存"));
  editorOpen.value = false;
  resetEditor();
  await loadJobs();
}

async function runJob(jobKey: string) {
  await runDynamicTransformJob(jobKey);
  message.success(t("common.success", "操作成功"));
  await loadJobs();
}

async function pauseJob(jobKey: string) {
  await pauseDynamicTransformJob(jobKey);
  message.success(t("common.success", "操作成功"));
  await loadJobs();
}

async function resumeJob(jobKey: string) {
  await resumeDynamicTransformJob(jobKey);
  message.success(t("common.success", "操作成功"));
  await loadJobs();
}

async function removeJob(jobKey: string) {
  await deleteDynamicTransformJob(jobKey);
  message.success(t("common.deleteSuccess", "删除成功"));
  await loadJobs();
}

async function openHistory(jobKey: string) {
  historyJobKey.value = jobKey;
  historyRows.value = await listDynamicTransformJobHistory(jobKey, 1, 50);
  historyTitle.value = `${jobKey} - ${t("dynamicDesigner.history", "历史版本")}`;
  historyOpen.value = true;
}

async function openExecution(executionId: string) {
  if (!historyJobKey.value) {
    return;
  }
  executionDetail.value = await getDynamicTransformExecution(historyJobKey.value, executionId);
  executionOpen.value = true;
}

function statusColor(status: string) {
  if (status === "Succeeded") {
    return "green";
  }
  if (status === "Failed") {
    return "red";
  }
  if (status === "PartiallySucceeded") {
    return "orange";
  }
  return "blue";
}
</script>

<style scoped>
.transform-designer-canvas {
  min-height: 280px;
}
</style>
