<template>
  <div class="transform-designer">
    <a-card size="small" :title="t('dynamicDesigner.transformDesign')">
      <template #extra>
        <a-button @click="loadJobs">{{ t("common.refresh") }}</a-button>
      </template>
      <a-table :data-source="jobs" :columns="columns" row-key="id" :loading="loading" size="small" :pagination="false">
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'enabled'">
            <a-tag :color="record.enabled ? 'green' : 'default'">
              {{ record.enabled ? t("common.statusEnabled") : t("common.statusDisabled") }}
            </a-tag>
          </template>
          <template v-if="column.key === 'actions'">
            <a-space :size="8">
              <a-button type="link" size="small" @click="runJob(record.jobKey)">{{ t("dynamicDesigner.runNow") }}</a-button>
              <a-button v-if="record.enabled" type="link" size="small" @click="pauseJob(record.jobKey)">
                {{ t("dynamicDesigner.pause") }}
              </a-button>
              <a-button v-else type="link" size="small" @click="resumeJob(record.jobKey)">
                {{ t("dynamicDesigner.resume") }}
              </a-button>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { message } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import {
  listDynamicTransformJobs,
  pauseDynamicTransformJob,
  resumeDynamicTransformJob,
  runDynamicTransformJob,
} from "@/services/api-dynamic-views";
import type { DynamicTransformJobDto } from "@/types/dynamic-dataflow";

const { t } = useI18n();
const loading = ref(false);
const jobs = ref<DynamicTransformJobDto[]>([]);

const columns = computed(() => [
  { title: "jobKey", dataIndex: "jobKey", key: "jobKey", width: 180 },
  { title: t("common.name"), dataIndex: "name", key: "name", width: 180 },
  { title: t("common.status"), dataIndex: "status", key: "status", width: 120 },
  { title: t("dynamicDesigner.enabled"), key: "enabled", width: 120 },
  { title: t("dynamicDesigner.lastRun"), dataIndex: "lastRunAt", key: "lastRunAt" },
  { title: t("common.actions"), key: "actions", width: 220 },
]);

const loadJobs = async () => {
  loading.value = true;
  try {
    jobs.value = await listDynamicTransformJobs();
  } catch (error) {
    message.error((error as Error).message || t("dynamicDesigner.loadFailed"));
  } finally {
    loading.value = false;
  }
};

const runJob = async (jobKey: string) => {
  try {
    await runDynamicTransformJob(jobKey);
    message.success(t("common.success"));
    await loadJobs();
  } catch (error) {
    message.error((error as Error).message || t("dynamicDesigner.runFailed"));
  }
};

const pauseJob = async (jobKey: string) => {
  try {
    await pauseDynamicTransformJob(jobKey);
    message.success(t("common.success"));
    await loadJobs();
  } catch (error) {
    message.error((error as Error).message || t("dynamicDesigner.pauseFailed"));
  }
};

const resumeJob = async (jobKey: string) => {
  try {
    await resumeDynamicTransformJob(jobKey);
    message.success(t("common.success"));
    await loadJobs();
  } catch (error) {
    message.error((error as Error).message || t("dynamicDesigner.resumeFailed"));
  }
};

onMounted(() => {
  void loadJobs();
});
</script>

<style scoped>
.transform-designer {
  display: flex;
  flex-direction: column;
  gap: 12px;
}
</style>
