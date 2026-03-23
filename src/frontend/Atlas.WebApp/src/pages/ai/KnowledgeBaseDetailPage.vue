<template>
  <a-card :bordered="false">
    <template #title>
      <a-space>
        <a-button type="link" @click="goBack">{{ t("ai.knowledgeBase.back") }}</a-button>
        <span>{{ title }}</span>
      </a-space>
    </template>

    <a-tabs v-model:active-key="activeTab">
      <a-tab-pane key="documents" :tab="t('ai.knowledgeBase.tabDocuments')">
        <div class="toolbar">
          <a-space wrap>
            <a-input-number v-model:value="uploadFileId" :min="1" :placeholder="t('ai.knowledgeBase.fileIdPlaceholder')" />
            <a-button type="primary" @click="handleAddDocument">{{ t("ai.knowledgeBase.addDocument") }}</a-button>
            <a-upload :show-upload-list="false" :custom-request="handleUploadDocument">
              <a-button>{{ t("ai.knowledgeBase.uploadAndAdd") }}</a-button>
            </a-upload>
          </a-space>
        </div>
        <a-table row-key="id" :columns="docColumns" :data-source="documents" :pagination="false" :loading="loadingDocs">
          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'status'">
              <a-tag :color="statusColor(record.status)">{{ statusLabel(record.status) }}</a-tag>
            </template>
            <template v-if="column.key === 'action'">
              <a-space>
                <a-button type="link" @click="loadChunks(record.id)">{{ t("ai.knowledgeBase.viewChunks") }}</a-button>
                <a-button type="link" @click="handleResegment(record.id)">{{ t("ai.knowledgeBase.resegment") }}</a-button>
                <a-popconfirm :title="t('ai.knowledgeBase.deleteDocConfirm')" @confirm="handleDeleteDocument(record.id)">
                  <a-button type="link" danger>{{ t("common.delete") }}</a-button>
                </a-popconfirm>
              </a-space>
            </template>
          </template>
        </a-table>
      </a-tab-pane>

      <a-tab-pane key="chunks" :tab="t('ai.knowledgeBase.tabChunks')">
        <div class="toolbar">
          <a-space wrap>
            <a-input-number v-model:value="newChunk.documentId" :min="1" :placeholder="t('ai.knowledgeBase.docIdPlaceholder')" />
            <a-input-number v-model:value="newChunk.chunkIndex" :min="0" :placeholder="t('ai.knowledgeBase.indexPlaceholder')" />
            <a-input v-model:value="newChunk.content" :placeholder="t('ai.knowledgeBase.chunkContentPlaceholder')" style="width: 300px" />
            <a-button type="primary" @click="handleCreateChunk">{{ t("ai.knowledgeBase.newChunk") }}</a-button>
          </a-space>
        </div>
        <a-table row-key="id" :columns="chunkColumns" :data-source="chunks" :pagination="false" :loading="loadingChunks">
          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'hasEmbedding'">
              <a-tag :color="record.hasEmbedding ? 'green' : 'default'">{{ record.hasEmbedding ? t("ai.yes") : t("ai.no") }}</a-tag>
            </template>
            <template v-if="column.key === 'action'">
              <a-space>
                <a-button type="link" @click="openChunkEdit(record)">{{ t("common.edit") }}</a-button>
                <a-popconfirm :title="t('ai.knowledgeBase.deleteChunkConfirm')" @confirm="handleDeleteChunk(record.id)">
                  <a-button type="link" danger>{{ t("common.delete") }}</a-button>
                </a-popconfirm>
              </a-space>
            </template>
          </template>
        </a-table>
      </a-tab-pane>

      <a-tab-pane key="retrieval" :tab="t('ai.knowledgeBase.tabRetrieval')">
        <a-form layout="vertical" class="retrieval-form">
          <a-form-item :label="t('ai.knowledgeBase.retrievalStrategy')">
            <a-select v-model:value="retrievalConfig.strategy" :options="strategyOptions" />
          </a-form-item>
          <a-form-item :label="t('ai.knowledgeBase.retrievalEnableRerank')">
            <a-switch v-model:checked="retrievalConfig.enableRerank" />
          </a-form-item>
          <a-row :gutter="12">
            <a-col :span="12">
              <a-form-item :label="t('ai.knowledgeBase.retrievalVectorTopK')">
                <a-input-number v-model:value="retrievalConfig.vectorTopK" :min="1" :max="200" style="width: 100%" />
              </a-form-item>
            </a-col>
            <a-col :span="12">
              <a-form-item :label="t('ai.knowledgeBase.retrievalBm25TopK')">
                <a-input-number v-model:value="retrievalConfig.bm25TopK" :min="1" :max="200" style="width: 100%" />
              </a-form-item>
            </a-col>
          </a-row>
          <a-row :gutter="12">
            <a-col :span="12">
              <a-form-item :label="t('ai.knowledgeBase.retrievalCandidateCount')">
                <a-input-number
                  v-model:value="retrievalConfig.bm25CandidateCount"
                  :min="10"
                  :max="2000"
                  style="width: 100%"
                />
              </a-form-item>
            </a-col>
            <a-col :span="12">
              <a-form-item :label="t('ai.knowledgeBase.retrievalRrfK')">
                <a-input-number v-model:value="retrievalConfig.rrfK" :min="1" :max="200" style="width: 100%" />
              </a-form-item>
            </a-col>
          </a-row>
        </a-form>
        <a-space>
          <a-button type="primary" :loading="retrievalSaving" @click="handleSaveRetrievalConfig">
            {{ t("common.save") }}
          </a-button>
          <a-button @click="goRetrievalTest">{{ t("ai.knowledgeBase.goRetrievalTest") }}</a-button>
        </a-space>
      </a-tab-pane>
    </a-tabs>

    <a-modal
      v-model:open="chunkModalVisible"
      :title="t('ai.knowledgeBase.editChunkTitle')"
      :confirm-loading="chunkModalLoading"
      @ok="submitChunkEdit"
      @cancel="closeChunkModal"
    >
      <a-form layout="vertical">
        <a-form-item :label="t('ai.knowledgeBase.labelContent')">
          <a-textarea v-model:value="editingChunk.content" :rows="4" />
        </a-form-item>
        <a-form-item label="StartOffset">
          <a-input-number v-model:value="editingChunk.startOffset" :min="0" />
        </a-form-item>
        <a-form-item label="EndOffset">
          <a-input-number v-model:value="editingChunk.endOffset" :min="0" />
        </a-form-item>
      </a-form>
    </a-modal>
  </a-card>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, onUnmounted } from "vue";
import { useI18n } from "vue-i18n";

const { t } = useI18n();

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { useRoute, useRouter } from "vue-router";
import { message } from "ant-design-vue";
import {
  createChunk,
  createKnowledgeDocumentByFile,
  createKnowledgeDocument,
  deleteChunk,
  deleteKnowledgeDocument,
  getDocumentChunksPaged,
  getKnowledgeBaseById,
  getKnowledgeDocumentsPaged,
  getKnowledgeRetrievalConfig,
  resegmentDocument,
  updateKnowledgeRetrievalConfig,
  updateChunk,
  type KnowledgeRetrievalConfigDto,
  type KnowledgeRetrievalStrategy,
  type DocumentChunkDto,
  type KnowledgeDocumentDto
} from "@/services/api-knowledge";
import { resolveCurrentAppId } from "@/utils/app-context";

const route = useRoute();
const router = useRouter();
const knowledgeBaseId = Number(route.params["id"]);

const title = ref("");
const activeTab = ref("documents");
const uploadFileId = ref<number | null>(null);
const retrievalSaving = ref(false);

const documents = ref<KnowledgeDocumentDto[]>([]);
const chunks = ref<DocumentChunkDto[]>([]);
const loadingDocs = ref(false);
const loadingChunks = ref(false);

const docColumns = computed(() => [
  { title: t("ai.knowledgeBase.colId"), dataIndex: "id", key: "id", width: 90 },
  { title: t("ai.knowledgeBase.colFileName"), dataIndex: "fileName", key: "fileName" },
  { title: t("ai.knowledgeBase.colStatus"), dataIndex: "status", key: "status", width: 110 },
  { title: t("ai.knowledgeBase.colChunkCount"), dataIndex: "chunkCount", key: "chunkCount", width: 100 },
  { title: t("ai.knowledgeBase.colError"), dataIndex: "errorMessage", key: "errorMessage", width: 240 },
  { title: t("ai.colActions"), key: "action", width: 220 }
]);

const chunkColumns = computed(() => [
  { title: t("ai.knowledgeBase.colId"), dataIndex: "id", key: "id", width: 90 },
  { title: t("ai.knowledgeBase.colDocId"), dataIndex: "documentId", key: "documentId", width: 100 },
  { title: t("ai.knowledgeBase.colIndex"), dataIndex: "chunkIndex", key: "chunkIndex", width: 80 },
  { title: t("ai.knowledgeBase.colContent"), dataIndex: "content", key: "content" },
  { title: t("ai.knowledgeBase.colEmbedding"), dataIndex: "hasEmbedding", key: "hasEmbedding", width: 90 },
  { title: t("ai.colActions"), key: "action", width: 160 }
]);

const newChunk = reactive({
  documentId: null as number | null,
  chunkIndex: 0,
  content: "",
  startOffset: 0,
  endOffset: 0
});

const retrievalConfig = reactive<KnowledgeRetrievalConfigDto>({
  strategy: "hybrid",
  enableRerank: true,
  vectorTopK: 12,
  bm25TopK: 12,
  bm25CandidateCount: 300,
  rrfK: 60
});
const strategyOptions = computed<{ label: string; value: KnowledgeRetrievalStrategy }[]>(() => [
  { label: t("ai.knowledgeBase.strategyVector"), value: "vector" },
  { label: t("ai.knowledgeBase.strategyBm25"), value: "bm25" },
  { label: t("ai.knowledgeBase.strategyHybrid"), value: "hybrid" }
]);

const chunkModalVisible = ref(false);
const chunkModalLoading = ref(false);
const editingChunk = reactive({
  id: 0,
  content: "",
  startOffset: 0,
  endOffset: 0
});

interface UploadRequestOptions {
  file: File;
  onError?: (error: Error) => void;
  onSuccess?: (body: Record<string, unknown>, xhr?: XMLHttpRequest) => void;
}

function statusLabel(status: number) {
  if (status === 1) return t("ai.knowledgeBase.statusProcessing");
  if (status === 2) return t("ai.knowledgeBase.statusDone");
  if (status === 3) return t("ai.knowledgeBase.statusFailed");
  return t("ai.knowledgeBase.statusPending");
}

function statusColor(status: number) {
  if (status === 1) return "processing";
  if (status === 2) return "success";
  if (status === 3) return "error";
  return "default";
}

function goBack() {
  const currentAppId = resolveCurrentAppId(route);
  if (currentAppId) {
    void router.push(`/apps/${currentAppId}/knowledge-bases`);
    return;
  }

  void router.push("/ai/knowledge-bases");
}

function goRetrievalTest() {
  const currentAppId = resolveCurrentAppId(route);
  if (currentAppId) {
    void router.push(`/apps/${currentAppId}/knowledge-bases/${knowledgeBaseId}/test`);
    return;
  }

  void router.push(`/ai/knowledge-bases/${knowledgeBaseId}/test`);
}

async function loadBase() {
  try {
    const detail  = await getKnowledgeBaseById(knowledgeBaseId);

    if (!isMounted.value) return;
    title.value = t("ai.knowledgeBase.detailTitleFmt", {
      name: detail.name,
      docCount: detail.documentCount,
      chunkCount: detail.chunkCount
    });
  } catch (err: unknown) {
    message.error((err as Error).message || t("ai.knowledgeBase.loadDetailFailed"));
  }
}

async function loadRetrievalConfig() {
  try {
    const config = await getKnowledgeRetrievalConfig(knowledgeBaseId);
    if (!isMounted.value) return;

    Object.assign(retrievalConfig, config);
  } catch {
    // 后端未提供检索配置接口时保留默认值，避免影响详情页使用
  }
}

async function loadDocuments() {
  loadingDocs.value = true;
  try {
    const result  = await getKnowledgeDocumentsPaged(knowledgeBaseId, { pageIndex: 1, pageSize: 100 });

    if (!isMounted.value) return;
    documents.value = result.items;
  } catch (err: unknown) {
    message.error((err as Error).message || t("ai.knowledgeBase.loadDocsFailed"));
  } finally {
    loadingDocs.value = false;
  }
}

async function loadChunks(documentId?: number) {
  loadingChunks.value = true;
  try {
    const targetDocId = documentId ?? documents.value[0]?.id;
    if (!targetDocId) {
      chunks.value = [];
      return;
    }
    const result  = await getDocumentChunksPaged(knowledgeBaseId, targetDocId, { pageIndex: 1, pageSize: 200 });

    if (!isMounted.value) return;
    chunks.value = result.items;
    activeTab.value = "chunks";
  } catch (err: unknown) {
    message.error((err as Error).message || t("ai.knowledgeBase.loadChunksFailed"));
  } finally {
    loadingChunks.value = false;
  }
}

async function handleAddDocument() {
  if (!uploadFileId.value || uploadFileId.value <= 0) {
    message.warning(t("ai.knowledgeBase.warnFileId"));
    return;
  }
  try {
    await createKnowledgeDocument(knowledgeBaseId, { fileId: uploadFileId.value });

    if (!isMounted.value) return;
    message.success(t("ai.knowledgeBase.docQueued"));
    uploadFileId.value = null;
    await Promise.all([loadDocuments(), loadBase()]);

    if (!isMounted.value) return;
  } catch (err: unknown) {
    message.error((err as Error).message || t("ai.knowledgeBase.addDocFailed"));
  }
}

async function handleUploadDocument(options: UploadRequestOptions) {
  const file = options.file;
  if (!file) {
    options.onError?.(new Error(t("ai.knowledgeBase.noFileSelected")));
    return;
  }

  try {
    await createKnowledgeDocumentByFile(knowledgeBaseId, file);

    if (!isMounted.value) return;
    message.success(t("ai.knowledgeBase.uploadQueued"));
    options.onSuccess?.({}, new XMLHttpRequest());
    await Promise.all([loadDocuments(), loadBase()]);

    if (!isMounted.value) return;
  } catch (err: unknown) {
    const uploadError = err instanceof Error ? err : new Error(t("ai.knowledgeBase.uploadFailed"));
    options.onError?.(uploadError);
    message.error(uploadError.message || t("ai.knowledgeBase.uploadFailed"));
  }
}

async function handleDeleteDocument(documentId: number) {
  try {
    await deleteKnowledgeDocument(knowledgeBaseId, documentId);

    if (!isMounted.value) return;
    message.success(t("crud.deleteSuccess"));
    await Promise.all([loadDocuments(), loadBase()]);

    if (!isMounted.value) return;
    if (chunks.value.some((x) => x.documentId === documentId)) {
      chunks.value = chunks.value.filter((x) => x.documentId !== documentId);
    }
  } catch (err: unknown) {
    message.error((err as Error).message || t("crud.deleteFailed"));
  }
}

async function handleResegment(documentId: number) {
  try {
    await resegmentDocument(knowledgeBaseId, documentId, { chunkSize: 500, overlap: 50 });

    if (!isMounted.value) return;
    message.success(t("ai.knowledgeBase.resegmentSubmitted"));
    await loadDocuments();

    if (!isMounted.value) return;
  } catch (err: unknown) {
    message.error((err as Error).message || t("ai.knowledgeBase.resegmentFailed"));
  }
}

async function handleSaveRetrievalConfig() {
  retrievalSaving.value = true;
  try {
    await updateKnowledgeRetrievalConfig(knowledgeBaseId, {
      strategy: retrievalConfig.strategy,
      enableRerank: retrievalConfig.enableRerank,
      vectorTopK: retrievalConfig.vectorTopK,
      bm25TopK: retrievalConfig.bm25TopK,
      bm25CandidateCount: retrievalConfig.bm25CandidateCount,
      rrfK: retrievalConfig.rrfK
    });
    if (!isMounted.value) return;

    message.success(t("ai.knowledgeBase.saveRetrievalConfigSuccess"));
  } catch (err: unknown) {
    message.error((err as Error).message || t("ai.knowledgeBase.saveRetrievalConfigFailed"));
  } finally {
    retrievalSaving.value = false;
  }
}

async function handleCreateChunk() {
  if (!newChunk.documentId || !newChunk.content.trim()) {
    message.warning(t("ai.knowledgeBase.warnDocContent"));
    return;
  }
  try {
    await createChunk(knowledgeBaseId, {
      documentId: newChunk.documentId,
      chunkIndex: newChunk.chunkIndex,
      content: newChunk.content,
      startOffset: newChunk.startOffset,
      endOffset: newChunk.endOffset
    });

    if (!isMounted.value) return;
    message.success(t("ai.knowledgeBase.chunkCreated"));
    Object.assign(newChunk, { documentId: null, chunkIndex: 0, content: "", startOffset: 0, endOffset: 0 });
    await Promise.all([loadChunks(), loadBase()]);

    if (!isMounted.value) return;
  } catch (err: unknown) {
    message.error((err as Error).message || t("ai.knowledgeBase.chunkCreateFailed"));
  }
}

function openChunkEdit(record: DocumentChunkDto) {
  Object.assign(editingChunk, {
    id: record.id,
    content: record.content,
    startOffset: record.startOffset,
    endOffset: record.endOffset
  });
  chunkModalVisible.value = true;
}

function closeChunkModal() {
  chunkModalVisible.value = false;
}

async function submitChunkEdit() {
  chunkModalLoading.value = true;
  try {
    await updateChunk(knowledgeBaseId, editingChunk.id, {
      content: editingChunk.content,
      startOffset: editingChunk.startOffset,
      endOffset: editingChunk.endOffset
    });

    if (!isMounted.value) return;
    message.success(t("crud.updateSuccess"));
    chunkModalVisible.value = false;
    await loadChunks();

    if (!isMounted.value) return;
  } catch (err: unknown) {
    message.error((err as Error).message || t("ai.knowledgeBase.updateRecordFailed"));
  } finally {
    chunkModalLoading.value = false;
  }
}

async function handleDeleteChunk(chunkId: number) {
  try {
    await deleteChunk(knowledgeBaseId, chunkId);

    if (!isMounted.value) return;
    message.success(t("crud.deleteSuccess"));
    await Promise.all([loadChunks(), loadBase()]);

    if (!isMounted.value) return;
  } catch (err: unknown) {
    message.error((err as Error).message || t("crud.deleteFailed"));
  }
}

onMounted(async () => {
  title.value = t("ai.knowledgeBase.detailTitle");
  await Promise.all([loadBase(), loadDocuments(), loadRetrievalConfig()]);

  if (!isMounted.value) return;
});
</script>

<style scoped>
.toolbar {
  margin-bottom: 12px;
}

.retrieval-form {
  max-width: 760px;
  margin-bottom: 12px;
}
</style>
