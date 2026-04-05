<template>
  <div class="runtime-executions-page" data-testid="e2e-console-runtime-executions-page">
    <a-card :bordered="false" class="runtime-execution-card">
      <template #title>{{ t("console.runtimeExec.title") }}</template>
      <template #extra>
        <a-space wrap>
          <a-select
            v-model:value="selectedAppId"
            style="width: 220px"
            :options="appFilterOptions"
            allow-clear
            show-search
            :filter-option="false"
            :loading="loadingAppOptions"
            :placeholder="t('console.runtimeExec.phApp')"
            @search="handleSearchAppOptions"
          />
          <a-select
            v-model:value="selectedStatus"
            style="width: 160px"
            :options="statusFilterOptions"
            allow-clear
            :placeholder="t('console.runtimeExec.phStatus')"
          />
          <a-range-picker
            v-model:value="startedAtRange"
            show-time
            value-format="YYYY-MM-DDTHH:mm:ss.SSS[Z]"
          />
          <a-input-search
            v-model:value="keyword"
            allow-clear
            :placeholder="t('console.runtimeExec.phKeyword')"
            style="width: 240px"
            @search="handleSearch"
          />
          <a-button type="primary" @click="handleSearch">{{ t("console.runtimeExec.search") }}</a-button>
          <a-button @click="handleResetFilters">{{ t("console.runtimeExec.reset") }}</a-button>
        </a-space>
      </template>

      <a-table
        row-key="id"
        :loading="loading"
        :columns="columns"
        :data-source="rows"
        :pagination="pagination"
        :row-class-name="resolveExecutionRowClass"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'status'">
            <a-tag :color="resolveStatusColor(record.status)">{{ record.status }}</a-tag>
          </template>
          <template v-else-if="column.key === 'appId'">
            <a-button v-if="record.appId" type="link" size="small" @click="goToApp(record.appId)">
              {{ record.appId }}
            </a-button>
            <span v-else>-</span>
          </template>
          <template v-else-if="column.key === 'releaseId'">
            <a-button v-if="record.releaseId" type="link" size="small" @click="goToRelease(record.releaseId)">
              {{ record.releaseId }}
            </a-button>
            <span v-else>-</span>
          </template>
          <template v-else-if="column.key === 'runtimeContextId'">
            <a-button
              v-if="record.runtimeContextId"
              type="link"
              size="small"
              @click="goToRuntimeContextByRecord(record)"
            >
              {{ record.runtimeContextId }}
            </a-button>
            <span v-else>-</span>
          </template>
          <template v-else-if="column.key === 'startedAt'">
            {{ formatDate(record.startedAt) }}
          </template>
          <template v-else-if="column.key === 'completedAt'">
            {{ formatDate(record.completedAt) }}
          </template>
          <template v-else-if="column.key === 'errorMessage'">
            {{ record.errorMessage || "-" }}
          </template>
          <template v-else-if="column.key === 'actions'">
            <a-space size="small">
              <a-button type="link" size="small" @click="openDetail(record.id)">{{ t("console.runtimeExec.detail") }}</a-button>
              <a-button v-if="canCancel(record.status)" type="link" size="small" @click="cancelExecution(record.id)">
                {{ t("console.runtimeExec.cancel") }}
              </a-button>
              <a-button v-if="canRetry(record.status)" type="link" size="small" @click="retryExecution(record.id)">
                {{ t("console.runtimeExec.retry") }}
              </a-button>
              <a-button v-if="canResume(record.status)" type="link" size="small" @click="resumeExecution(record.id)">
                {{ t("console.runtimeExec.resume") }}
              </a-button>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <a-drawer
      v-model:open="detailVisible"
      :title="t('console.runtimeExec.drawerTitle')"
      width="860"
      :destroy-on-close="true"
      @close="handleDetailClose"
    >
      <a-spin :spinning="detailLoading">
        <a-breadcrumb class="execution-breadcrumb">
          <a-breadcrumb-item>{{ appBreadcrumbLabel }}</a-breadcrumb-item>
          <a-breadcrumb-item>{{ releaseBreadcrumbLabel }}</a-breadcrumb-item>
          <a-breadcrumb-item>{{ runtimeContextBreadcrumbLabel }}</a-breadcrumb-item>
          <a-breadcrumb-item>{{ executionBreadcrumbLabel }}</a-breadcrumb-item>
        </a-breadcrumb>

        <a-descriptions :column="2" bordered size="small">
          <a-descriptions-item :label="t('console.runtimeExec.labelExecId')">{{ detail?.id || "-" }}</a-descriptions-item>
          <a-descriptions-item :label="t('console.runtimeExec.labelWorkflowId')">
            {{ detail?.workflowId || "-" }}
          </a-descriptions-item>
          <a-descriptions-item :label="t('console.runtimeExec.labelAppId')">
            <a-button v-if="detail?.appId" type="link" size="small" @click="goToApp(detail.appId)">
              {{ detail.appId }}
            </a-button>
            <span v-else>-</span>
          </a-descriptions-item>
          <a-descriptions-item :label="t('console.runtimeExec.labelReleaseId')">
            <a-button v-if="detail?.releaseId" type="link" size="small" @click="goToRelease(detail.releaseId)">
              {{ detail.releaseId }}
            </a-button>
            <span v-else>-</span>
          </a-descriptions-item>
          <a-descriptions-item :label="t('console.runtimeExec.labelRuntimeContextId')">
            <a-button v-if="detail?.runtimeContextId" type="link" size="small" @click="goToRuntimeContext(detail.runtimeContextId)">
              {{ detail.runtimeContextId }}
            </a-button>
            <span v-else>-</span>
          </a-descriptions-item>
          <a-descriptions-item :label="t('console.runtimeExec.labelStatus')">{{ detail?.status || "-" }}</a-descriptions-item>
          <a-descriptions-item :label="t('console.runtimeExec.labelStartedAt')">{{ formatDate(detail?.startedAt) }}</a-descriptions-item>
          <a-descriptions-item :label="t('console.runtimeExec.labelCompletedAt')">
            {{ formatDate(detail?.completedAt) }}
          </a-descriptions-item>
          <a-descriptions-item :label="t('console.runtimeExec.labelError')" :span="2">
            {{ detail?.errorMessage || "-" }}
          </a-descriptions-item>
        </a-descriptions>

        <a-space class="detail-actions" wrap>
          <a-button :disabled="!detail?.appId" @click="goToRelatedApp">{{ t("console.runtimeExec.linkApp") }}</a-button>
          <a-button :disabled="!detail?.releaseId" @click="goToRelatedRelease">{{ t("console.runtimeExec.linkRelease") }}</a-button>
          <a-button
            :loading="operationLoading"
            :disabled="!detail || !canCancel(detail.status)"
            @click="detail && cancelExecution(detail.id, true)"
          >
            {{ t("console.runtimeExec.actionCancel") }}
          </a-button>
          <a-button
            :loading="operationLoading"
            :disabled="!detail || !canRetry(detail.status)"
            @click="detail && retryExecution(detail.id, true)"
          >
            {{ t("console.runtimeExec.actionRetry") }}
          </a-button>
          <a-button
            :loading="operationLoading"
            :disabled="!detail || !canResume(detail.status)"
            @click="detail && resumeExecution(detail.id, true)"
          >
            {{ t("console.runtimeExec.actionResume") }}
          </a-button>
          <a-button :loading="diagnosisLoading" :disabled="!detail" @click="detail && loadTimeoutDiagnosis(detail.id)">
            {{ t("console.runtimeExec.timeoutDiag") }}
          </a-button>
        </a-space>

        <a-space class="detail-actions" wrap>
          <a-input
            v-model:value="debugNodeKey"
            style="width: 220px"
            :placeholder="t('console.runtimeExec.phNodeKey')"
          />
          <a-button
            :loading="operationLoading"
            :disabled="!detail || !debugNodeKey.trim()"
            @click="detail && debugExecution(detail.id)"
          >
            {{ t("console.runtimeExec.debugNode") }}
          </a-button>
        </a-space>

        <a-alert
          v-if="timeoutDiagnosis"
          class="diagnosis-alert"
          :type="timeoutDiagnosis.timeoutRisk ? 'warning' : 'info'"
          show-icon
          :message="timeoutDiagnosis.diagnosis"
          :description="t('console.runtimeExec.elapsed', { seconds: Math.round(timeoutDiagnosis.elapsedSeconds) })"
        />
        <ul v-if="timeoutDiagnosis" class="diagnosis-suggestions">
          <li v-for="item in timeoutDiagnosis.suggestions" :key="item">{{ item }}</li>
        </ul>

        <a-divider orientation="left">{{ t("console.runtimeExec.sectionInputsJson") }}</a-divider>
        <pre class="json-block">{{ detail?.inputsJson || "-" }}</pre>

        <a-divider orientation="left">{{ t("console.runtimeExec.sectionOutputsJson") }}</a-divider>
        <pre class="json-block">{{ detail?.outputsJson || "-" }}</pre>

        <a-divider orientation="left">{{ t("console.runtimeExec.auditDivider") }}</a-divider>
        <a-space style="margin-bottom: 12px" wrap>
          <a-input-search
            v-model:value="auditKeyword"
            allow-clear
            :placeholder="t('console.runtimeExec.phAuditSearch')"
            style="width: 260px"
            @search="handleAuditSearch"
          />
        </a-space>
        <a-table
          row-key="auditId"
          :loading="auditLoading"
          :columns="auditColumns"
          :data-source="auditRows"
          :pagination="auditPagination"
          size="small"
          @change="handleAuditTableChange"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'occurredAt'">
              {{ formatDate(record.occurredAt) }}
            </template>
            <template v-else-if="column.key === 'actions'">
              <a-button v-if="canOpenAuditTarget(record.target)" type="link" size="small" @click="openAuditTarget(record.target)">
                {{ t("console.runtimeExec.view") }}
              </a-button>
              <span v-else>-</span>
            </template>
          </template>
        </a-table>
      </a-spin>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from "vue";
import { useRouter } from "vue-router";
import type { TableColumnsType, TablePaginationConfig } from "ant-design-vue";
import { message } from "ant-design-vue";
import { debounce } from "@atlas/shared-core";
import { useI18n } from "vue-i18n";
import { getReleaseCenterDetail } from "@/services/api-release-center";
import { getRuntimeContextById } from "@/services/api-runtime-contexts";
import {
  cancelRuntimeExecution,
  debugRuntimeExecution,
  getRuntimeExecutionAuditTrails,
  getRuntimeExecutionDetail,
  getRuntimeExecutionsPaged,
  getRuntimeExecutionTimeoutDiagnosis,
  resumeRuntimeExecution,
  retryRuntimeExecution
} from "@/services/api-runtime-executions";
import { getTenantAppInstanceDetail, getTenantAppInstancesPaged } from "@/services/api-console";
import type {
  ReleaseCenterDetail,
  RuntimeContextDetail,
  RuntimeExecutionAuditTrailItem,
  RuntimeExecutionDetail,
  RuntimeExecutionListItem,
  RuntimeExecutionTimeoutDiagnosis,
  TenantAppInstanceDetail,
  TenantAppInstanceListItem
} from "@/types/platform-console";

const router = useRouter();
const { t, locale } = useI18n();

const isMounted = ref(false);
const loading = ref(false);
const detailLoading = ref(false);
const auditLoading = ref(false);
const operationLoading = ref(false);
const diagnosisLoading = ref(false);

const keyword = ref("");
const selectedAppId = ref<string>();
const selectedStatus = ref<string>();
const startedAtRange = ref<[string, string]>();
const loadingAppOptions = ref(false);
const appFilterOptions = ref<Array<{ label: string; value: string }>>([]);
const auditKeyword = ref("");
const rows = ref<RuntimeExecutionListItem[]>([]);
const detail = ref<RuntimeExecutionDetail | null>(null);
const linkedApp = ref<TenantAppInstanceDetail | null>(null);
const linkedRelease = ref<ReleaseCenterDetail | null>(null);
const linkedRuntimeContext = ref<RuntimeContextDetail | null>(null);
const selectedExecutionId = ref("");
const auditRows = ref<RuntimeExecutionAuditTrailItem[]>([]);
const timeoutDiagnosis = ref<RuntimeExecutionTimeoutDiagnosis | null>(null);
const debugNodeKey = ref("");
const detailVisible = ref(false);

const pageIndex = ref(1);
const pageSize = ref(10);
const auditPageIndex = ref(1);
const auditPageSize = ref(20);
const auditTotal = ref(0);

const statusFilterOptions = computed(() => [
  { label: t("console.runtimeExec.statusPending"), value: "Pending" },
  { label: t("console.runtimeExec.statusRunning"), value: "Running" },
  { label: t("console.runtimeExec.statusCompleted"), value: "Completed" },
  { label: t("console.runtimeExec.statusFailed"), value: "Failed" },
  { label: t("console.runtimeExec.statusCancelled"), value: "Cancelled" },
  { label: t("console.runtimeExec.statusInterrupted"), value: "Interrupted" }
]);

const columns = computed<TableColumnsType<RuntimeExecutionListItem>>(() => [
  { title: t("console.runtimeExec.colWorkflowId"), dataIndex: "workflowId", key: "workflowId", width: 130 },
  { title: t("console.runtimeExec.colAppId"), dataIndex: "appId", key: "appId", width: 130 },
  { title: t("console.runtimeExec.colReleaseId"), dataIndex: "releaseId", key: "releaseId", width: 130 },
  { title: t("console.runtimeExec.colRuntimeContextId"), dataIndex: "runtimeContextId", key: "runtimeContextId", width: 160 },
  { title: t("console.runtimeExec.colStatus"), dataIndex: "status", key: "status", width: 110 },
  { title: t("console.runtimeExec.colStartedAt"), dataIndex: "startedAt", key: "startedAt", width: 180 },
  { title: t("console.runtimeExec.colCompletedAt"), dataIndex: "completedAt", key: "completedAt", width: 180 },
  { title: t("console.runtimeExec.colError"), dataIndex: "errorMessage", key: "errorMessage", ellipsis: true },
  { title: t("console.runtimeExec.colActions"), key: "actions", width: 170, fixed: "right" }
]);

const auditColumns = computed<TableColumnsType<RuntimeExecutionAuditTrailItem>>(() => [
  { title: t("console.runtimeExec.auditColId"), dataIndex: "auditId", key: "auditId", width: 150 },
  { title: t("console.runtimeExec.auditColActor"), dataIndex: "actor", key: "actor", width: 130 },
  { title: t("console.runtimeExec.auditColAction"), dataIndex: "action", key: "action", width: 180 },
  { title: t("console.runtimeExec.auditColResult"), dataIndex: "result", key: "result", width: 120 },
  { title: t("console.runtimeExec.auditColTarget"), dataIndex: "target", key: "target", ellipsis: true },
  { title: t("console.runtimeExec.auditColTime"), dataIndex: "occurredAt", key: "occurredAt", width: 180 },
  { title: t("console.runtimeExec.colActions"), key: "actions", width: 90 }
]);

const pagination = ref<TablePaginationConfig>({
  current: 1,
  pageSize: 10,
  total: 0,
  showSizeChanger: true,
  showTotal: (total) => t("crud.totalItems", { total })
});

const auditPagination = computed<TablePaginationConfig>(() => ({
  current: auditPageIndex.value,
  pageSize: auditPageSize.value,
  total: auditTotal.value,
  showSizeChanger: true,
  showTotal: (total) => t("crud.totalItems", { total })
}));

const appBreadcrumbLabel = computed(() =>
  linkedApp.value?.name || detail.value?.appId || t("console.runtimeExec.linkedApp")
);

const releaseBreadcrumbLabel = computed(() => {
  if (linkedRelease.value) {
    return t("console.runtimeExec.releaseVer", { version: linkedRelease.value.version });
  }
  return detail.value?.releaseId
    ? t("console.runtimeExec.releaseById", { id: detail.value.releaseId })
    : t("console.runtimeExec.releaseId");
});

const runtimeContextBreadcrumbLabel = computed(() => {
  if (linkedRuntimeContext.value) {
    return t("console.runtimeExec.runtimeCtxSlash", {
      appKey: linkedRuntimeContext.value.appKey,
      pageKey: linkedRuntimeContext.value.pageKey
    });
  }
  return detail.value?.runtimeContextId
    ? t("console.runtimeExec.runtimeCtx", { key: detail.value.runtimeContextId })
    : t("console.runtimeExec.runtimeCtxId");
});

const executionBreadcrumbLabel = computed(() =>
  detail.value?.id
    ? t("console.runtimeExec.execRecordWithId", { id: detail.value.id })
    : t("console.runtimeExec.execRecord")
);

function formatDate(value?: string) {
  if (!value) {
    return "-";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }
  return date.toLocaleString(locale.value);
}

function resolveStatusColor(status: string) {
  if (status === "Completed") {
    return "success";
  }
  if (status === "Failed") {
    return "error";
  }
  return "processing";
}

function canCancel(status: string) {
  return status === "Pending" || status === "Running" || status === "Interrupted";
}

function canRetry(status: string) {
  return status === "Failed" || status === "Cancelled" || status === "Interrupted";
}

function canResume(status: string) {
  return status === "Interrupted";
}

function resetLinkedResources() {
  linkedApp.value = null;
  linkedRelease.value = null;
  linkedRuntimeContext.value = null;
}

function mapAppOptions(items: TenantAppInstanceListItem[]) {
  return items.map((item) => ({
    value: item.id,
    label: `${item.name} (${item.appKey})`
  }));
}

async function loadAppFilterOptions(keywordValue = "") {
  loadingAppOptions.value = true;
  try {
    const result = await getTenantAppInstancesPaged({
      pageIndex: 1,
      pageSize: 20,
      keyword: keywordValue || undefined
    });
    if (!isMounted.value) {
      return;
    }
    appFilterOptions.value = mapAppOptions(result.items);
  } catch {
    // ignore option loading failure to avoid interrupting main view
  } finally {
    loadingAppOptions.value = false;
  }
}

const handleSearchAppOptions = debounce((value: string) => {
  void loadAppFilterOptions(value.trim());
}, 300);

async function loadRuntimeExecutions() {
  loading.value = true;
  try {
    const result = await getRuntimeExecutionsPaged({
      pageIndex: pageIndex.value,
      pageSize: pageSize.value,
      keyword: keyword.value || undefined,
      appId: selectedAppId.value,
      status: selectedStatus.value,
      startedFrom: startedAtRange.value?.[0],
      startedTo: startedAtRange.value?.[1]
    });
    if (!isMounted.value) {
      return;
    }

    rows.value = result.items;
    pagination.value = {
      ...pagination.value,
      current: result.pageIndex,
      pageSize: result.pageSize,
      total: result.total
    };
  } catch (error) {
    message.error((error as Error).message || t("console.runtimeExec.loadListFailed"));
  } finally {
    loading.value = false;
  }
}

function handleSearch() {
  pageIndex.value = 1;
  void loadRuntimeExecutions();
}

function handleResetFilters() {
  keyword.value = "";
  selectedAppId.value = undefined;
  selectedStatus.value = undefined;
  startedAtRange.value = undefined;
  pageIndex.value = 1;
  void loadRuntimeExecutions();
}

function handleTableChange(page: TablePaginationConfig) {
  pageIndex.value = page.current ?? 1;
  pageSize.value = page.pageSize ?? 10;
  void loadRuntimeExecutions();
}

function resolveExecutionRowClass(record: RuntimeExecutionListItem) {
  return record.status === "Failed" ? "failed-row" : "";
}

async function loadAuditTrails(executionId: string, targetPageIndex = 1, targetPageSize = auditPageSize.value) {
  auditLoading.value = true;
  try {
    const result = await getRuntimeExecutionAuditTrails(executionId, {
      pageIndex: targetPageIndex,
      pageSize: targetPageSize,
      keyword: auditKeyword.value || undefined
    });
    if (!isMounted.value) {
      return;
    }

    auditRows.value = result.items;
    auditPageIndex.value = result.pageIndex;
    auditPageSize.value = result.pageSize;
    auditTotal.value = result.total;
  } catch (error) {
    message.error((error as Error).message || t("console.runtimeExec.loadAuditFailed"));
  } finally {
    auditLoading.value = false;
  }
}

async function loadLinkedResources(detailResult: RuntimeExecutionDetail) {
  resetLinkedResources();
  const jobs: Array<Promise<void>> = [];

  if (detailResult.appId) {
    jobs.push((async () => {
      const app = await getTenantAppInstanceDetail(detailResult.appId!);
      if (isMounted.value) {
        linkedApp.value = app;
      }
    })());
  }

  if (detailResult.releaseId) {
    jobs.push((async () => {
      const release = await getReleaseCenterDetail(detailResult.releaseId!);
      if (isMounted.value) {
        linkedRelease.value = release;
      }
    })());
  }

  if (detailResult.runtimeContextId) {
    jobs.push((async () => {
      const runtimeContext = await getRuntimeContextById(detailResult.runtimeContextId!);
      if (isMounted.value) {
        linkedRuntimeContext.value = runtimeContext;
      }
    })());
  }

  if (jobs.length > 0) {
    await Promise.allSettled(jobs);
  }
}

async function openDetail(id: string) {
  detailVisible.value = true;
  selectedExecutionId.value = id;
  detailLoading.value = true;
  timeoutDiagnosis.value = null;
  debugNodeKey.value = "";
  auditKeyword.value = "";
  auditRows.value = [];
  auditTotal.value = 0;
  auditPageIndex.value = 1;

  try {
    const detailResult = await getRuntimeExecutionDetail(id);
    if (!isMounted.value) {
      return;
    }

    detail.value = detailResult;
    await Promise.all([
      loadLinkedResources(detailResult),
      loadAuditTrails(id, 1, auditPageSize.value)
    ]);
  } catch (error) {
    message.error((error as Error).message || t("console.runtimeExec.loadDetailFailed"));
    detailVisible.value = false;
  } finally {
    detailLoading.value = false;
  }
}

async function loadTimeoutDiagnosis(executionId: string) {
  diagnosisLoading.value = true;
  try {
    timeoutDiagnosis.value = await getRuntimeExecutionTimeoutDiagnosis(executionId);
  } catch (error) {
    message.error((error as Error).message || t("console.runtimeExec.loadTimeoutFailed"));
  } finally {
    diagnosisLoading.value = false;
  }
}

async function cancelExecution(executionId: string, refreshDetail = false) {
  operationLoading.value = true;
  try {
    const result = await cancelRuntimeExecution(executionId);
    if (!isMounted.value) {
      return;
    }

    message.success(result.message);
    await loadRuntimeExecutions();

    if (refreshDetail) {
      await openDetail(executionId);
    }
  } catch (error) {
    message.error((error as Error).message || t("console.runtimeExec.cancelFailed"));
  } finally {
    operationLoading.value = false;
  }
}

async function retryExecution(executionId: string, refreshDetail = false) {
  operationLoading.value = true;
  try {
    const result = await retryRuntimeExecution(executionId);
    if (!isMounted.value) {
      return;
    }

    message.success(result.message);
    await loadRuntimeExecutions();

    if (refreshDetail) {
      if (result.newExecutionId) {
        await openDetail(result.newExecutionId);
      } else {
        await openDetail(executionId);
      }
    }
  } catch (error) {
    message.error((error as Error).message || t("console.runtimeExec.retryFailed"));
  } finally {
    operationLoading.value = false;
  }
}

async function resumeExecution(executionId: string, refreshDetail = false) {
  operationLoading.value = true;
  try {
    const result = await resumeRuntimeExecution(executionId);
    if (!isMounted.value) {
      return;
    }

    message.success(result.message);
    await loadRuntimeExecutions();

    if (refreshDetail) {
      await openDetail(executionId);
    }
  } catch (error) {
    message.error((error as Error).message || t("console.runtimeExec.resumeFailed"));
  } finally {
    operationLoading.value = false;
  }
}

async function debugExecution(executionId: string) {
  const nodeKey = debugNodeKey.value.trim();
  if (!nodeKey) {
    message.warning(t("console.runtimeExec.warnNodeKey"));
    return;
  }

  operationLoading.value = true;
  try {
    const result = await debugRuntimeExecution(executionId, {
      nodeKey,
      inputsJson: detail.value?.inputsJson
    });
    if (!isMounted.value) {
      return;
    }

    message.success(result.message);
    await loadRuntimeExecutions();
    if (result.newExecutionId) {
      await openDetail(result.newExecutionId);
    }
  } catch (error) {
    message.error((error as Error).message || t("console.runtimeExec.debugFailed"));
  } finally {
    operationLoading.value = false;
  }
}

function handleDetailClose() {
  detail.value = null;
  selectedExecutionId.value = "";
  auditRows.value = [];
  auditTotal.value = 0;
  auditKeyword.value = "";
  timeoutDiagnosis.value = null;
  debugNodeKey.value = "";
  resetLinkedResources();
}

async function goToRuntimeContextByRecord(record: RuntimeExecutionListItem) {
  if (!record.runtimeContextId) {
    return;
  }

  const query: Record<string, string> = {
    runtimeContextId: record.runtimeContextId
  };

  if (record.appId) {
    try {
      const app = await getTenantAppInstanceDetail(record.appId);
      if (!isMounted.value) {
        return;
      }
      if (app.appKey) {
        query.appKey = app.appKey;
      }
    } catch {
      // fallback to runtimeContextId-only navigation
    }
  }

  await router.push({
    path: "/console/runtime-contexts",
    query
  });
}

async function goToRuntimeContext(runtimeContextId: string) {
  const query: Record<string, string> = {
    runtimeContextId
  };
  const appKey = linkedRuntimeContext.value?.appKey || linkedApp.value?.appKey;
  if (appKey) {
    query.appKey = appKey;
  }

  await router.push({
    path: "/console/runtime-contexts",
    query
  });
}

function goToApp(appId: string) {
  void router.push(`/apps/${appId}/dashboard`);
}

function goToRelease(releaseId: string) {
  void router.push({
    path: "/console/releases",
    query: { releaseId }
  });
}

function goToRelatedApp() {
  if (detail.value?.appId) {
    goToApp(detail.value.appId);
  }
}

function goToRelatedRelease() {
  if (detail.value?.releaseId) {
    goToRelease(detail.value.releaseId);
  }
}

function handleAuditSearch() {
  if (!selectedExecutionId.value) {
    return;
  }
  void loadAuditTrails(selectedExecutionId.value, 1, auditPageSize.value);
}

function handleAuditTableChange(page: TablePaginationConfig) {
  if (!selectedExecutionId.value) {
    return;
  }
  void loadAuditTrails(
    selectedExecutionId.value,
    page.current ?? 1,
    page.pageSize ?? auditPageSize.value
  );
}

type AuditTargetRoute =
  | { type: "release"; id: string }
  | { type: "app"; id: string }
  | { type: "runtime"; id: string }
  | { type: "execution"; id: string };

function parseAuditTarget(target: string): AuditTargetRoute | null {
  if (!target) {
    return null;
  }

  const releaseMatch = target.match(/(?:Release|AppRelease):([A-Za-z0-9-]+)/i);
  if (releaseMatch?.[1]) {
    return { type: "release", id: releaseMatch[1] };
  }

  const runtimeMatch = target.match(/(?:RuntimeContext|RuntimeRoute):([A-Za-z0-9-]+)/i);
  if (runtimeMatch?.[1]) {
    return { type: "runtime", id: runtimeMatch[1] };
  }

  const appMatch = target.match(/(?:App|AppManifest):([A-Za-z0-9-]+)/i);
  if (appMatch?.[1]) {
    return { type: "app", id: appMatch[1] };
  }

  const executionMatch = target.match(/(?:WorkflowExecution|RuntimeExecution):([A-Za-z0-9-]+)/i);
  if (executionMatch?.[1]) {
    return { type: "execution", id: executionMatch[1] };
  }

  if (/^[A-Za-z0-9-]+$/.test(target)) {
    return { type: "execution", id: target };
  }
  return null;
}

function canOpenAuditTarget(target: string) {
  return parseAuditTarget(target) !== null;
}

function openAuditTarget(target: string) {
  const route = parseAuditTarget(target);
  if (!route) {
    return;
  }

  if (route.type === "release") {
    goToRelease(route.id);
    return;
  }
  if (route.type === "app") {
    goToApp(route.id);
    return;
  }
  if (route.type === "runtime") {
    void goToRuntimeContext(route.id);
    return;
  }
  void openDetail(route.id);
}

onMounted(() => {
  isMounted.value = true;
  void loadAppFilterOptions();
  void loadRuntimeExecutions();
});

onUnmounted(() => {
  isMounted.value = false;
});
</script>

<style scoped>
.runtime-executions-page {
  padding: 24px;
}

.runtime-execution-card {
  border-radius: 12px;
}

.json-block {
  max-height: 220px;
  overflow: auto;
  white-space: pre-wrap;
  word-break: break-all;
  background: #fafafa;
  border: 1px solid #f0f0f0;
  border-radius: 8px;
  padding: 12px;
}

.execution-breadcrumb {
  margin-bottom: 12px;
}

.detail-actions {
  margin-top: 12px;
}

.diagnosis-alert {
  margin-top: 12px;
}

.diagnosis-suggestions {
  margin: 8px 0 0;
  padding-left: 18px;
  color: #595959;
}

:deep(.failed-row > td) {
  background: #fff1f0 !important;
}
</style>
