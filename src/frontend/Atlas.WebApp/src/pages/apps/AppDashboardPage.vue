<template>
  <a-row :gutter="16">
    <a-col :span="12">
      <a-card title="应用信息" :loading="loading">
        <a-descriptions :column="1" size="small">
          <a-descriptions-item label="应用名称">{{ appDetail?.name || "-" }}</a-descriptions-item>
          <a-descriptions-item label="应用标识">{{ appDetail?.appKey || "-" }}</a-descriptions-item>
          <a-descriptions-item label="状态">{{ appDetail?.status || "-" }}</a-descriptions-item>
          <a-descriptions-item label="版本">{{ appDetail?.version || "-" }}</a-descriptions-item>
        </a-descriptions>
      </a-card>
    </a-col>

    <a-col :span="12">
      <a-card title="数据源状态" :loading="loading">
        <a-descriptions :column="1" size="small">
          <a-descriptions-item label="数据源名称">{{ dataSource?.name || "平台默认" }}</a-descriptions-item>
          <a-descriptions-item label="数据库类型">{{ dataSource?.dbType || "-" }}</a-descriptions-item>
          <a-descriptions-item label="最近测试">
            <a-tag :color="testStatusColor">{{ testStatusText }}</a-tag>
          </a-descriptions-item>
          <a-descriptions-item label="测试时间">{{ dataSource?.lastTestedAt || "-" }}</a-descriptions-item>
        </a-descriptions>
      </a-card>
    </a-col>
  </a-row>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useRoute } from "vue-router";
import type { AppDataSourceView, LowCodeAppDetail } from "@/types/lowcode";
import { getAppDatasource, getLowCodeAppDetail } from "@/services/lowcode";

const route = useRoute();
const appId = route.params.appId as string;

const loading = ref(false);
const appDetail = ref<LowCodeAppDetail>();
const dataSource = ref<AppDataSourceView>();

const testStatusText = computed(() => {
  if (dataSource.value?.lastTestSuccess === true) return "正常";
  if (dataSource.value?.lastTestSuccess === false) return "异常";
  return "未测试";
});
const testStatusColor = computed(() => {
  if (dataSource.value?.lastTestSuccess === true) return "green";
  if (dataSource.value?.lastTestSuccess === false) return "red";
  return "default";
});

const loadData = async () => {
  loading.value = true;
  try {
    const [detail, datasource] = await Promise.all([
      getLowCodeAppDetail(appId),
      getAppDatasource(appId)
    ]);
    appDetail.value = detail;
    dataSource.value = datasource;
  } finally {
    loading.value = false;
  }
};

onMounted(loadData);
</script>
