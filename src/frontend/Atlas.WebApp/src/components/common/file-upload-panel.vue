<template>
  <div class="file-upload-panel">
    <a-space direction="vertical" style="width: 100%">
      <a-upload
        :custom-request="handleCustomUpload"
        :show-upload-list="false"
        :disabled="disabled || reachedMaxCount"
        :accept="accept"
      >
        <a-button :disabled="disabled || reachedMaxCount" :loading="uploading">
          {{ reachedMaxCount ? t("fileUpload.maxCount", { count: maxCount }) : resolvedButtonText }}
        </a-button>
      </a-upload>

      <a-empty
        v-if="uploadedFiles.length === 0"
        :description="t('fileUpload.empty')"
        :image="false"
      />

      <a-list v-else size="small" :data-source="uploadedFiles">
        <template #renderItem="{ item }">
          <a-list-item>
            <template #actions>
              <a-button
                type="link"
                danger
                size="small"
                :disabled="disabled"
                @click="removeFile(item.id)"
              >
                {{ t("common.delete") }}
              </a-button>
            </template>
            <a-list-item-meta
              :title="item.originalName"
              :description="`${item.contentType} · ${formatSize(item.sizeBytes)}`"
            />
          </a-list-item>
        </template>
      </a-list>
    </a-space>
  </div>
</template>

<script setup lang="ts">
import { computed, ref, watch } from "vue";
import { message } from "ant-design-vue";
import type { UploadRequestOption as RcCustomRequestOptions } from "ant-design-vue/es/vc-upload/interface";
import { useI18n } from "vue-i18n";
import { deleteFile, uploadFile } from "@/services/api";
import type { FileUploadResult } from "@/types/api";

const props = withDefaults(
  defineProps<{
    modelValue?: FileUploadResult[];
    maxCount?: number;
    disabled?: boolean;
    accept?: string;
    buttonText?: string;
  }>(),
  {
    modelValue: () => [],
    maxCount: 5,
    disabled: false,
    accept: "",
    buttonText: ""
  }
);

const emit = defineEmits<{
  (e: "update:modelValue", value: FileUploadResult[]): void;
  (e: "uploaded", value: FileUploadResult): void;
  (e: "removed", value: number): void;
}>();

const { t } = useI18n();
const resolvedButtonText = computed(() => props.buttonText ?? t("fileUpload.uploadAttachment"));
const uploadedFiles = ref<FileUploadResult[]>([]);
const uploading = ref(false);

watch(
  () => props.modelValue,
  (value) => {
    uploadedFiles.value = value.slice();
  },
  { immediate: true, deep: true }
);

const reachedMaxCount = computed(() => uploadedFiles.value.length >= props.maxCount);

const handleCustomUpload = async (options: RcCustomRequestOptions) => {
  const originFile = options.file;
  if (!(originFile instanceof File)) {
    const error = new Error(t("fileUpload.uploadFailed"));
    options.onError?.(error);
    return;
  }

  uploading.value = true;
  try {
    const result = await uploadFile(originFile);
    const nextFiles = [...uploadedFiles.value, result];
    uploadedFiles.value = nextFiles;
    emit("update:modelValue", nextFiles);
    emit("uploaded", result);
    options.onSuccess?.(result);
    message.success(t("fileUpload.uploadSuccess"));
  } catch (error) {
    const errorMessage = error instanceof Error ? error.message : t("fileUpload.uploadFailed");
    options.onError?.(new Error(errorMessage));
    message.error(errorMessage);
  } finally {
    uploading.value = false;
  }
};

const removeFile = async (fileId: number) => {
  try {
    await deleteFile(fileId);
    const nextFiles = uploadedFiles.value.filter((item) => item.id !== fileId);
    uploadedFiles.value = nextFiles;
    emit("update:modelValue", nextFiles);
    emit("removed", fileId);
    message.success(t("fileUpload.deleteSuccess"));
  } catch (error) {
    message.error(error instanceof Error ? error.message : t("fileUpload.deleteFailed"));
  }
};

const formatSize = (bytes: number) => {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
};
</script>

<style scoped>
.file-upload-panel {
  width: 100%;
}
</style>
