<template>
  <div class="attachment-panel">
    <!-- 上传按钮 -->
    <a-upload
      :custom-request="handleUpload"
      :show-upload-list="false"
      :disabled="disabled || (!allowMultiple && bindings.length > 0)"
      :accept="accept"
    >
      <a-button
        :disabled="disabled || (!allowMultiple && bindings.length > 0)"
        :loading="uploading"
        type="dashed"
        block
      >
        <template #icon><UploadOutlined /></template>
        {{ t("attachmentPanel.upload") }}
      </a-button>
    </a-upload>

    <!-- 附件列表 -->
    <a-empty
      v-if="bindings.length === 0 && !loading"
      :description="t('attachmentPanel.empty')"
      :image="false"
      style="margin-top: 12px"
    />

    <a-spin :spinning="loading">
      <a-list
        v-if="bindings.length > 0"
        size="small"
        :data-source="bindings"
        style="margin-top: 8px"
      >
        <template #renderItem="{ item }">
          <a-list-item>
            <template #actions>
              <a-tooltip :title="t('attachmentPanel.viewHistory')">
                <a-button
                  type="link"
                  size="small"
                  @click="openVersionHistory(item)"
                >
                  <template #icon><HistoryOutlined /></template>
                  v{{ item.file?.versionNumber ?? 1 }}
                </a-button>
              </a-tooltip>
              <a-button
                type="link"
                size="small"
                @click="handleDownload(item)"
              >
                {{ t("attachmentPanel.download") }}
              </a-button>
              <a-popconfirm
                :title="t('attachmentPanel.confirmUnbind')"
                :disabled="disabled"
                @confirm="handleUnbind(item)"
              >
                <a-button
                  type="link"
                  danger
                  size="small"
                  :disabled="disabled"
                >
                  {{ t("attachmentPanel.remove") }}
                </a-button>
              </a-popconfirm>
            </template>

            <a-list-item-meta
              :title="item.file?.originalName ?? String(item.fileRecordId)"
              :description="`${item.file?.contentType ?? ''} · ${formatSize(item.file?.sizeBytes ?? 0)} · ${formatDate(item.createdAt)}`"
            />
          </a-list-item>
        </template>
      </a-list>
    </a-spin>

    <!-- 版本历史抽屉 -->
    <a-drawer
      v-model:open="versionDrawerOpen"
      :title="t('attachmentPanel.versionDrawerTitle', { name: currentFileName })"
      width="500"
      placement="right"
    >
      <a-spin :spinning="historyLoading">
        <a-empty
          v-if="versionHistory.length === 0 && !historyLoading"
          :description="t('common.noData')"
          :image="false"
        />
        <a-timeline v-else>
          <a-timeline-item
            v-for="ver in versionHistory"
            :key="ver.id"
            :color="ver.isLatestVersion ? 'green' : 'gray'"
          >
            <div class="version-item">
              <a-space>
                <a-tag :color="ver.isLatestVersion ? 'success' : 'default'">
                  v{{ ver.versionNumber }}
                  <span v-if="ver.isLatestVersion"> · {{ t("attachmentPanel.versionLatest") }}</span>
                </a-tag>
                <span class="version-size">{{ formatSize(ver.sizeBytes) }}</span>
              </a-space>
              <div class="version-meta">
                <span>{{ ver.uploadedByName }}</span>
                <span>{{ formatDate(ver.uploadedAt) }}</span>
              </div>
            </div>
          </a-timeline-item>
        </a-timeline>
      </a-spin>
    </a-drawer>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, onMounted } from "vue";
import { message } from "ant-design-vue";
import { UploadOutlined, HistoryOutlined } from "@ant-design/icons-vue";
import type { UploadRequestOption as RcCustomRequestOptions } from "ant-design-vue/es/vc-upload/interface";
import { useI18n } from "vue-i18n";
import {
  uploadFileResource,
  bindAttachment,
  unbindAttachment,
  listAttachments,
  getFileVersionHistory,
  getFileSignedUrl
} from "@/services/api-files";
import type { AttachmentBindingDto, FileVersionHistoryItemDto } from "@/types/api";

const props = withDefaults(
  defineProps<{
    entityType: string;
    entityId: number;
    fieldKey?: string;
    allowMultiple?: boolean;
    disabled?: boolean;
    accept?: string;
  }>(),
  {
    fieldKey: undefined,
    allowMultiple: true,
    disabled: false,
    accept: ""
  }
);

const emit = defineEmits<{
  (e: "bound", value: AttachmentBindingDto): void;
  (e: "unbound", fileRecordId: number): void;
}>();

const { t } = useI18n();

const bindings = ref<AttachmentBindingDto[]>([]);
const loading = ref(false);
const uploading = ref(false);

// ---- 版本历史抽屉 ----
const versionDrawerOpen = ref(false);
const historyLoading = ref(false);
const versionHistory = ref<FileVersionHistoryItemDto[]>([]);
const currentFileName = ref("");

async function loadAttachments() {
  if (!props.entityId) return;
  loading.value = true;
  try {
    bindings.value = await listAttachments(props.entityType, props.entityId, props.fieldKey);
  } catch {
    message.error(t("attachmentPanel.loadFailed"));
  } finally {
    loading.value = false;
  }
}

watch(
  () => [props.entityType, props.entityId, props.fieldKey],
  () => loadAttachments(),
  { immediate: false }
);

onMounted(() => loadAttachments());

async function handleUpload(options: RcCustomRequestOptions) {
  const originFile = options.file;
  if (!(originFile instanceof File)) {
    options.onError?.(new Error(t("attachmentPanel.uploadFailed")));
    return;
  }

  uploading.value = true;
  try {
    const uploaded = await uploadFileResource(originFile);
    const binding = await bindAttachment({
      fileRecordId: uploaded.id,
      entityType: props.entityType,
      entityId: props.entityId,
      fieldKey: props.fieldKey,
      isPrimary: bindings.value.length === 0
    });
    bindings.value = [...bindings.value, binding];
    emit("bound", binding);
    options.onSuccess?.(binding);
    message.success(t("attachmentPanel.bindSuccess"));
  } catch (err) {
    const errorMsg = err instanceof Error ? err.message : t("attachmentPanel.uploadFailed");
    options.onError?.(new Error(errorMsg));
    message.error(errorMsg);
  } finally {
    uploading.value = false;
  }
}

async function handleUnbind(item: AttachmentBindingDto) {
  try {
    await unbindAttachment({
      fileRecordId: item.fileRecordId,
      entityType: props.entityType,
      entityId: props.entityId,
      fieldKey: props.fieldKey
    });
    bindings.value = bindings.value.filter((b) => b.id !== item.id);
    emit("unbound", item.fileRecordId);
    message.success(t("attachmentPanel.unbindSuccess"));
  } catch {
    message.error(t("attachmentPanel.unbindFailed"));
  }
}

async function handleDownload(item: AttachmentBindingDto) {
  try {
    const result = await getFileSignedUrl(item.fileRecordId);
    window.open(result.url, "_blank");
  } catch {
    message.error(t("common.failed"));
  }
}

async function openVersionHistory(item: AttachmentBindingDto) {
  currentFileName.value = item.file?.originalName ?? String(item.fileRecordId);
  versionDrawerOpen.value = true;
  historyLoading.value = true;
  try {
    versionHistory.value = await getFileVersionHistory(item.fileRecordId);
  } catch {
    message.error(t("attachmentPanel.loadFailed"));
  } finally {
    historyLoading.value = false;
  }
}

function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

function formatDate(iso: string): string {
  if (!iso) return "";
  return new Date(iso).toLocaleString();
}
</script>

<style scoped>
.attachment-panel {
  width: 100%;
}

.version-item {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.version-meta {
  display: flex;
  gap: 8px;
  color: var(--ant-color-text-secondary, #888);
  font-size: 12px;
}

.version-size {
  color: var(--ant-color-text-secondary, #888);
  font-size: 12px;
}
</style>
