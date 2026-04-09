<template>
  <section class="appbridge-page">
    <h2>暴露数据浏览器</h2>
    <a-form layout="inline">
      <a-form-item label="实例ID">
        <a-input v-model:value="appInstanceId" style="width: 140px;" />
      </a-form-item>
      <a-form-item label="数据集">
        <a-select v-model:value="dataSet" style="width: 180px;" :options="datasetOptions" />
      </a-form-item>
      <a-form-item label="关键字">
        <a-input v-model:value="keyword" style="width: 220px;" />
      </a-form-item>
      <a-button type="primary" :loading="loading" @click="search">查询</a-button>
    </a-form>

    <a-table
      row-key="__rowKey"
      :columns="columns"
      :data-source="rows"
      :loading="loading"
      :pagination="pagination"
      @change="onTableChange"
    />
  </section>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";
import type { TableColumnsType } from "ant-design-vue";
import type { PagedRequest } from "@atlas/shared-core";
import { queryExposedData } from "@/services/api-appbridge";

type RowRecord = Record<string, unknown> & { __rowKey: string };

const appInstanceId = ref("1");
const dataSet = ref("users");
const keyword = ref("");
const loading = ref(false);
const rows = ref<RowRecord[]>([]);
const total = ref(0);
const request = ref<PagedRequest>({
  pageIndex: 1,
  pageSize: 10,
  keyword: "",
  sortBy: "",
  sortDesc: false
});

const datasetOptions = [
  { label: "users", value: "users" },
  { label: "departments", value: "departments" },
  { label: "positions", value: "positions" },
  { label: "projects", value: "projects" }
];

const columns = computed<TableColumnsType<RowRecord>>(() => {
  if (rows.value.length === 0) {
    return [{ title: "数据", dataIndex: "__rowKey", key: "__rowKey" }];
  }

  const sample = rows.value[0]!;
  return Object.keys(sample)
    .filter((key) => key !== "__rowKey")
    .map((key) => ({
      title: key,
      dataIndex: key,
      key
    }));
});

const pagination = computed(() => ({
  current: request.value.pageIndex,
  pageSize: request.value.pageSize,
  total: total.value,
  showSizeChanger: true
}));

async function search() {
  loading.value = true;
  try {
    const payload: PagedRequest = {
      ...request.value,
      keyword: keyword.value
    };
    const data = await queryExposedData(appInstanceId.value, dataSet.value, payload);
    const list = data.result.items;
    rows.value = list.map((item, index) => ({
      __rowKey: `${request.value.pageIndex}-${index}`,
      ...item
    }));
    total.value = data.result.total;
  } finally {
    loading.value = false;
  }
}

function onTableChange(page: { current?: number; pageSize?: number }) {
  request.value = {
    ...request.value,
    pageIndex: page.current ?? 1,
    pageSize: page.pageSize ?? 10
  };
  void search();
}
</script>

<style scoped>
.appbridge-page {
  display: flex;
  flex-direction: column;
  gap: 12px;
}
</style>
