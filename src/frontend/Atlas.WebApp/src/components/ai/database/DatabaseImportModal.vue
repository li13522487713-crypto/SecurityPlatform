<template>
  <a-modal
    :open="open"
    title="导入数据库记录"
    :confirm-loading="submitting"
    ok-text="提交导入"
    @ok="handleSubmit"
    @cancel="emit('cancel')"
  >
    <a-alert
      type="info"
      show-icon
      message="请先上传 CSV 文件，首行为列名。上传后将异步导入，可在页面查看导入状态。"
      style="margin-bottom: 16px"
    />

    <file-upload-panel
      v-model="uploadedFiles"
      :max-count="1"
      accept=".csv,text/csv"
      button-text="上传 CSV 文件"
    />
  </a-modal>
</template>

<script setup lang="ts">
import { ref } from "vue";
import { message } from "ant-design-vue";
import FileUploadPanel from "@/components/common/file-upload-panel.vue";
import { submitAiDatabaseImport } from "@/services/api-ai-database";
import type { FileUploadResult } from "@/types/api";

const props = defineProps<{
  open: boolean;
  databaseId: number;
}>();

const emit = defineEmits<{
  (e: "success"): void;
  (e: "cancel"): void;
}>();

const uploadedFiles = ref<FileUploadResult[]>([]);
const submitting = ref(false);

async function handleSubmit() {
  const file = uploadedFiles.value[0];
  if (!file) {
    message.warning("请先上传 CSV 文件");
    return;
  }

  submitting.value = true;
  try {
    await submitAiDatabaseImport(props.databaseId, { fileId: file.id });
    message.success("导入任务已提交");
    uploadedFiles.value = [];
    emit("success");
  } catch (error: unknown) {
    message.error((error as Error).message || "提交导入任务失败");
  } finally {
    submitting.value = false;
  }
}
</script>
