<template>
  <a-card class="page-card">
    <div class="crud-toolbar">
      <a-space wrap>
        <a-input
          v-model:value="keyword"
          :placeholder="t('pages.assets.searchPlaceholder')"
          allow-clear
          @press-enter="handleSearch"
        />
        <a-button @click="handleSearch">{{ t("common.search") }}</a-button>
        <a-button @click="handleReset">{{ t("common.reset") }}</a-button>
      </a-space>
    </div>

    <a-table
      :columns="columns"
      :data-source="dataSource"
      :pagination="pagination"
      :loading="loading"
      :locale="{ emptyText: t('pages.assets.emptyText') }"
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
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, onUnmounted } from "vue";
import { useI18n } from "vue-i18n";

const isMounted = ref(false);
onMounted(() => {
  isMounted.value = true;
});
onUnmounted(() => {
  isMounted.value = false;
});

import { getAssetsPaged } from "@/services/api";
import type { TablePaginationConfig } from "ant-design-vue";
import { message } from "ant-design-vue";
import { formatDateTime } from "@/utils/common";

const { t } = useI18n();

interface AssetRow {
  id: string;
  name: string;
  type?: string;
  status?: string;
  createdAt?: string;
  updatedAt?: string;
}

const columns = computed(() => [
  { title: t("pages.assets.colId"), dataIndex: "id", key: "id", ellipsis: true },
  { title: t("pages.assets.colName"), dataIndex: "name", key: "name" },
  { title: t("pages.assets.colType"), dataIndex: "type", key: "type" },
  { title: t("pages.assets.colStatus"), dataIndex: "status", key: "status" },
  { title: t("pages.assets.colCreatedAt"), dataIndex: "createdAt", key: "createdAt" },
  { title: t("pages.assets.colUpdatedAt"), dataIndex: "updatedAt", key: "updatedAt" }
]);

const keyword = ref("");
const dataSource = ref<AssetRow[]>([]);
const loading = ref(false);
const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 10,
  total: 0,
  showTotal: (total) => t("crud.totalItems", { total })
});

const fetchData = async () => {
  loading.value = true;
  try {
    const result = await getAssetsPaged({
      pageIndex: pagination.current ?? 1,
      pageSize: pagination.pageSize ?? 10,
      keyword: keyword.value || undefined
    });

    if (!isMounted.value) return;
    dataSource.value = result.items;
    pagination.total = result.total;
  } catch (error) {
    message.error((error as Error).message || t("pages.assets.queryFailed"));
  } finally {
    loading.value = false;
  }
};

const handleSearch = () => {
  pagination.current = 1;
  fetchData();
};

const handleReset = () => {
  keyword.value = "";
  handleSearch();
};

const onTableChange = (pager: TablePaginationConfig) => {
  pagination.current = pager.current;
  pagination.pageSize = pager.pageSize;
  fetchData();
};

onMounted(fetchData);
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
