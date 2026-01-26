<template>
  <a-card title="告警模块" class="page-card">
    <a-table
      :columns="columns"
      :data-source="dataSource"
      :pagination="pagination"
      :loading="loading"
      row-key="id"
      @change="onTableChange"
    />
  </a-card>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from "vue";
import { getAlertsPaged } from "@/services/api";
import type { TablePaginationConfig } from "ant-design-vue";

interface AlertRow {
  id: string;
  title: string;
  createdAt: string;
}

const columns = [
  { title: "告警ID", dataIndex: "id" },
  { title: "标题", dataIndex: "title" },
  { title: "创建时间", dataIndex: "createdAt" }
];

const dataSource = ref<AlertRow[]>([]);
const loading = ref(false);
const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 10,
  total: 0,
  showTotal: (total) => `共 ${total} 条`
});

const fetchData = async () => {
  loading.value = true;
  try {
    const result = await getAlertsPaged({
      pageIndex: pagination.current ?? 1,
      pageSize: pagination.pageSize ?? 10
    });
    dataSource.value = result.items;
    pagination.total = result.total;
  } finally {
    loading.value = false;
  }
};

const onTableChange = (pager: TablePaginationConfig) => {
  pagination.current = pager.current;
  pagination.pageSize = pager.pageSize;
  fetchData();
};

onMounted(fetchData);
</script>