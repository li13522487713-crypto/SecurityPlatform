<template>
  <div class="runtime-executions-page" data-testid="e2e-console-runtime-executions-page">
    <a-card :bordered="false" class="runtime-execution-card">
      <template #title>运行执行记录</template>
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
            placeholder="按应用过滤"
            @search="handleSearchAppOptions"
          />
          <a-select
            v-model:value="selectedStatus"
            style="width: 160px"
            :options="statusFilterOptions"
            allow-clear
            placeholder="按状态过滤"
          />
          <a-range-picker
            v-model:value="startedAtRange"
            show-time
            value-format="YYYY-MM-DDTHH:mm:ss.SSS[Z]"
            :presets="startedAtPresets"
          />
          <a-input-search
            v-model:value="keyword"
            allow-clear
            placeholder="按ID/状态/错误信息检索"
            style="width: 240px"
            @search="handleSearch"
          />
          <a-button type="primary" @click="handleSearch">查询</a-button>
          <a-button @click="handleResetFilters">重置</a-button>
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
            <a-button
              v-if="record.appId"
              type="link"
              size="small"
              @click="goToApp(record.appId)"
            >
              {{ record.appId }}
            </a-button>
            <span v-else>-</span>
          </template>
          <template v-else-if="column.key === 'releaseId'">
            <a-button
              v-if="record.releaseId"
              type="link"
              size="small"
              @click="goToRelease(record.releaseId)"
            >
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
          <template v-if="column.key === 'startedAt'">
            {{ formatDate(record.startedAt) }}
          </template>
          <template v-if="column.key === 'completedAt'">
            {{ formatDate(record.completedAt) }}
          </template>
          <template v-if="column.key === 'errorMessage'">
            {{ record.errorMessage || "-" }}
          </template>
          <template v-if="column.key === 'actions'">
            <a-space size="small">
              <a-button type="link" size="small" @click="openDetail(record.id)">详情</a-button>
              <a-button v-if="canCancel(record.status)" type="link" size="small" @click="cancelExecution(record.id)">
                取消
              </a-button>
              <a-button v-if="canRetry(record.status)" type="link" size="small" @click="retryExecution(record.id)">
                重试
              </a-button>
              <a-button v-if="canResume(record.status)" type="link" size="small" @click="resumeExecution(record.id)">
                恢复
              </a-button>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <a-drawer
      v-model:open="detailVisible"
      title="运行执行详情"
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
          <a-descriptions-item label="执行ID">{{ detail?.id || "-" }}</a-descriptions-item>
          <a-descriptions-item label="WorkflowId">{{ detail?.workflowId || "-" }}</a-descriptions-item>
          <a-descriptions-item label="AppId">
            <a-button
              v-if="detail?.appId"
              type="link"
              size="small"
              @click="goToApp(detail.appId)"
            >
              {{ detail.appId }}
            </a-button>
            <span v-else>-</span>
          </a-descriptions-item>
          <a-descriptions-item label="ReleaseId">
            <a-button
              v-if="detail?.releaseId"
              type="link"
              size="small"
              @click="goToRelease(detail.releaseId)"
            >
              {{ detail.releaseId }}
            </a-button>
            <span v-else>-</span>
          </a-descriptions-item>
          <a-descriptions-item label="RuntimeContextId">
            <a-button
              v-if="detail?.runtimeContextId"
              type="link"
              size="small"
              @click="goToRuntimeContext(detail.runtimeContextId)"
            >
              {{ detail.runtimeContextId }}
            </a-button>
            <span v-else>-</span>
          </a-descriptions-item>
          <a-descriptions-item label="状态">{{ detail?.status || "-" }}</a-descriptions-item>
          <a-descriptions-item label="开始时间">{{ formatDate(detail?.startedAt) }}</a-descriptions-item>
          <a-descriptions-item label="完成时间">{{ formatDate(detail?.completedAt) }}</a-descriptions-item>
          <a-descriptions-item label="错误信息" :span="2">
            {{ detail?.errorMessage || "-" }}
          </a-descriptions-item>
        </a-descriptions>

        <a-space class="detail-actions" wrap>
          <a-button
            :disabled="!detail?.appId"
            @click="goToRelatedApp"
          >
            查看关联应用
          </a-button>
          <a-button
            :disabled="!detail?.releaseId"
            @click="goToRelatedRelease"
          >
            查看发布版本
          </a-button>
          <a-button
            :loading="operationLoading"
            :disabled="!detail || !canCancel(detail.status)"
            @click="detail && cancelExecution(detail.id, true)"
          >
            取消执行
          </a-button>
          <a-button
            :loading="operationLoading"
            :disabled="!detail || !canRetry(detail.status)"
            @click="detail && retryExecution(detail.id, true)"
          >
            重试执行
          </a-button>
          <a-button
            :loading="operationLoading"
            :disabled="!detail || !canResume(detail.status)"
            @click="detail && resumeExecution(detail.id, true)"
          >
            恢复执行
          </a-button>
          <a-button
            :loading="diagnosisLoading"
            :disabled="!detail"
            @click="detail && loadTimeoutDiagnosis(detail.id)"
          >
            超时诊断
          </a-button>
        </a-space>

        <a-space class="detail-actions" wrap>
          <a-input
            v-model:value="debugNodeKey"
            style="width: 220px"
            placeholder="输入节点 Key 进行调试"
          />
          <a-button
            :loading="operationLoading"
            :disabled="!detail || !debugNodeKey.trim()"
            @click="detail && debugExecution(detail.id)"
          >
            单节点调试
          </a-button>
        </a-space>

        <a-alert
          v-if="timeoutDiagnosis"
          class="diagnosis-alert"
          :type="timeoutDiagnosis.timeoutRisk ? 'warning' : 'info'"
          show-icon
          :message="timeoutDiagnosis.diagnosis"
          :description="`耗时 ${Math.round(timeoutDiagnosis.elapsedSeconds)} 秒`"
        />
        <ul v-if="timeoutDiagnosis" class="diagnosis-suggestions">
          <li v-for="item in timeoutDiagnosis.suggestions" :key="item">{{ item }}</li>
        </ul>

        <a-divider orientation="left">InputsJson</a-divider>
        <pre class="json-block">{{ detail?.inputsJson || "-" }}</pre>

        <a-divider orientation="left">OutputsJson</a-divider>
        <pre class="json-block">{{ detail?.outputsJson || "-" }}</pre>

        <a-divider orientation="left">审计追踪</a-divider>
        <a-space style="margin-bottom: 12px" wrap>
          <a-input-search
            v-model:value="auditKeyword"
            allow-clear
            placeholder="按 action / target / actor 检索"
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
              <a-button
                v-if="canOpenAuditTarget(record.target)"
                type="link"
                size="small"
                @click="openAuditTarget(record.target)"
              >
                查看
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
import { computed, onMounted, ref } from "vue";
import dayjs from "dayjs";
import { useRouter } from "vue-router";
import type { TableColumnsType, TablePaginationConfig } from "ant-design-vue";
import { message } from "ant-design-vue";
import { getReleaseCenterDetail } from "@/services/api-coze-runtime";
import {
  cancelRuntimeExecution,
  debugRuntimeExecution,
  getRuntimeExecutionAuditTrails,
  getRuntimeExecutionDetail,
  getRuntimeExecutionTimeoutDiagnosis,
  getRuntimeExecutionsPaged,
  resumeRuntimeExecution,
  retryRuntimeExecution
} from "@/services/api-runtime-executions";
import { getRuntimeContextById } from "@/services/api-runtime-contexts";
import { getTenantAppInstanceDetail, getTenantAppInstancesPaged } from "@/services/api-tenant-app-instances";
import { debounce } from "@/utils/common";
import type {
  ReleaseCenterDetail,
  RuntimeExecutionAuditTrailItem,
  RuntimeExecutionDetail,
  RuntimeExecutionListItem,
  RuntimeExecutionTimeoutDiagnosis,
  RuntimeContextDetail,
  TenantAppInstanceDetail,
  TenantAppInstanceListItem
} from "@/types/platform-v2";

const router = useRouter();
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
const selectedExecutionId = ref<string>("");
const auditRows = ref<RuntimeExecutionAuditTrailItem[]>([]);
const timeoutDiagnosis = ref<RuntimeExecutionTimeoutDiagnosis | null>(null);
const debugNodeKey = ref("");
const detailVisible = ref(false);
const pageIndex = ref(1);
const pageSize = ref(10);
const auditPageIndex = ref(1);
const auditPageSize = ref(20);
const auditTotal = ref(0);
const statusFilterOptions = [
  { label: "Pending", value: "Pending" },
  { label: "Running", value: "Running" },
  { label: "Completed", value: "Completed" },
  { label: "Failed", value: "Failed" },
  { label: "Cancelled", value: "Cancelled" },
  { label: "Interrupted", value: "Interrupted" }
] as const;
const startedAtPresets = [
  { label: "最近 24 小时", value: [dayjs().subtract(1, "day"), dayjs()] },
  { label: "最近 7 天", value: [dayjs().subtract(7, "day"), dayjs()] },
  { label: "最近 30 天", value: [dayjs().subtract(30, "day"), dayjs()] }
];

const columns: TableColumnsType<RuntimeExecutionListItem> = [
  { title: "WorkflowId", dataIndex: "workflowId", key: "workflowId", width: 130 },
  { title: "AppId", dataIndex: "appId", key: "appId", width: 130 },
  { title: "ReleaseId", dataIndex: "releaseId", key: "releaseId", width: 130 },
  { title: "RuntimeContextId", dataIndex: "runtimeContextId", key: "runtimeContextId", width: 160 },
  { title: "状态", dataIndex: "status", key: "status", width: 110 },
  { title: "开始时间", dataIndex: "startedAt", key: "startedAt", width: 180 },
  { title: "完成时间", dataIndex: "completedAt", key: "completedAt", width: 180 },
  { title: "错误信息", dataIndex: "errorMessage", key: "errorMessage", ellipsis: true },
  { title: "操作", key: "actions", width: 90, fixed: "right" }
];

const auditColumns: TableColumnsType<RuntimeExecutionAuditTrailItem> = [
  { title: "审计ID", dataIndex: "auditId", key: "auditId", width: 150 },
  { title: "操作人", dataIndex: "actor", key: "actor", width: 130 },
  { title: "动作", dataIndex: "action", key: "action", width: 180 },
  { title: "结果", dataIndex: "result", key: "result", width: 120 },
  { title: "目标", dataIndex: "target", key: "target", ellipsis: true },
  { title: "发生时间", dataIndex: "occurredAt", key: "occurredAt", width: 180 },
  { title: "操作", key: "actions", width: 90 }
];

const pagination = ref<TablePaginationConfig>({
  current: 1,
  pageSize: 10,
  total: 0,
  showSizeChanger: true,
  showTotal: (all) => `共 ${all} 条`
});

const auditPagination = computed<TablePaginationConfig>(() => ({
  current: auditPageIndex.value,
  pageSize: auditPageSize.value,
  total: auditTotal.value,
  showSizeChanger: true,
  showTotal: (all) => `共 ${all} 条`
}));

const appBreadcrumbLabel = computed(() =>
  linkedApp.value?.name || detail.value?.appId || "租户应用"
);
const releaseBreadcrumbLabel = computed(() => {
  if (linkedRelease.value) {
    return `发布版本 v${linkedRelease.value.version}`;
  }
  return detail.value?.releaseId ? `发布版本 ${detail.value.releaseId}` : "发布版本";
});
const runtimeContextBreadcrumbLabel = computed(() => {
  if (linkedRuntimeContext.value) {
    return `运行上下文 ${linkedRuntimeContext.value.appKey}/${linkedRuntimeContext.value.pageKey}`;
  }
  return detail.value?.runtimeContextId ? `运行上下文 ${detail.value.runtimeContextId}` : "运行上下文";
});
const executionBreadcrumbLabel = computed(() =>
  detail.value?.id ? `执行记录 ${detail.value.id}` : "执行记录"
);

function formatDate(value?: string) {
  if (!value) {
    return "-";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleString();
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
    label: `${item.name}（${item.appKey}）`
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
    appFilterOptions.value = mapAppOptions(result.items);
  } catch {
    // ignore filter option loading failures to avoid interrupting main table.
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
    rows.value = result.items;
    pagination.value = {
      ...pagination.value,
      current: result.pageIndex,
      pageSize: result.pageSize,
      total: result.total
    };
  } catch (error) {
    message.error((error as Error).message || "加载运行执行记录失败");
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
    const auditResult = await getRuntimeExecutionAuditTrails(executionId, {
      pageIndex: targetPageIndex,
      pageSize: targetPageSize,
      keyword: auditKeyword.value || undefined
    });
    auditRows.value = auditResult.items;
    auditPageIndex.value = auditResult.pageIndex;
    auditPageSize.value = auditResult.pageSize;
    auditTotal.value = auditResult.total;
  } catch (error) {
    message.error((error as Error).message || "加载运行执行审计轨迹失败");
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
      linkedApp.value = app;
    })());
  }

  if (detailResult.releaseId) {
    jobs.push((async () => {
      const release = await getReleaseCenterDetail(detailResult.releaseId!);
      linkedRelease.value = release;
    })());
  }

  if (detailResult.runtimeContextId) {
    jobs.push((async () => {
      const runtimeContext = await getRuntimeContextById(detailResult.runtimeContextId!);
      linkedRuntimeContext.value = runtimeContext;
    })());
  }

  if (jobs.length === 0) {
    return;
  }
  await Promise.allSettled(jobs);
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
    detail.value = detailResult;
    await Promise.all([
      loadLinkedResources(detailResult),
      loadAuditTrails(id, 1, auditPageSize.value)
    ]);
  } catch (error) {
    message.error((error as Error).message || "加载运行执行详情失败");
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
    message.error((error as Error).message || "加载超时诊断失败");
  } finally {
    diagnosisLoading.value = false;
  }
}

async function cancelExecution(executionId: string, refreshDetail = false) {
  operationLoading.value = true;
  try {
    const result = await cancelRuntimeExecution(executionId);
    message.success(result.message);
    await loadRuntimeExecutions();
    if (refreshDetail) {
      await openDetail(executionId);
    }
  } catch (error) {
    message.error((error as Error).message || "取消执行失败");
  } finally {
    operationLoading.value = false;
  }
}

async function retryExecution(executionId: string, refreshDetail = false) {
  operationLoading.value = true;
  try {
    const result = await retryRuntimeExecution(executionId);
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
    message.error((error as Error).message || "重试执行失败");
  } finally {
    operationLoading.value = false;
  }
}

async function resumeExecution(executionId: string, refreshDetail = false) {
  operationLoading.value = true;
  try {
    const result = await resumeRuntimeExecution(executionId);
    message.success(result.message);
    await loadRuntimeExecutions();
    if (refreshDetail) {
      await openDetail(executionId);
    }
  } catch (error) {
    message.error((error as Error).message || "恢复执行失败");
  } finally {
    operationLoading.value = false;
  }
}

async function debugExecution(executionId: string) {
  const nodeKey = debugNodeKey.value.trim();
  if (!nodeKey) {
    message.warning("请输入节点 Key");
    return;
  }

  operationLoading.value = true;
  try {
    const result = await debugRuntimeExecution(executionId, {
      nodeKey,
      inputsJson: detail.value?.inputsJson
    });
    message.success(result.message);
    await loadRuntimeExecutions();
    if (result.newExecutionId) {
      await openDetail(result.newExecutionId);
    }
  } catch (error) {
    message.error((error as Error).message || "单节点调试失败");
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
      if (app.appKey) {
        query.appKey = app.appKey;
      }
    } catch {
      // ignore appKey resolving failure and fallback to runtimeContextId navigation
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
  const linkedAppKey = linkedRuntimeContext.value?.appKey || linkedApp.value?.appKey;
  if (linkedAppKey) {
    query.appKey = linkedAppKey;
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
  if (!detail.value?.appId) {
    return;
  }
  goToApp(detail.value.appId);
}

function goToRelatedRelease() {
  if (!detail.value?.releaseId) {
    return;
  }
  goToRelease(detail.value.releaseId);
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
  const nextPageIndex = page.current ?? 1;
  const nextPageSize = page.pageSize ?? auditPageSize.value;
  void loadAuditTrails(selectedExecutionId.value, nextPageIndex, nextPageSize);
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

  const releaseMatch = target.match(/(?:Release|AppRelease):(\d+)/i);
  if (releaseMatch?.[1]) {
    return { type: "release", id: releaseMatch[1] };
  }

  const runtimeMatch = target.match(/(?:RuntimeContext|RuntimeRoute):(\d+)/i);
  if (runtimeMatch?.[1]) {
    return { type: "runtime", id: runtimeMatch[1] };
  }

  const appMatch = target.match(/(?:App|AppManifest):(\d+)/i);
  if (appMatch?.[1]) {
    return { type: "app", id: appMatch[1] };
  }

  const executionMatch = target.match(/(?:WorkflowExecution|RuntimeExecution):(\d+)/i);
  if (executionMatch?.[1]) {
    return { type: "execution", id: executionMatch[1] };
  }

  if (/^\d+$/.test(target)) {
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
  void loadAppFilterOptions();
  void loadRuntimeExecutions();
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
