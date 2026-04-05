<template>
  <div class="release-center-page" data-testid="e2e-release-center-page">
    <a-card :bordered="false" class="release-card">
      <template #title>{{ t("console.releaseCenter.title") }}</template>
      <template #extra>
        <a-space wrap>
          <a-select
            v-model:value="selectedStatus"
            allow-clear
            :placeholder="t('console.releaseCenter.phStatus')"
            style="width: 140px"
            :options="statusOptions"
          />
          <a-input
            v-model:value="appKeyFilter"
            allow-clear
            :placeholder="t('console.releaseCenter.phAppKey')"
            style="width: 180px"
          />
          <a-input
            v-model:value="manifestIdFilter"
            allow-clear
            :placeholder="t('console.releaseCenter.phCatalogId')"
            style="width: 160px"
          />
          <a-input-search
            v-model:value="keyword"
            allow-clear
            :placeholder="t('console.releaseCenter.phNote')"
            style="width: 240px"
            @search="handleSearch"
          />
          <a-button @click="resetFilters">{{ t("console.releaseCenter.reset") }}</a-button>
        </a-space>
      </template>

      <a-table
        row-key="releaseId"
        :loading="loading"
        :columns="columns"
        :data-source="rows"
        :pagination="pagination"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'status'">
            <a-tag :color="record.status === 'Released' ? 'processing' : 'default'">
              {{ record.status }}
            </a-tag>
          </template>
          <template v-if="column.key === 'releasedAt'">
            {{ formatDate(record.releasedAt) }}
          </template>
          <template v-if="column.key === 'actions'">
            <a-space>
              <a-button type="link" size="small" @click="viewDetail(record.releaseId)">
                {{ t("console.releaseCenter.detail") }}
              </a-button>
              <a-button type="link" size="small" @click="openDebugTrace(record.releaseId)">
                {{ t("console.releaseCenter.trace") }}
              </a-button>
              <a-popconfirm :title="t('console.releaseCenter.rollbackConfirm')" @confirm="rollback(record.releaseId)">
                <a-button type="link" size="small" danger>{{ t("console.releaseCenter.rollback") }}</a-button>
              </a-popconfirm>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <a-modal v-model:open="detailVisible" :title="t('console.releaseCenter.modalTitle')" width="760px" :footer="null">
      <a-alert
        v-if="rollbackResult"
        class="rollback-result"
        :type="rollbackResult.switched ? 'success' : 'warning'"
        show-icon
        :message="
          rollbackResult.switched ? t('console.releaseCenter.rollbackSwitched') : t('console.releaseCenter.rollbackNoSwitch')
        "
        :description="buildRollbackMessage(rollbackResult)"
      />
      <a-descriptions :column="2" bordered size="small">
        <a-descriptions-item :label="t('console.releaseCenter.labelReleaseId')">{{ detail?.releaseId }}</a-descriptions-item>
        <a-descriptions-item :label="t('console.releaseCenter.labelCatalogId')">{{ detail?.applicationCatalogId }}</a-descriptions-item>
        <a-descriptions-item :label="t('console.releaseCenter.labelApp')">{{ detail?.applicationCatalogName }}</a-descriptions-item>
        <a-descriptions-item :label="t('console.releaseCenter.colAppKey')">{{ detail?.appKey }}</a-descriptions-item>
        <a-descriptions-item :label="t('console.releaseCenter.labelVersion')">{{ detail?.version }}</a-descriptions-item>
        <a-descriptions-item :label="t('console.releaseCenter.labelStatus')">{{ detail?.status }}</a-descriptions-item>
        <a-descriptions-item :label="t('console.releaseCenter.labelReleasedAt')" :span="2">{{
          formatDate(detail?.releasedAt)
        }}</a-descriptions-item>
        <a-descriptions-item :label="t('console.releaseCenter.labelNote')" :span="2">{{ detail?.releaseNote || "-" }}</a-descriptions-item>
      </a-descriptions>
      <a-typography-paragraph class="snapshot-title">{{ t("console.releaseCenter.diffTitle") }}</a-typography-paragraph>
      <a-descriptions bordered size="small" :column="3">
        <a-descriptions-item :label="t('console.releaseCenter.labelBaseline')">{{
          diffSummary?.baselineReleaseId || "-"
        }}</a-descriptions-item>
        <a-descriptions-item :label="t('console.releaseCenter.labelAdded')">{{ diffSummary?.addedCount ?? 0 }}</a-descriptions-item>
        <a-descriptions-item :label="t('console.releaseCenter.labelRemoved')">{{ diffSummary?.removedCount ?? 0 }}</a-descriptions-item>
        <a-descriptions-item :label="t('console.releaseCenter.labelChanged')">{{ diffSummary?.changedCount ?? 0 }}</a-descriptions-item>
      </a-descriptions>
      <a-typography-paragraph class="snapshot-json">{{ formatDiffSummary(diffSummary) }}</a-typography-paragraph>
      <a-typography-paragraph class="snapshot-title">{{ t("console.releaseCenter.impactTitle") }}</a-typography-paragraph>
      <a-descriptions bordered size="small" :column="2">
        <a-descriptions-item :label="t('console.releaseCenter.labelRoutes')">{{ impactSummary?.runtimeRouteCount ?? 0 }}</a-descriptions-item>
        <a-descriptions-item :label="t('console.releaseCenter.labelActiveRoutes')">{{
          impactSummary?.activeRuntimeRouteCount ?? 0
        }}</a-descriptions-item>
        <a-descriptions-item :label="t('console.releaseCenter.labelCtxCount')">{{ impactSummary?.runtimeContextCount ?? 0 }}</a-descriptions-item>
        <a-descriptions-item :label="t('console.releaseCenter.labelExec24h')">{{ impactSummary?.recentExecutionCount ?? 0 }}</a-descriptions-item>
        <a-descriptions-item :label="t('console.releaseCenter.labelRunning')">{{ impactSummary?.runningExecutionCount ?? 0 }}</a-descriptions-item>
        <a-descriptions-item :label="t('console.releaseCenter.labelFailed')">{{ impactSummary?.failedExecutionCount ?? 0 }}</a-descriptions-item>
      </a-descriptions>
      <a-typography-paragraph class="snapshot-title">{{ t("console.releaseCenter.snapshotJsonTitle") }}</a-typography-paragraph>
      <a-typography-paragraph class="snapshot-json">{{ detail?.snapshotJson || "{}" }}</a-typography-paragraph>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { useRoute, useRouter } from "vue-router";
import type { TableColumnsType, TablePaginationConfig } from "ant-design-vue";
import { message } from "ant-design-vue";
import {
  getReleaseCenterDiff,
  getReleaseCenterDetail,
  getReleaseCenterImpact,
  getReleaseCenterPaged,
  rollbackReleaseCenter
} from "@/services/api-release-center";
import type {
  ReleaseCenterDetail,
  ReleaseCenterListItem,
  ReleaseDiffSummary,
  ReleaseImpactSummary,
  ReleaseRollbackResult
} from "@/types/platform-console";

const { t, locale } = useI18n();
const route = useRoute();
const router = useRouter();

const isMounted = ref(false);
const loading = ref(false);
const keyword = ref("");
const selectedStatus = ref<string>();
const appKeyFilter = ref("");
const manifestIdFilter = ref("");
const rows = ref<ReleaseCenterListItem[]>([]);
const detail = ref<ReleaseCenterDetail | null>(null);
const diffSummary = ref<ReleaseDiffSummary | null>(null);
const impactSummary = ref<ReleaseImpactSummary | null>(null);
const rollbackResult = ref<ReleaseRollbackResult | null>(null);
const detailVisible = ref(false);
const pageIndex = ref(1);
const pageSize = ref(10);

const columns = computed<TableColumnsType<ReleaseCenterListItem>>(() => [
  { title: t("console.releaseCenter.colReleaseId"), dataIndex: "releaseId", key: "releaseId", width: 160 },
  { title: t("console.releaseCenter.colCatalog"), dataIndex: "applicationCatalogName", key: "applicationCatalogName", width: 180 },
  { title: t("console.releaseCenter.colAppKey"), dataIndex: "appKey", key: "appKey", width: 160 },
  { title: t("console.releaseCenter.colVersion"), dataIndex: "version", key: "version", width: 90 },
  { title: t("console.releaseCenter.colStatus"), dataIndex: "status", key: "status", width: 110 },
  { title: t("console.releaseCenter.colReleasedAt"), dataIndex: "releasedAt", key: "releasedAt", width: 180 },
  { title: t("console.releaseCenter.colNote"), dataIndex: "releaseNote", key: "releaseNote", ellipsis: true },
  { title: t("console.releaseCenter.colActions"), key: "actions", width: 220, fixed: "right" }
]);

const pagination = ref<TablePaginationConfig>({
  current: 1,
  pageSize: 10,
  total: 0,
  showSizeChanger: true,
  showTotal: (all) => t("crud.totalItems", { total: all })
});

const statusOptions = computed(() => [
  { label: "Pending", value: "Pending" },
  { label: "Released", value: "Released" },
  { label: "RolledBack", value: "RolledBack" }
]);

function formatDate(value?: string) {
  if (!value) {
    return "-";
  }

  return new Date(value).toLocaleString(locale.value === "en-US" ? "en-US" : "zh-CN");
}

async function loadReleases() {
  loading.value = true;
  try {
    const parsedManifestId =
      manifestIdFilter.value.trim().length > 0 ? Number.parseInt(manifestIdFilter.value.trim(), 10) : Number.NaN;
    const result = await getReleaseCenterPaged({
      pageIndex: pageIndex.value,
      pageSize: pageSize.value,
      keyword: keyword.value || undefined,
      status: selectedStatus.value,
      appKey: appKeyFilter.value || undefined,
      manifestId: Number.isNaN(parsedManifestId) ? undefined : parsedManifestId
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
    message.error((error as Error).message || t("console.releaseCenter.loadFailed"));
  } finally {
    loading.value = false;
  }
}

function handleTableChange(page: TablePaginationConfig) {
  pageIndex.value = page.current ?? 1;
  pageSize.value = page.pageSize ?? 10;
  void loadReleases();
}

function handleSearch() {
  pageIndex.value = 1;
  void loadReleases();
}

function resetFilters() {
  keyword.value = "";
  selectedStatus.value = undefined;
  appKeyFilter.value = "";
  manifestIdFilter.value = "";
  pageIndex.value = 1;
  void loadReleases();
}

async function viewDetail(releaseId: string) {
  try {
    const [detailData, diffData, impactData] = await Promise.all([
      getReleaseCenterDetail(releaseId),
      getReleaseCenterDiff(releaseId),
      getReleaseCenterImpact(releaseId)
    ]);

    if (!isMounted.value) {
      return;
    }
    detail.value = detailData;
    diffSummary.value = diffData;
    impactSummary.value = impactData;
    detailVisible.value = true;
  } catch (error) {
    message.error((error as Error).message || t("console.releaseCenter.loadDetailFailed"));
  }
}

async function rollback(releaseId: string) {
  try {
    const result = await rollbackReleaseCenter(releaseId);

    if (!isMounted.value) {
      return;
    }
    rollbackResult.value = result;
    message.success(result.switched ? t("console.releaseCenter.rollbackOkSwitch") : t("console.releaseCenter.rollbackOkNoSwitch"));
    await loadReleases();

    if (!isMounted.value) {
      return;
    }
    if (detailVisible.value && detail.value?.releaseId === releaseId) {
      await viewDetail(releaseId);
    }
  } catch (error) {
    message.error((error as Error).message || t("console.releaseCenter.rollbackFailed"));
  }
}

function formatDiffSummary(summary: ReleaseDiffSummary | null) {
  if (!summary) {
    return t("console.releaseCenter.diffEmpty");
  }

  return [
    t("console.releaseCenter.diffAddedKeys", { keys: summary.addedKeys.join(", ") || "-" }),
    t("console.releaseCenter.diffRemovedKeys", { keys: summary.removedKeys.join(", ") || "-" }),
    t("console.releaseCenter.diffChangedKeys", { keys: summary.changedKeys.join(", ") || "-" })
  ].join("\n");
}

function buildRollbackMessage(result: ReleaseRollbackResult) {
  const prevDetail = result.previousReleaseId
    ? `#${result.previousReleaseId} (v${result.previousVersion ?? "-"})`
    : "-";
  const parts = [
    t("console.releaseCenter.rollbackTraceTarget", {
      id: result.targetReleaseId,
      version: result.targetVersion
    }),
    t("console.releaseCenter.rollbackTracePrevious", { detail: prevDetail }),
    t("console.releaseCenter.rollbackTraceRebound", { n: result.reboundRouteCount }),
    t("console.releaseCenter.rollbackTraceResult", { result: result.result })
  ];
  if (result.message) {
    parts.push(t("console.releaseCenter.rollbackTraceNote", { msg: result.message }));
  }

  return parts.join(" | ");
}

function openDebugTrace(releaseId: string) {
  void router.push({
    path: "/console/debug",
    query: { releaseId }
  });
}

function openReleaseDetailByRouteQuery() {
  const releaseId = typeof route.query.releaseId === "string" ? route.query.releaseId.trim() : "";
  if (!releaseId) {
    return;
  }

  void viewDetail(releaseId);
}

onMounted(() => {
  isMounted.value = true;
  void loadReleases();
  openReleaseDetailByRouteQuery();
});

onUnmounted(() => {
  isMounted.value = false;
});

watch(
  () => route.query.releaseId,
  () => {
    openReleaseDetailByRouteQuery();
  }
);
</script>

<style scoped>
.release-center-page {
  padding: 24px;
}

.release-card {
  border-radius: 12px;
}

.rollback-result {
  margin-bottom: 12px;
}

.snapshot-title {
  margin-top: 16px;
}

.snapshot-json {
  max-height: 220px;
  overflow: auto;
  white-space: pre-wrap;
  background: #fafafa;
  border: 1px solid #f0f0f0;
  padding: 12px;
  border-radius: 8px;
}
</style>
