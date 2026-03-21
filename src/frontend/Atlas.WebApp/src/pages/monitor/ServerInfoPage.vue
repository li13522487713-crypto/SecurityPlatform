<template>
  <div class="page-container">
    <div class="page-header">
      <h2 class="page-title">{{ t("monitorServer.title") }}</h2>
      <div class="header-actions">
        <a-tag color="green" v-if="!loading">
          <ClockCircleOutlined /> {{ t("monitorServer.autoRefresh") }}
        </a-tag>
        <a-button :loading="loading" @click="load">{{ t("monitorServer.refreshNow") }}</a-button>
      </div>
    </div>

    <a-spin :spinning="loading">
      <a-row :gutter="[16, 16]" v-if="info">
        <!-- CPU -->
        <a-col :xs="24" :md="12" :lg="6">
          <a-card :title="t('monitorServer.cardCpu')">
            <a-statistic
              :title="t('monitorServer.logicalCores')"
              :value="info.cpu.logicalCores"
              suffix="核"
            />
            <a-progress
              :percent="info.cpu.processCpuUsagePercent"
              :stroke-color="progressColor(info.cpu.processCpuUsagePercent)"
              style="margin-top: 12px"
            />
            <div class="stat-label">{{ t("monitorServer.processCpuUsage") }}</div>
          </a-card>
        </a-col>

        <!-- 内存 -->
        <a-col :xs="24" :md="12" :lg="6">
          <a-card :title="t('monitorServer.cardMemory')">
            <a-statistic
              :title="t('monitorServer.totalMemory')"
              :value="formatBytes(info.memory.totalBytes)"
            />
            <a-progress
              :percent="info.memory.usagePercent"
              :stroke-color="progressColor(info.memory.usagePercent)"
              style="margin-top: 12px"
            />
            <div class="stat-label">
              {{ t("monitorServer.usedAvailable", {
                used: formatBytes(info.memory.usedBytes),
                available: formatBytes(info.memory.availableBytes)
              }) }}
            </div>
          </a-card>
        </a-col>

        <!-- 运行时 -->
        <a-col :xs="24" :md="12" :lg="12">
          <a-card :title="t('monitorServer.runtimeInfo')">
            <a-descriptions :column="2" size="small">
              <a-descriptions-item :label="t('monitorServer.dotnetVersion')">{{ info.runtime.dotNetVersion }}</a-descriptions-item>
              <a-descriptions-item :label="t('monitorServer.osDescription')">{{ info.runtime.osDescription }}</a-descriptions-item>
              <a-descriptions-item :label="t('monitorServer.machineName')">{{ info.runtime.machineName }}</a-descriptions-item>
              <a-descriptions-item :label="t('monitorServer.processId')">{{ info.runtime.processId }}</a-descriptions-item>
              <a-descriptions-item :label="t('monitorServer.threadCount')">{{ info.runtime.threadCount }}</a-descriptions-item>
              <a-descriptions-item :label="t('monitorServer.gcMemory')">{{ formatBytes(info.runtime.gcMemoryBytes) }}</a-descriptions-item>
              <a-descriptions-item :label="t('monitorServer.startedAt')">{{ formatTime(info.runtime.startedAt) }}</a-descriptions-item>
              <a-descriptions-item :label="t('monitorServer.uptime')">{{ info.runtime.uptime }}</a-descriptions-item>
            </a-descriptions>
          </a-card>
        </a-col>

        <!-- 磁盘 -->
        <a-col :span="24">
          <a-card :title="t('monitorServer.diskInfo')">
            <a-row :gutter="[16, 16]">
              <a-col
                v-for="disk in info.disks"
                :key="disk.name"
                :xs="24"
                :sm="12"
                :md="8"
                :lg="6"
              >
                <div class="disk-item">
                  <div class="disk-name">{{ disk.name }}</div>
                  <a-progress
                    :percent="disk.usagePercent"
                    :stroke-color="progressColor(disk.usagePercent)"
                    size="small"
                  />
                  <div class="disk-meta">
                    {{ t("monitorServer.usedTotal", {
                      used: formatBytes(disk.usedBytes),
                      total: formatBytes(disk.totalBytes)
                    }) }}
                  </div>
                </div>
              </a-col>
            </a-row>
          </a-card>
        </a-col>

        <!-- 依赖健康 -->
        <a-col :span="24">
          <a-card :title="t('monitorServer.healthTitle')">
            <div class="health-header">
              <a-tag :color="healthTagColor(healthInfo?.status)">
                {{ healthInfo?.status ?? "未知" }}
              </a-tag>
              <span class="health-checked-at" v-if="healthInfo?.checkedAt">
                {{ t("monitorServer.checkedAt", { time: formatTime(healthInfo.checkedAt) }) }}
              </span>
            </div>
            <a-table
              :columns="healthColumns"
              :data-source="healthInfo?.dependencies ?? []"
              :pagination="false"
              :loading="healthLoading"
              row-key="name"
              size="small"
            >
              <template #bodyCell="{ column, record }">
                <template v-if="column.key === 'healthy'">
                  <a-tag :color="record.healthy ? 'green' : 'red'">
                    {{ record.healthy ? t("monitorServer.healthy") : t("monitorServer.unhealthy") }}
                  </a-tag>
                </template>
              </template>
            </a-table>
          </a-card>
        </a-col>

        <!-- 链路入口 -->
        <a-col :span="24">
          <a-card :title="t('monitorServer.linksTitle')">
            <a-space wrap>
              <a-button @click="openLink('/hangfire')">{{ t("monitorServer.linkHangfire") }}</a-button>
              <a-button @click="openLink('/swagger')">{{ t("monitorServer.linkSwagger") }}</a-button>
              <a-button @click="openLink('/api/v1/health')">{{ t("monitorServer.linkHealthApi") }}</a-button>
              <a-button @click="openLink('/api/v1/scheduled-jobs?pageIndex=1&pageSize=10')">{{ t("monitorServer.linkJobsApi") }}</a-button>
            </a-space>
          </a-card>
        </a-col>
      </a-row>

      <a-empty v-else-if="!loading" :description="t('monitorServer.noData')" />
    </a-spin>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from "vue";

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { message } from "ant-design-vue";
import { ClockCircleOutlined } from "@ant-design/icons-vue";
import { useI18n } from "vue-i18n";
import { requestApi } from "@/services/api-core";
import type { ApiResponse } from "@/types/api";

interface CpuInfo { logicalCores: number; processCpuUsagePercent: number }
interface MemoryInfo { totalBytes: number; usedBytes: number; availableBytes: number; usagePercent: number }
interface DiskInfo { name: string; totalBytes: number; usedBytes: number; availableBytes: number; usagePercent: number }
interface RuntimeInfo { dotNetVersion: string; osDescription: string; machineName: string; processId: number; threadCount: number; gcMemoryBytes: number; startedAt: string; uptime: string }
interface ServerInfo { cpu: CpuInfo; memory: MemoryInfo; disks: DiskInfo[]; runtime: RuntimeInfo }
interface HealthDependencyStatus { name: string; healthy: boolean; message: string }
interface HealthStatusPayload { status: string; checkedAt: string; dependencies: HealthDependencyStatus[] }

const loading = ref(false);
const info = ref<ServerInfo | null>(null);
const healthLoading = ref(false);
const healthInfo = ref<HealthStatusPayload | null>(null);
const { t } = useI18n();
let timer: number | undefined;
const healthColumns = [
  { title: t("monitorServer.dependencyName"), dataIndex: "name", key: "name", width: 180 },
  { title: t("monitorServer.dependencyStatus"), key: "healthy", width: 140 },
  { title: t("monitorServer.dependencyMessage"), dataIndex: "message", key: "message" }
];

const load = async () => {
  loading.value = true;
  try {
    const [serverResponse]  = await Promise.all([
      requestApi<ApiResponse<ServerInfo>>("/monitor/server-info"),
      loadHealth()
    ]);

    if (!isMounted.value) return;
    info.value = serverResponse.data ?? null;
  } catch (e: unknown) {
    message.error(e instanceof Error ? e.message : t("monitorServer.loadFailed"));
  } finally {
    loading.value = false;
  }
};

const loadHealth = async () => {
  healthLoading.value = true;
  try {
    const response  = await requestApi<ApiResponse<HealthStatusPayload>>("/health");

    if (!isMounted.value) return;
    healthInfo.value = response.data ?? null;
  } catch (e: unknown) {
    healthInfo.value = null;
    message.warning(e instanceof Error ? e.message : t("monitorServer.loadFailed"));
  } finally {
    healthLoading.value = false;
  }
};

const formatBytes = (bytes: number) => {
  if (bytes >= 1024 ** 3) return `${(bytes / 1024 ** 3).toFixed(1)} GB`;
  if (bytes >= 1024 ** 2) return `${(bytes / 1024 ** 2).toFixed(1)} MB`;
  if (bytes >= 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${bytes} B`;
};

const progressColor = (percent: number) => {
  if (percent >= 90) return "#ff4d4f";
  if (percent >= 70) return "#faad14";
  return "#52c41a";
};

const formatTime = (iso: string) => {
  try {
    return new Date(iso).toLocaleString("zh-CN");
  } catch {
    return iso;
  }
};

const healthTagColor = (status?: string) => {
  if (!status) return "default";
  if (status === "Healthy") return "green";
  if (status === "Degraded") return "orange";
  return "red";
};

const openLink = (path: string) => {
  const url = `${window.location.origin}${path}`;
  window.open(url, "_blank", "noopener,noreferrer");
};

onMounted(() => {
  load();
  timer = window.setInterval(load, 30_000);
});

onUnmounted(() => {
  window.clearInterval(timer);
});
</script>

<style scoped>
.page-container {
  padding: 24px;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 20px;
}

.page-title {
  margin: 0;
  font-size: 18px;
  font-weight: 600;
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

.stat-label {
  font-size: 12px;
  color: #999;
  margin-top: 4px;
}

.disk-item {
  padding: 8px;
  border: 1px solid #f0f0f0;
  border-radius: 6px;
}

.disk-name {
  font-weight: 500;
  margin-bottom: 8px;
}

.disk-meta {
  font-size: 12px;
  color: #666;
  margin-top: 4px;
}

.health-header {
  display: flex;
  align-items: center;
  gap: 12px;
  margin-bottom: 12px;
}

.health-checked-at {
  color: #8c8c8c;
  font-size: 12px;
}
</style>
