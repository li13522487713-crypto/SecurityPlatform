<template>
  <div class="application-catalog-page" data-testid="e2e-console-application-catalog-page">
    <a-card :bordered="false" class="catalog-card">
      <template #title>{{ t("console.catalog.title") }}</template>
      <template #extra>
        <a-space wrap>
          <a-select
            v-model:value="selectedStatus"
            allow-clear
            :placeholder="t('console.catalog.phStatus')"
            style="width: 140px"
            :options="statusOptions"
          />
          <a-input
            v-model:value="categoryFilter"
            allow-clear
            :placeholder="t('console.catalog.phCategory')"
            style="width: 160px"
          />
          <a-input
            v-model:value="appKeyFilter"
            allow-clear
            :placeholder="t('console.catalog.phAppKey')"
            style="width: 180px"
          />
          <a-input-search
            v-model:value="keyword"
            allow-clear
            :placeholder="t('console.catalog.phSearch')"
            style="width: 260px"
            @search="handleSearch"
          />
          <a-button @click="resetFilters">{{ t("console.catalog.reset") }}</a-button>
        </a-space>
      </template>

      <a-table
        row-key="id"
        :loading="loading"
        :columns="columns"
        :data-source="rows"
        :pagination="pagination"
        :scroll="{ x: 1400 }"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'status'">
            <a-tag :color="record.status === 'Published' ? 'processing' : 'default'">
              {{ statusLabel(record.status) }}
            </a-tag>
          </template>
          <template v-if="column.key === 'publishedAt'">
            {{ formatDate(record.publishedAt) }}
          </template>
          <template v-if="column.key === 'actions'">
            <a-space>
              <a-button type="link" size="small" @click="viewDetail(record.id)">{{ t("console.catalog.view") }}</a-button>
              <a-button type="link" size="small" :disabled="!canEditCatalog" @click="openEditCatalog(record)">
                {{ t("console.catalog.edit") }}
              </a-button>
              <a-button
                type="link"
                size="small"
                :disabled="record.isBound || !canEditCatalog"
                @click="openEditDataSource(record)"
              >
                {{ t("console.catalog.editDataSource") }}
              </a-button>
              <a-button type="link" size="small" :disabled="!canEditCatalog" @click="openPublishCatalog(record)">
                {{ t("console.catalog.publish") }}
              </a-button>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <a-drawer
      v-model:open="detailVisible"
      :title="t('console.catalog.drawerTitle')"
      width="640"
      :destroy-on-close="true"
    >
      <a-descriptions :column="2" bordered size="small">
        <a-descriptions-item :label="t('console.catalog.labelId')">{{ detail?.id || "-" }}</a-descriptions-item>
        <a-descriptions-item :label="t('console.catalog.labelCatalogKey')">{{ detail?.catalogKey || "-" }}</a-descriptions-item>
        <a-descriptions-item :label="t('console.catalog.labelName')">{{ detail?.name || "-" }}</a-descriptions-item>
        <a-descriptions-item :label="t('console.catalog.labelVersion')">{{ detail?.version ?? "-" }}</a-descriptions-item>
        <a-descriptions-item :label="t('console.catalog.labelStatus')">{{
          detail?.status ? statusLabel(detail.status) : "-"
        }}</a-descriptions-item>
        <a-descriptions-item :label="t('console.catalog.labelCategory')">{{ detail?.category || "-" }}</a-descriptions-item>
        <a-descriptions-item :label="t('console.catalog.labelPublishedAt')" :span="2">
          {{ formatDate(detail?.publishedAt) }}
        </a-descriptions-item>
        <a-descriptions-item :label="t('console.catalog.labelDataSourceId')" :span="2">
          {{ detail?.dataSourceId || "-" }}
        </a-descriptions-item>
        <a-descriptions-item :label="t('console.catalog.labelDescription')" :span="2">
          {{ detail?.description || "-" }}
        </a-descriptions-item>
      </a-descriptions>
    </a-drawer>

    <a-modal
      v-model:open="editVisible"
      :title="t('console.catalog.editDataSourceTitle')"
      :confirm-loading="savingEdit"
      @ok="submitEditDataSource"
    >
      <a-alert
        type="info"
        show-icon
        style="margin-bottom: 12px"
        :message="t('console.catalog.editDataSourceHint')"
      />
      <a-form layout="vertical">
        <a-form-item :label="t('console.catalog.labelDataSourceId')">
          <a-select
            v-model:value="editingDataSourceId"
            show-search
            allow-clear
            :filter-option="filterDataSourceOption"
            :options="dataSourceOptions"
            :placeholder="t('console.catalog.selectDataSourcePlaceholder')"
          />
        </a-form-item>
      </a-form>
    </a-modal>

    <a-modal
      v-model:open="editCatalogVisible"
      :title="t('console.catalog.editTitle')"
      :confirm-loading="savingCatalog"
      @ok="submitEditCatalog"
    >
      <a-form layout="vertical">
        <a-form-item :label="t('console.catalog.labelName')" required>
          <a-input v-model:value="editCatalogForm.name" />
        </a-form-item>
        <a-form-item :label="t('console.catalog.labelCategory')">
          <a-input v-model:value="editCatalogForm.category" />
        </a-form-item>
        <a-form-item :label="t('console.catalog.labelDescription')">
          <a-textarea v-model:value="editCatalogForm.description" :rows="3" />
        </a-form-item>
        <a-form-item :label="t('console.catalog.labelIcon')">
          <a-input v-model:value="editCatalogForm.icon" />
        </a-form-item>
      </a-form>
    </a-modal>

    <a-modal
      v-model:open="publishVisible"
      :title="t('console.catalog.publishDialogTitle')"
      :confirm-loading="publishingCatalog"
      @ok="submitPublishCatalog"
    >
      <a-form layout="vertical">
        <a-form-item :label="t('console.catalog.publishNoteLabel')">
          <a-textarea
            v-model:value="publishReleaseNote"
            :rows="3"
            :maxlength="200"
            :placeholder="t('console.catalog.publishNotePlaceholder')"
            show-count
          />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import type { TableColumnsType, TablePaginationConfig } from "ant-design-vue";
import { message } from "ant-design-vue";
import { getAuthProfile, hasPermission } from "@atlas/shared-core";
import { getTenantDataSources } from "@/services/api-system";
import {
  getApplicationCatalogDetail,
  getApplicationCatalogsPaged,
  publishApplicationCatalog,
  updateApplicationCatalog,
  updateApplicationCatalogDataSource
} from "@/services/api-console";
import type { ApplicationCatalogDetail, ApplicationCatalogListItem } from "@/types/platform-console";

const { t } = useI18n();

const isMounted = ref(false);
onMounted(() => {
  isMounted.value = true;
});
onUnmounted(() => {
  isMounted.value = false;
});

const profile = getAuthProfile();
const canEditCatalog = computed(() => hasPermission(profile, "apps:update"));

const loading = ref(false);
const savingEdit = ref(false);
const savingCatalog = ref(false);
const publishingCatalog = ref(false);
const keyword = ref("");
const selectedStatus = ref<string>();
const categoryFilter = ref("");
const appKeyFilter = ref("");
const rows = ref<ApplicationCatalogListItem[]>([]);
const detail = ref<ApplicationCatalogDetail | null>(null);
const detailVisible = ref(false);
const editVisible = ref(false);
const editCatalogVisible = ref(false);
const publishVisible = ref(false);
const editingCatalogId = ref<string>("");
const editingCatalogRecord = ref<ApplicationCatalogListItem | null>(null);
const publishingCatalogRecord = ref<ApplicationCatalogListItem | null>(null);
const publishReleaseNote = ref("");
const editingDataSourceId = ref<string | number>();
const dataSourceOptions = ref<Array<{ label: string; value: string }>>([]);
const editCatalogForm = ref({
  name: "",
  category: "",
  description: "",
  icon: ""
});
const pageIndex = ref(1);
const pageSize = ref(10);

const columns = computed<TableColumnsType<ApplicationCatalogListItem>>(() => [
  { title: t("console.catalog.labelName"), dataIndex: "name", key: "name", width: 180 },
  { title: t("console.catalog.labelCatalogKey"), dataIndex: "catalogKey", key: "catalogKey", width: 180 },
  { title: t("console.catalog.labelVersion"), dataIndex: "version", key: "version", width: 100 },
  { title: t("console.catalog.labelStatus"), dataIndex: "status", key: "status", width: 120 },
  { title: t("console.catalog.labelCategory"), dataIndex: "category", key: "category", width: 140 },
  { title: t("console.catalog.labelPublishedAt"), dataIndex: "publishedAt", key: "publishedAt", width: 180 },
  { title: t("console.resourceCenter.colActions"), key: "actions", width: 320, fixed: "right" }
]);

const pagination = ref<TablePaginationConfig>({
  current: 1,
  pageSize: 10,
  total: 0,
  showSizeChanger: true,
  showTotal: (all) => t("crud.totalItems", { total: all })
});

const statusOptions = computed(() => [
  { label: t("console.catalog.statusDraft"), value: "Draft" },
  { label: t("console.catalog.statusPublished"), value: "Published" },
  { label: t("console.catalog.statusDisabled"), value: "Disabled" },
  { label: t("console.catalog.statusArchived"), value: "Archived" }
]);

function statusLabel(code: string): string {
  const map: Record<string, string> = {
    Draft: t("console.catalog.statusDraft"),
    Published: t("console.catalog.statusPublished"),
    Disabled: t("console.catalog.statusDisabled"),
    Archived: t("console.catalog.statusArchived")
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

async function loadCatalogs() {
  loading.value = true;
  try {
    const result = await getApplicationCatalogsPaged({
      pageIndex: pageIndex.value,
      pageSize: pageSize.value,
      keyword: keyword.value || undefined,
      status: selectedStatus.value,
      category: categoryFilter.value || undefined,
      appKey: appKeyFilter.value || undefined
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
    message.error((error as Error).message || t("console.catalog.loadFailed"));
  } finally {
    loading.value = false;
  }
}

function handleSearch() {
  pageIndex.value = 1;
  void loadCatalogs();
}

function resetFilters() {
  keyword.value = "";
  selectedStatus.value = undefined;
  categoryFilter.value = "";
  appKeyFilter.value = "";
  pageIndex.value = 1;
  void loadCatalogs();
}

function handleTableChange(page: TablePaginationConfig) {
  pageIndex.value = page.current ?? 1;
  pageSize.value = page.pageSize ?? 10;
  void loadCatalogs();
}

function filterDataSourceOption(input: string, option: { label?: string; value?: string } | undefined): boolean {
  const label = String(option?.label ?? "").toLowerCase();
  return label.includes(input.trim().toLowerCase());
}

async function loadDataSourceOptions() {
  try {
    const list = await getTenantDataSources();
    dataSourceOptions.value = list
      .filter((item) => item.isActive)
      .map((item) => ({
        value: String(item.id),
        label: `${item.name} (${item.dbType}) #${item.id}`
      }));
  } catch (error) {
    message.error((error as Error).message || t("console.catalog.loadDataSourcesFailed"));
  }
}

async function viewDetail(id: string) {
  try {
    detail.value = await getApplicationCatalogDetail(id);

    if (!isMounted.value) return;
    detailVisible.value = true;
  } catch (error) {
    message.error((error as Error).message || t("console.catalog.loadDetailFailed"));
  }
}

async function openEditDataSource(record: ApplicationCatalogListItem) {
  editingCatalogId.value = record.id;
  editingDataSourceId.value = undefined;
  editVisible.value = true;
  try {
    const current = await getApplicationCatalogDetail(record.id);
    if (!isMounted.value || editingCatalogId.value !== record.id || !editVisible.value) {
      return;
    }
    editingDataSourceId.value = current.dataSourceId ? String(current.dataSourceId) : undefined;
  } catch {
    // 回显失败时仅保留可编辑状态，不中断用户继续选择新数据源。
  }
}

function openEditCatalog(record: ApplicationCatalogListItem) {
  editingCatalogRecord.value = record;
  editCatalogForm.value = {
    name: record.name ?? "",
    category: record.category ?? "",
    description: record.description ?? "",
    icon: record.icon ?? ""
  };
  editCatalogVisible.value = true;
}

async function submitEditDataSource() {
  if (!editingCatalogId.value) {
    return;
  }
  const selected = String(editingDataSourceId.value ?? "").trim();
  if (!selected) {
    message.warning(t("console.catalog.selectDataSourceRequired"));
    return;
  }

  savingEdit.value = true;
  try {
    await updateApplicationCatalogDataSource(editingCatalogId.value, selected);
    message.success(t("console.catalog.editDataSourceSuccess"));
    editVisible.value = false;
    await loadCatalogs();
  } catch (error) {
    message.error((error as Error).message || t("console.catalog.editDataSourceFailed"));
  } finally {
    savingEdit.value = false;
  }
}

async function submitEditCatalog() {
  if (!editingCatalogRecord.value) {
    return;
  }
  if (!editCatalogForm.value.name.trim()) {
    message.warning(t("console.catalog.nameRequired"));
    return;
  }

  savingCatalog.value = true;
  try {
    await updateApplicationCatalog(editingCatalogRecord.value.id, {
      name: editCatalogForm.value.name.trim(),
      category: editCatalogForm.value.category.trim() || undefined,
      description: editCatalogForm.value.description.trim() || undefined,
      icon: editCatalogForm.value.icon.trim() || undefined
    });
    message.success(t("console.catalog.editSuccess"));
    editCatalogVisible.value = false;
    await loadCatalogs();
  } catch (error) {
    message.error((error as Error).message || t("console.catalog.editFailed"));
  } finally {
    savingCatalog.value = false;
  }
}

function openPublishCatalog(record: ApplicationCatalogListItem) {
  publishingCatalogRecord.value = record;
  publishReleaseNote.value = "";
  publishVisible.value = true;
}

async function submitPublishCatalog() {
  if (!publishingCatalogRecord.value) {
    return;
  }

  publishingCatalog.value = true;
  try {
    await publishApplicationCatalog(publishingCatalogRecord.value.id, publishReleaseNote.value.trim() || undefined);
    message.success(t("console.catalog.publishSuccess"));
    publishVisible.value = false;
    await loadCatalogs();
  } catch (error) {
    message.error((error as Error).message || t("console.catalog.publishFailed"));
  } finally {
    publishingCatalog.value = false;
  }
}

onMounted(() => {
  void loadDataSourceOptions();
  void loadCatalogs();
});
</script>

<style scoped>
.application-catalog-page {
  padding: 24px;
}

.catalog-card {
  border-radius: 12px;
}
</style>
