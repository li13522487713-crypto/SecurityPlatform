<template>
  <a-card :title="t('fileTransfer.chunkDownloadTitle')" size="small">
    <a-space direction="vertical" style="width: 100%">
      <a-space>
        <a-input-number
          v-model:value="innerFileId"
          :placeholder="t('fileTransfer.fileIdPlaceholder')"
          style="width: 220px"
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

      <a-progress :percent="progressPercent" :status="progressStatus" />
      <a-descriptions :column="1" size="small" bordered>
        <a-descriptions-item :label="t('fileTransfer.fileName')">
          {{ fileName || "-" }}
        </a-descriptions-item>
        <a-descriptions-item :label="t('fileTransfer.downloadedBytes')">
          {{ downloadedBytes }} / {{ totalBytes }}
        </a-descriptions-item>
        <a-descriptions-item :label="t('fileTransfer.downloadStatus')">
          {{ statusLabel }}
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
}>(), {
  fileId: undefined,
  chunkSize: 2 * 1024 * 1024
});

const { t } = useI18n();
const innerFileId = ref<number | undefined>(props.fileId);
const {
  fileName,
  totalBytes,
  downloadedBytes,
  progressPercent,
  status,
  start,
  pause,
  resume,
  cancel,
  resetAll
} = useRangeDownload(props.chunkSize);
const downloading = computed(() => status.value === "downloading");

watch(
  () => props.fileId,
  (value) => {
    innerFileId.value = value;
  }
);

const progressStatus = computed(() => {
  if (status.value === "error") {
    return "exception";
  }
  if (status.value === "completed") {
    return "success";
  }
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
  return map[status.value];
});

async function startDownload() {
  if (!innerFileId.value) {
    message.warning(t("fileTransfer.selectFileFirst"));
    return;
  }

  try {
    await start(innerFileId.value, props.chunkSize);
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
    await resume(props.chunkSize);
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
