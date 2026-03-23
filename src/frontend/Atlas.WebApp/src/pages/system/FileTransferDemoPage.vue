<template>
  <a-space direction="vertical" style="width: 100%" :size="16">
    <a-alert
      type="info"
      show-icon
      :message="t('fileTransfer.demoTitle')"
      :description="t('fileTransfer.demoDescription')"
    />

    <a-card :title="t('fileTransfer.basicUploadTitle')" size="small">
      <file-upload-panel
        v-model="uploadedFiles"
        :max-count="3"
        :button-text="t('fileTransfer.basicUploadButton')"
      />
    </a-card>

    <chunk-upload-panel @completed="handleTusCompleted" />

    <chunk-download-panel :file-id="selectedFileId" />

    <a-card :title="t('fileTransfer.recentFilesTitle')" size="small">
      <a-empty v-if="uploadedFiles.length === 0 && !selectedFileId" :description="t('fileTransfer.noRecentFile')" />
      <a-space v-else direction="vertical" style="width: 100%">
        <a-tag v-for="file in uploadedFiles" :key="file.id" color="processing" @click="selectFile(file.id)">
          #{{ file.id }} · {{ file.originalName }}
        </a-tag>
        <a-tag v-if="selectedFileId" color="success">
          {{ t("fileTransfer.currentFileId") }}: {{ selectedFileId }}
        </a-tag>
      </a-space>
    </a-card>
  </a-space>
</template>

<script setup lang="ts">
import { ref } from "vue";
import { useI18n } from "vue-i18n";
import type { FileUploadResult } from "@/types/api";
import FileUploadPanel from "@/components/common/file-upload-panel.vue";
import ChunkUploadPanel from "@/components/common/chunk-upload-panel.vue";
import ChunkDownloadPanel from "@/components/common/chunk-download-panel.vue";

const { t } = useI18n();
const uploadedFiles = ref<FileUploadResult[]>([]);
const selectedFileId = ref<number>();

function selectFile(fileId: number) {
  selectedFileId.value = fileId;
}

function handleTusCompleted(fileId: number) {
  selectedFileId.value = fileId;
}
</script>
