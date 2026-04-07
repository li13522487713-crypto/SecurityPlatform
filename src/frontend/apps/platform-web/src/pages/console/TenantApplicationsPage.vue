<template>
  <div class="tenant-applications-page" data-testid="e2e-console-tenant-applications-page">
    <a-card :bordered="false" class="tenant-app-card">
      <template #title>{{ t("console.tenantApps.title") }}</template>
      <template #extra>
        <a-space wrap>
          <a-select
            v-model:value="selectedStatus"
            allow-clear
            :placeholder="t('console.tenantApps.phStatus')"
            style="width: 140px"
            :options="statusOptions"
          />
          <a-input-search
            v-model:value="keyword"
            allow-clear
            :placeholder="t('console.tenantApps.phSearch')"
            style="width: 260px"
            @search="handleSearch"
          />
          <a-button @click="resetFilters">{{ t("console.tenantApps.reset") }}</a-button>
        </a-space>
      </template>

      <a-table
        row-key="id"
        :loading="loading"
        :columns="columns"
        :data-source="rows"
        :pagination="pagination"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'status'">
            <a-tag :color="record.status === 'Active' ? 'processing' : 'default'">
              {{ statusLabel(record.status) }}
            </a-tag>
          </template>
          <template v-if="column.key === 'openedAt'">
            {{ formatDate(record.openedAt) }}
          </template>
          <template v-if="column.key === 'actions'">
            <a-space>
              <a-button type="link" size="small" @click="viewDetail(record.id)">{{ t("console.tenantApps.view") }}</a-button>
              <a-button type="link" size="small" :disabled="!record.appKey" @click="openAppRuntime(record.appKey)">
                {{ t("console.tenantApps.openRuntime") }}
              </a-button>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <a-drawer
      v-model:open="detailVisible"
      :title="t('console.tenantApps.drawerTitle')"
      width="680"
      :destroy-on-close="true"
    >
      <template #extra>
        <a-button
          type="primary"
          ghost
          size="small"
          :disabled="!detail?.appKey"
          @click="openAppRuntime(detail?.appKey)"
        >
          {{ t("console.tenantApps.openRuntime") }}
        </a-button>
      </template>
      <a-descriptions :column="2" bordered size="small">
        <a-descriptions-item :label="t('console.catalog.labelId')">{{ detail?.id || "-" }}</a-descriptions-item>
        <a-descriptions-item :label="t('console.tenantApps.labelCatalogId')">{{
          detail?.applicationCatalogId || "-"
        }}</a-descriptions-item>
        <a-descriptions-item :label="t('console.tenantApps.labelCatalogName')">{{
          detail?.applicationCatalogName || "-"
        }}</a-descriptions-item>
        <a-descriptions-item :label="t('console.tenantApps.labelInstanceId')">{{
          detail?.tenantAppInstanceId || "-"
        }}</a-descriptions-item>
        <a-descriptions-item :label="t('console.tenantApps.labelAppKey')">{{ detail?.appKey || "-" }}</a-descriptions-item>
        <a-descriptions-item :label="t('console.tenantApps.labelAppName')">{{ detail?.name || "-" }}</a-descriptions-item>
        <a-descriptions-item :label="t('console.tenantApps.labelStatus')">{{
          detail?.status ? statusLabel(detail.status) : "-"
        }}</a-descriptions-item>
        <a-descriptions-item :label="t('console.tenantApps.labelOpenedAt')">{{ formatDate(detail?.openedAt) }}</a-descriptions-item>
        <a-descriptions-item :label="t('console.tenantApps.labelUpdatedAt')">{{ formatDate(detail?.updatedAt) }}</a-descriptions-item>
        <a-descriptions-item :label="t('console.tenantApps.labelDataSourceId')" :span="2">{{
          detail?.dataSourceId || "-"
        }}</a-descriptions-item>
      </a-descriptions>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import type { TableColumnsType, TablePaginationConfig } from "ant-design-vue";
import { message } from "ant-design-vue";
import { getTenantApplicationDetail, getTenantApplicationsPaged } from "@/services/api-console";
import type { TenantApplicationDetail, TenantApplicationListItem } from "@/types/platform-console";

const { t } = useI18n();

const isMounted = ref(false);
onMounted(() => {
  isMounted.value = true;
});
onUnmounted(() => {
  isMounted.value = false;
});

const loading = ref(false);
const keyword = ref("");
const selectedStatus = ref<string>();
const rows = ref<TenantApplicationListItem[]>([]);
const detail = ref<TenantApplicationDetail | null>(null);
const detailVisible = ref(false);
const pageIndex = ref(1);
const pageSize = ref(10);

const columns = computed<TableColumnsType<TenantApplicationListItem>>(() => [
  { title: t("console.tenantApps.colCatalogName"), dataIndex: "applicationCatalogName", key: "applicationCatalogName", width: 180 },
  { title: t("console.tenantApps.labelAppKey"), dataIndex: "appKey", key: "appKey", width: 170 },
  { title: t("console.tenantApps.colAppName"), dataIndex: "name", key: "name", width: 170 },
  { title: t("console.tenantApps.colOpenedAt"), dataIndex: "openedAt", key: "openedAt", width: 180 },
  { title: t("console.tenantApps.colStatus"), dataIndex: "status", key: "status", width: 120 },
  { title: t("console.tenantApps.colActions"), key: "actions", width: 100, fixed: "right" }
]);

const pagination = ref<TablePaginationConfig>({
  current: 1,
  pageSize: 10,
  total: 0,
  showSizeChanger: true,
  showTotal: (all) => t("crud.totalItems", { total: all })
});

function resolveAppWebOrigin(): string {
  const configured = String(import.meta.env.VITE_APP_WEB_ORIGIN ?? "").trim();
  if (configured) return configured;
  if (typeof window === "undefined") return "http://localhost:5181";
  const current = new URL(window.location.origin);
  if (current.port === "5180") {
    current.port = "5181";
  }
  return current.origin;
}

const statusOptions = computed(() => [
  { label: t("console.tenantApps.statusProvisioning"), value: "Provisioning" },
  { label: t("console.tenantApps.statusActive"), value: "Active" },
  { label: t("console.tenantApps.statusDisabled"), value: "Disabled" },
  { label: t("console.tenantApps.statusArchived"), value: "Archived" }
]);

function statusLabel(code: string): string {
  const map: Record<string, string> = {
    Provisioning: t("console.tenantApps.statusProvisioning"),
    Active: t("console.tenantApps.statusActive"),
    Disabled: t("console.tenantApps.statusDisabled"),
    Archived: t("console.tenantApps.statusArchived")
  };
  return map[code] ?? code;
}

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

async function loadTenantApplications() {
  loading.value = true;
  try {
    const result = await getTenantApplicationsPaged({
      pageIndex: pageIndex.value,
      pageSize: pageSize.value,
      keyword: keyword.value || undefined,
      status: selectedStatus.value
    });

    if (!isMounted.value) return;
    rows.value = result.items;
    pagination.value = {
      ...pagination.value,
      current: result.pageIndex,
      pageSize: result.pageSize,
      total: result.total
    };
  } catch (error) {
    message.error((error as Error).message || t("console.tenantApps.loadFailed"));
  } finally {
    loading.value = false;
  }
}

function handleSearch() {
  pageIndex.value = 1;
  void loadTenantApplications();
}

function resetFilters() {
  keyword.value = "";
  selectedStatus.value = undefined;
  pageIndex.value = 1;
  void loadTenantApplications();
}

function handleTableChange(page: TablePaginationConfig) {
  pageIndex.value = page.current ?? 1;
  pageSize.value = page.pageSize ?? 10;
  void loadTenantApplications();
}

function openAppRuntime(appKey?: string) {
  if (!appKey) return;
  const targetUrl = `${resolveAppWebOrigin()}/apps/${encodeURIComponent(appKey)}/entry?from=platform`;
  window.open(targetUrl, "_blank", "noopener,noreferrer");
}

async function viewDetail(id: string) {
  try {
    detail.value = await getTenantApplicationDetail(id);

    if (!isMounted.value) return;
    detailVisible.value = true;
  } catch (error) {
    message.error((error as Error).message || t("console.tenantApps.loadDetailFailed"));
  }
}

onMounted(() => {
  void loadTenantApplications();
});
</script>

<style scoped>
.tenant-applications-page {
  padding: 24px;
}

.tenant-app-card {
  border-radius: 12px;
}
</style>
