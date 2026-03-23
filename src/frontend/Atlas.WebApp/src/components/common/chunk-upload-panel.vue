<template>
  <a-card :title="t('fileTransfer.chunkUploadTitle')" size="small">
    <a-space direction="vertical" style="width: 100%">
      <a-upload
        :before-upload="beforeSelectFile"
        :show-upload-list="false"
        :disabled="isBusy"
      >
        <a-button :disabled="isBusy">
          {{ selectedFile ? selectedFile.name : t("fileTransfer.selectLargeFile") }}
        </a-button>
      </a-upload>

      <a-progress :percent="progressPercent" :status="progressStatus" />

      <a-descriptions :column="1" size="small" bordered>
        <a-descriptions-item :label="t('fileTransfer.sessionId')">
          {{ sessionId || "-" }}
        </a-descriptions-item>
        <a-descriptions-item :label="t('fileTransfer.uploadedBytes')">
          {{ uploadedBytes }} / {{ totalBytes }}
        </a-descriptions-item>
        <a-descriptions-item :label="t('fileTransfer.uploadStatus')">
          {{ statusLabel }}
        </a-descriptions-item>
      </a-descriptions>

      <a-space>
        <a-button type="primary" :disabled="!selectedFile || isBusy" @click="startUpload">
          {{ t("fileTransfer.startUpload") }}
        </a-button>
        <a-button :disabled="status !== 'uploading'" @click="pauseUpload">
          {{ t("fileTransfer.pauseUpload") }}
        </a-button>
        <a-button :disabled="status !== 'paused'" @click="resumeUpload">
          {{ t("fileTransfer.resumeUpload") }}
        </a-button>
        <a-button danger :disabled="status !== 'uploading' && status !== 'paused'" @click="cancelUpload">
          {{ t("fileTransfer.cancelUpload") }}
        </a-button>
      </a-space>
    </a-space>
  </a-card>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";
import { message } from "ant-design-vue";
import type { UploadProps } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import { useTusUpload } from "@/composables/use-tus-upload";
import { checkInstantUpload } from "@/services/api-files";

const props = withDefaults(defineProps<{
  chunkSize?: number;
}>(), {
  chunkSize: 2 * 1024 * 1024
});

const emit = defineEmits<{
  (e: "completed", fileId: number): void;
}>();

const { t } = useI18n();
const selectedFile = ref<File | null>(null);
const {
  sessionId,
  fileId,
  uploadedBytes,
  totalBytes,
  progressPercent,
  status,
  start,
  pause,
  resume,
  cancel
} = useTusUpload(props.chunkSize);

const isBusy = computed(() => status.value === "uploading");
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
    uploading: t("fileTransfer.statusUploading"),
    paused: t("fileTransfer.statusPaused"),
    completed: t("fileTransfer.statusCompleted"),
    error: t("fileTransfer.statusError"),
    cancelled: t("fileTransfer.statusCancelled")
  };
  return map[status.value] ?? status.value;
});

const beforeSelectFile: UploadProps["beforeUpload"] = (file) => {
  selectedFile.value = file as File;
  return false;
};

async function startUpload() {
  if (!selectedFile.value) {
    message.warning(t("fileTransfer.selectFileFirst"));
    return;
  }
  try {
    const hash = await computeFileSha256(selectedFile.value);
    const instantResult = await checkInstantUpload(hash, selectedFile.value.size);
    if (instantResult.exists && typeof instantResult.fileId === "number" && instantResult.fileId > 0) {
      message.success(t("fileTransfer.instantUploadHit", { id: instantResult.fileId }));
      emit("completed", instantResult.fileId);
      return;
    }

    await start(selectedFile.value);
    if (status.value === "completed") {
      message.success(t("fileTransfer.uploadCompleted"));
      if (typeof fileId.value === "number" && fileId.value > 0) {
        emit("completed", fileId.value);
      }
    }
  } catch (error) {
    message.error((error as Error).message || t("fileTransfer.uploadFailed"));
  }
}

function pauseUpload() {
  pause();
  message.info(t("fileTransfer.uploadPaused"));
}

async function resumeUpload() {
  resume();
  message.info(t("fileTransfer.uploadResumed"));
}

function cancelUpload() {
  cancel();
  message.info(t("fileTransfer.uploadCancelled"));
}

async function computeFileSha256(file: File): Promise<string> {
  const arrayBuffer = await file.arrayBuffer();
  const digest = await crypto.subtle.digest("SHA-256", arrayBuffer);
  return Array.from(new Uint8Array(digest))
    .map((item) => item.toString(16).padStart(2, "0"))
    .join("");
}
</script>
