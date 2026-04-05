<template>
  <div style="padding: 24px;">
    <a-card>
      <div class="crud-toolbar">
        <a-space wrap>
          <a-input
            v-model:value="keyword"
            :placeholder="t('assetsPage.searchPlaceholder')"
            allow-clear
            @press-enter="handleSearch"
          />
          <a-button type="primary" @click="handleSearch">{{ t("common.search") }}</a-button>
          <a-button @click="handleReset">{{ t("common.reset") }}</a-button>
        </a-space>
      </div>

      <a-table
        :columns="columns"
        :data-source="dataSource"
        :pagination="pagination"
        :loading="loading"
        :locale="{ emptyText: t('assetsPage.emptyText') }"
        row-key="id"
        @change="onTableChange"
      >
        <template #bodyCell="{ column, text }">
          <template v-if="column.key === 'createdAt' || column.key === 'updatedAt'">
            {{ formatDateTime(text) }}
          </template>
        </template>
      </a-table>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, reactive, ref } from "vue";
import type { TablePaginationConfig } from "ant-design-vue";
import { message } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import { getAssetsPaged } from "@/services/api-system";

interface AssetRow {
  id: string;
  name: string;
  type?: string;
  status?: string;
  createdAt?: string;
  updatedAt?: string;
}

const { t, locale } = useI18n();

const isMounted = ref(false);
const keyword = ref("");
const dataSource = ref<AssetRow[]>([]);
const loading = ref(false);

const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 10,
  total: 0,
  showTotal: (total) => t("crud.totalItems", { total })
});

const columns = computed(() => [
  { title: t("assetsPage.colId"), dataIndex: "id", key: "id", ellipsis: true },
  { title: t("assetsPage.colName"), dataIndex: "name", key: "name" },
  { title: t("assetsPage.colType"), dataIndex: "type", key: "type" },
  { title: t("assetsPage.colStatus"), dataIndex: "status", key: "status" },
  { title: t("assetsPage.colCreatedAt"), dataIndex: "createdAt", key: "createdAt" },
  { title: t("assetsPage.colUpdatedAt"), dataIndex: "updatedAt", key: "updatedAt" }
]);

function formatDateTime(value?: string) {
  if (!value) return "-";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  const loc = locale.value === "en-US" ? "en-US" : "zh-CN";
  return date.toLocaleString(loc, { hour12: false });
}

const fetchData = async () => {
  loading.value = true;
  try {
    const result = await getAssetsPaged({
      pageIndex: pagination.current ?? 1,
      pageSize: pagination.pageSize ?? 10,
      keyword: keyword.value || undefined
    });

    if (!isMounted.value) return;
    dataSource.value = result.items as AssetRow[];
    pagination.total = result.total;
  } catch (error: unknown) {
    message.error(error instanceof Error ? error.message : t("assetsPage.queryFailed"));
  } finally {
    loading.value = false;
  }
};

function handleSearch() {
  pagination.current = 1;
  void fetchData();
}

function handleReset() {
  keyword.value = "";
  handleSearch();
}

function onTableChange(pager: TablePaginationConfig) {
  pagination.current = pager.current ?? 1;
  pagination.pageSize = pager.pageSize ?? 10;
  void fetchData();
}

onMounted(() => {
  isMounted.value = true;
  void fetchData();
});

onUnmounted(() => {
  isMounted.value = false;
});
</script>

<style scoped>
.crud-toolbar {
  margin-bottom: 12px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
}
</style>
