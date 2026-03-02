<template>
  <div class="page-container">
    <div class="page-header">
      <h2 class="page-title">服务监控</h2>
      <div class="header-actions">
        <a-tag color="green" v-if="!loading">
          <ClockCircleOutlined /> 每 30 秒自动刷新
        </a-tag>
        <a-button :loading="loading" @click="load">立即刷新</a-button>
      </div>
    </div>

    <a-spin :spinning="loading">
      <a-row :gutter="[16, 16]" v-if="info">
        <!-- CPU -->
        <a-col :xs="24" :md="12" :lg="6">
          <a-card title="CPU">
            <a-statistic
              title="逻辑核心数"
              :value="info.cpu.logicalCores"
              suffix="核"
            />
            <a-progress
              :percent="info.cpu.processCpuUsagePercent"
              :stroke-color="progressColor(info.cpu.processCpuUsagePercent)"
              style="margin-top: 12px"
            />
            <div class="stat-label">进程 CPU 使用率</div>
          </a-card>
        </a-col>

        <!-- 内存 -->
        <a-col :xs="24" :md="12" :lg="6">
          <a-card title="内存">
            <a-statistic
              title="总内存"
              :value="formatBytes(info.memory.totalBytes)"
            />
            <a-progress
              :percent="info.memory.usagePercent"
              :stroke-color="progressColor(info.memory.usagePercent)"
              style="margin-top: 12px"
            />
            <div class="stat-label">
              已用 {{ formatBytes(info.memory.usedBytes) }} /
              可用 {{ formatBytes(info.memory.availableBytes) }}
            </div>
          </a-card>
        </a-col>

        <!-- 运行时 -->
        <a-col :xs="24" :md="12" :lg="12">
          <a-card title="运行时信息">
            <a-descriptions :column="2" size="small">
              <a-descriptions-item label=".NET 版本">{{ info.runtime.dotNetVersion }}</a-descriptions-item>
              <a-descriptions-item label="操作系统">{{ info.runtime.osDescription }}</a-descriptions-item>
              <a-descriptions-item label="主机名">{{ info.runtime.machineName }}</a-descriptions-item>
              <a-descriptions-item label="进程 ID">{{ info.runtime.processId }}</a-descriptions-item>
              <a-descriptions-item label="线程数">{{ info.runtime.threadCount }}</a-descriptions-item>
              <a-descriptions-item label="GC 内存">{{ formatBytes(info.runtime.gcMemoryBytes) }}</a-descriptions-item>
              <a-descriptions-item label="启动时间">{{ formatTime(info.runtime.startedAt) }}</a-descriptions-item>
              <a-descriptions-item label="运行时长">{{ info.runtime.uptime }}</a-descriptions-item>
            </a-descriptions>
          </a-card>
        </a-col>

        <!-- 磁盘 -->
        <a-col :span="24">
          <a-card title="磁盘信息">
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
                    已用 {{ formatBytes(disk.usedBytes) }} /
                    总计 {{ formatBytes(disk.totalBytes) }}
                  </div>
                </div>
              </a-col>
            </a-row>
          </a-card>
        </a-col>
      </a-row>

      <a-empty v-else-if="!loading" description="暂无数据" />
    </a-spin>
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, onUnmounted } from "vue";
import { message } from "ant-design-vue";
import { ClockCircleOutlined } from "@ant-design/icons-vue";
import { requestApi } from "@/services/api-core";
import type { ApiResponse } from "@/types/api";

interface CpuInfo { logicalCores: number; processCpuUsagePercent: number }
interface MemoryInfo { totalBytes: number; usedBytes: number; availableBytes: number; usagePercent: number }
interface DiskInfo { name: string; totalBytes: number; usedBytes: number; availableBytes: number; usagePercent: number }
interface RuntimeInfo { dotNetVersion: string; osDescription: string; machineName: string; processId: number; threadCount: number; gcMemoryBytes: number; startedAt: string; uptime: string }
interface ServerInfo { cpu: CpuInfo; memory: MemoryInfo; disks: DiskInfo[]; runtime: RuntimeInfo }

const loading = ref(false);
const info = ref<ServerInfo | null>(null);
let timer: number | undefined;

const load = async () => {
  loading.value = true;
  try {
    const response = await requestApi<ApiResponse<ServerInfo>>("/monitor/server-info");
    info.value = response.data ?? null;
  } catch (e: unknown) {
    message.error(e instanceof Error ? e.message : "加载失败");
  } finally {
    loading.value = false;
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
</style>
