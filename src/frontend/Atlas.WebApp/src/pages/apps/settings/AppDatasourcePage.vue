<template>
  <a-card title="应用数据源（只读）" :loading="loading">
    <template #extra>
      <a-space>
        <LockOutlined />
        <span>绑定后不可更改</span>
      </a-space>
    </template>

    <a-form layout="vertical">
      <a-form-item label="数据源 ID">
        <a-input :value="dataSource?.dataSourceId || '平台默认'" disabled />
      </a-form-item>
      <a-form-item label="数据源名称">
        <a-input :value="dataSource?.name || '平台默认'" disabled />
      </a-form-item>
      <a-form-item label="数据库类型">
        <a-input :value="dataSource?.dbType || '-'" disabled />
      </a-form-item>
      <a-form-item label="最近测试结果">
        <a-tag :color="testColor">{{ testText }}</a-tag>
      </a-form-item>
      <a-form-item label="最近测试时间">
        <a-input :value="dataSource?.lastTestedAt || '-'" disabled />
      </a-form-item>
    </a-form>

    <a-button type="primary" :loading="testing" @click="handleTest">重新测试连接</a-button>
  </a-card>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useRoute } from "vue-router";
import { message } from "ant-design-vue";
import { LockOutlined } from "@ant-design/icons-vue";
import type { AppDataSourceView } from "@/types/lowcode";
import { getAppDatasource, testAppDatasource } from "@/services/lowcode";

const route = useRoute();
const appId = route.params.appId as string;

const loading = ref(false);
const testing = ref(false);
const dataSource = ref<AppDataSourceView>();

const testText = computed(() => {
  if (dataSource.value?.lastTestSuccess === true) return "连接正常";
  if (dataSource.value?.lastTestSuccess === false) return "连接异常";
  return "未测试";
});
const testColor = computed(() => {
  if (dataSource.value?.lastTestSuccess === true) return "green";
  if (dataSource.value?.lastTestSuccess === false) return "red";
  return "default";
});

const loadDatasource = async () => {
  loading.value = true;
  try {
    dataSource.value = await getAppDatasource(appId);
  } catch (error) {
    message.error((error as Error).message || "加载数据源失败");
  } finally {
    loading.value = false;
  }
};

const handleTest = async () => {
  testing.value = true;
  try {
    const result = await testAppDatasource(appId);
    if (result.success) {
      message.success("连接测试成功");
    } else {
      message.error(result.errorMessage || "连接测试失败");
    }
    await loadDatasource();
  } catch (error) {
    message.error((error as Error).message || "连接测试失败");
  } finally {
    testing.value = false;
  }
};

onMounted(loadDatasource);
</script>
