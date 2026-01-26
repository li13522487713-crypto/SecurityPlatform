<template>
  <a-card title="资产模块" class="page-card">
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
import { getAssetsPaged } from "@/services/api";
import type { TablePaginationConfig } from "ant-design-vue";

interface AssetRow {
  id: string;
  name: string;
}

const columns = [
  { title: "资产ID", dataIndex: "id" },
  { title: "资产名称", dataIndex: "name" }
];

const dataSource = ref<AssetRow[]>([]);
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
    const result = await getAssetsPaged({
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