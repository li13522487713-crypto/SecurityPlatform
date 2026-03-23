<template>
  <a-card :title="t('fileTransfer.chunkDownloadTitle')" size="small">
    <a-space direction="vertical" style="width: 100%">
      <!-- 文件 ID + 操作按钮 -->
      <a-space wrap>
        <a-input-number
          v-model:value="innerFileId"
          :placeholder="t('fileTransfer.fileIdPlaceholder')"
          style="width: 160px"
          :min="1"
        />
        <a-button type="primary" :disabled="!innerFileId || downloading" @click="startDownload">
          {{ t("fileTransfer.startDownload") }}
        </a-button>
        <a-button :disabled="status !== 'downloading'" @click="pauseDownload">
          {{ t("fileTransfer.pauseDownload") }}
        </a-button>
        <a-button :disabled="status !== 'paused'" @click="resumeDownload">
          {{ t("fileTransfer.resumeDownload") }}
        </a-button>
        <a-button danger :disabled="status !== 'downloading' && status !== 'paused'" @click="cancelDownload">
          {{ t("fileTransfer.cancelDownload") }}
        </a-button>
      </a-space>

      <!-- 并发 & 限速配置 -->
      <a-space wrap>
        <a-space>
          <span style="white-space: nowrap">{{ t("fileTransfer.concurrencyLabel") }}</span>
          <a-select
            v-model:value="innerConcurrency"
            :disabled="downloading"
            style="width: 80px"
          >
            <a-select-option :value="1">1</a-select-option>
            <a-select-option :value="2">2</a-select-option>
            <a-select-option :value="4">4</a-select-option>
          </a-select>
        </a-space>
        <a-space>
          <span style="white-space: nowrap">{{ t("fileTransfer.speedLimitLabel") }}</span>
          <a-input-number
            v-model:value="innerSpeedLimitKbs"
            :disabled="downloading"
            :min="0"
            :step="128"
            style="width: 110px"
          />
          <span>KB/s&nbsp;(0={{ t("fileTransfer.noLimit") }})</span>
        </a-space>
      </a-space>

      <!-- 进度条 -->
      <a-progress :percent="progressPercent" :status="progressStatus" />

      <!-- 状态描述 -->
      <a-descriptions :column="2" size="small" bordered>
        <a-descriptions-item :label="t('fileTransfer.fileName')">
          {{ fileName || "-" }}
        </a-descriptions-item>
        <a-descriptions-item :label="t('fileTransfer.downloadStatus')">
          {{ statusLabel }}
        </a-descriptions-item>
        <a-descriptions-item :label="t('fileTransfer.downloadedBytes')">
          {{ formatBytes(downloadedBytes) }} / {{ formatBytes(totalBytes) }}
        </a-descriptions-item>
        <a-descriptions-item :label="t('fileTransfer.currentSpeed')">
          {{ currentSpeedLabel }}
        </a-descriptions-item>
      </a-descriptions>
    </a-space>
  </a-card>
</template>

<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { message } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import { useRangeDownload } from "@/composables/use-range-download";

const props = withDefaults(defineProps<{
  fileId?: number;
  chunkSize?: number;
  concurrency?: number;
  /** 速度上限（bytes/sec），0 表示不限速 */
  maxBytesPerSecond?: number;
}>(), {
  fileId: undefined,
  chunkSize: 2 * 1024 * 1024,
  concurrency: 1,
  maxBytesPerSecond: 0
});

const { t } = useI18n();

const innerFileId = ref<number | undefined>(props.fileId);
const innerConcurrency = ref<number>(props.concurrency);
// UI 展示单位为 KB/s，存储时换算为 bytes/sec
const innerSpeedLimitKbs = ref<number>(
  props.maxBytesPerSecond > 0 ? Math.round(props.maxBytesPerSecond / 1024) : 0
);

const {
  fileName,
  totalBytes,
  downloadedBytes,
  progressPercent,
  status,
  currentSpeed,
  start,
  pause,
  resume,
  cancel,
  resetAll
} = useRangeDownload({ chunkSize: props.chunkSize });

const downloading = computed(() => status.value === "downloading");

watch(() => props.fileId, (v) => { innerFileId.value = v; });
watch(() => props.concurrency, (v) => { innerConcurrency.value = v; });
watch(
  () => props.maxBytesPerSecond,
  (v) => { innerSpeedLimitKbs.value = v > 0 ? Math.round(v / 1024) : 0; }
);

const progressStatus = computed(() => {
  if (status.value === "error") return "exception";
  if (status.value === "completed") return "success";
  return "active";
});

const statusLabel = computed(() => {
  const map: Record<string, string> = {
    idle: t("fileTransfer.statusIdle"),
    downloading: t("fileTransfer.statusDownloading"),
    paused: t("fileTransfer.statusPaused"),
    completed: t("fileTransfer.statusCompleted"),
    cancelled: t("fileTransfer.statusCancelled"),
    error: t("fileTransfer.statusError")
  };
  return map[status.value] ?? status.value;
});

const currentSpeedLabel = computed(() => {
  if (status.value !== "downloading" || currentSpeed.value <= 0) return "-";
  return `${formatBytes(currentSpeed.value)}/s`;
});

function formatBytes(bytes: number): string {
  if (bytes <= 0) return "0 B";
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / 1024 / 1024).toFixed(2)} MB`;
  return `${(bytes / 1024 / 1024 / 1024).toFixed(2)} GB`;
}

function resolveMaxBps(): number {
  return innerSpeedLimitKbs.value > 0 ? innerSpeedLimitKbs.value * 1024 : 0;
}

async function startDownload() {
  if (!innerFileId.value) {
    message.warning(t("fileTransfer.selectFileFirst"));
    return;
  }
  try {
    await start(innerFileId.value, props.chunkSize, innerConcurrency.value, resolveMaxBps());
    message.success(t("fileTransfer.downloadCompleted"));
  } catch (error) {
    message.error((error as Error).message || t("fileTransfer.downloadFailed"));
  }
}

function pauseDownload() {
  pause();
  message.info(t("fileTransfer.downloadPaused"));
}

async function resumeDownload() {
  try {
    await resume(props.chunkSize, innerConcurrency.value, resolveMaxBps());
    message.info(t("fileTransfer.downloadResumed"));
  } catch (error) {
    message.error((error as Error).message || t("fileTransfer.downloadFailed"));
  }
}

function cancelDownload() {
  cancel();
  resetAll();
  message.info(t("fileTransfer.downloadCancelled"));
}
</script>
