<template>
  <a-card :bordered="false">
    <template #title>
      <a-space>
        <a-button type="link" @click="goBack">返回</a-button>
        <span>{{ title }}</span>
      </a-space>
    </template>

    <a-tabs v-model:activeKey="activeTab">
      <a-tab-pane key="documents" tab="文档">
        <div class="toolbar">
          <a-space wrap>
            <a-input-number v-model:value="uploadFileId" :min="1" placeholder="输入已上传 fileId" />
            <a-button type="primary" @click="handleAddDocument">添加文档</a-button>
            <a-upload :show-upload-list="false" :custom-request="handleUploadDocument">
              <a-button>上传并添加文档</a-button>
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
                <a-button type="link" @click="loadChunks(record.id)">查看分片</a-button>
                <a-button type="link" @click="handleResegment(record.id)">重分段</a-button>
                <a-popconfirm title="确认删除该文档？" @confirm="handleDeleteDocument(record.id)">
                  <a-button type="link" danger>删除</a-button>
                </a-popconfirm>
              </a-space>
            </template>
          </template>
        </a-table>
      </a-tab-pane>

      <a-tab-pane key="chunks" tab="分片">
        <div class="toolbar">
          <a-space wrap>
            <a-input-number v-model:value="newChunk.documentId" :min="1" placeholder="文档ID" />
            <a-input-number v-model:value="newChunk.chunkIndex" :min="0" placeholder="索引" />
            <a-input v-model:value="newChunk.content" placeholder="分片内容" style="width: 300px" />
            <a-button type="primary" @click="handleCreateChunk">新增分片</a-button>
          </a-space>
        </div>
        <a-table row-key="id" :columns="chunkColumns" :data-source="chunks" :pagination="false" :loading="loadingChunks">
          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'hasEmbedding'">
              <a-tag :color="record.hasEmbedding ? 'green' : 'default'">{{ record.hasEmbedding ? "是" : "否" }}</a-tag>
            </template>
            <template v-if="column.key === 'action'">
              <a-space>
                <a-button type="link" @click="openChunkEdit(record)">编辑</a-button>
                <a-popconfirm title="确认删除该分片？" @confirm="handleDeleteChunk(record.id)">
                  <a-button type="link" danger>删除</a-button>
                </a-popconfirm>
              </a-space>
            </template>
          </template>
        </a-table>
      </a-tab-pane>
    </a-tabs>

    <a-modal
      v-model:open="chunkModalVisible"
      title="编辑分片"
      :confirm-loading="chunkModalLoading"
      @ok="submitChunkEdit"
      @cancel="closeChunkModal"
    >
      <a-form layout="vertical">
        <a-form-item label="内容">
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
import { onMounted, reactive, ref } from "vue";
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
  resegmentDocument,
  updateChunk,
  type DocumentChunkDto,
  type KnowledgeDocumentDto
} from "@/services/api-knowledge";

const route = useRoute();
const router = useRouter();
const knowledgeBaseId = Number(route.params["id"]);

const title = ref("知识库详情");
const activeTab = ref("documents");
const uploadFileId = ref<number | null>(null);

const documents = ref<KnowledgeDocumentDto[]>([]);
const chunks = ref<DocumentChunkDto[]>([]);
const loadingDocs = ref(false);
const loadingChunks = ref(false);

const docColumns = [
  { title: "ID", dataIndex: "id", key: "id", width: 90 },
  { title: "文件名", dataIndex: "fileName", key: "fileName" },
  { title: "状态", dataIndex: "status", key: "status", width: 110 },
  { title: "分片数", dataIndex: "chunkCount", key: "chunkCount", width: 100 },
  { title: "错误", dataIndex: "errorMessage", key: "errorMessage", width: 240 },
  { title: "操作", key: "action", width: 220 }
];

const chunkColumns = [
  { title: "ID", dataIndex: "id", key: "id", width: 90 },
  { title: "文档ID", dataIndex: "documentId", key: "documentId", width: 100 },
  { title: "索引", dataIndex: "chunkIndex", key: "chunkIndex", width: 80 },
  { title: "内容", dataIndex: "content", key: "content" },
  { title: "向量化", dataIndex: "hasEmbedding", key: "hasEmbedding", width: 90 },
  { title: "操作", key: "action", width: 160 }
];

const newChunk = reactive({
  documentId: null as number | null,
  chunkIndex: 0,
  content: "",
  startOffset: 0,
  endOffset: 0
});

const chunkModalVisible = ref(false);
const chunkModalLoading = ref(false);
const editingChunk = reactive({
  id: 0,
  content: "",
  startOffset: 0,
  endOffset: 0
});

function statusLabel(status: number) {
  if (status === 1) return "处理中";
  if (status === 2) return "完成";
  if (status === 3) return "失败";
  return "待处理";
}

function statusColor(status: number) {
  if (status === 1) return "processing";
  if (status === 2) return "success";
  if (status === 3) return "error";
  return "default";
}

function goBack() {
  void router.push("/ai/knowledge-bases");
}

async function loadBase() {
  try {
    const detail = await getKnowledgeBaseById(knowledgeBaseId);
    title.value = `${detail.name}（${detail.documentCount} 文档 / ${detail.chunkCount} 分片）`;
  } catch (err: unknown) {
    message.error((err as Error).message || "加载知识库详情失败");
  }
}

async function loadDocuments() {
  loadingDocs.value = true;
  try {
    const result = await getKnowledgeDocumentsPaged(knowledgeBaseId, { pageIndex: 1, pageSize: 100 });
    documents.value = result.items;
  } catch (err: unknown) {
    message.error((err as Error).message || "加载文档失败");
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
    const result = await getDocumentChunksPaged(knowledgeBaseId, targetDocId, { pageIndex: 1, pageSize: 200 });
    chunks.value = result.items;
    activeTab.value = "chunks";
  } catch (err: unknown) {
    message.error((err as Error).message || "加载分片失败");
  } finally {
    loadingChunks.value = false;
  }
}

async function handleAddDocument() {
  if (!uploadFileId.value || uploadFileId.value <= 0) {
    message.warning("请先输入 fileId");
    return;
  }
  try {
    await createKnowledgeDocument(knowledgeBaseId, { fileId: uploadFileId.value });
    message.success("文档已加入处理队列");
    uploadFileId.value = null;
    await Promise.all([loadDocuments(), loadBase()]);
  } catch (err: unknown) {
    message.error((err as Error).message || "添加文档失败");
  }
}

async function handleUploadDocument(options: any) {
  const file = options.file as File;
  if (!file) {
    options.onError?.(new Error("未选择文件"));
    return;
  }

  try {
    await createKnowledgeDocumentByFile(knowledgeBaseId, file);
    message.success("文档上传成功，已加入处理队列");
    options.onSuccess?.({}, new XMLHttpRequest());
    await Promise.all([loadDocuments(), loadBase()]);
  } catch (err: unknown) {
    const uploadError = err instanceof Error ? err : new Error("上传失败");
    options.onError?.(uploadError);
    message.error(uploadError.message || "上传失败");
  }
}

async function handleDeleteDocument(documentId: number) {
  try {
    await deleteKnowledgeDocument(knowledgeBaseId, documentId);
    message.success("删除成功");
    await Promise.all([loadDocuments(), loadBase()]);
    if (chunks.value.some((x) => x.documentId === documentId)) {
      chunks.value = chunks.value.filter((x) => x.documentId !== documentId);
    }
  } catch (err: unknown) {
    message.error((err as Error).message || "删除失败");
  }
}

async function handleResegment(documentId: number) {
  try {
    await resegmentDocument(knowledgeBaseId, documentId, { chunkSize: 500, overlap: 50 });
    message.success("已提交重分段任务");
    await loadDocuments();
  } catch (err: unknown) {
    message.error((err as Error).message || "重分段失败");
  }
}

async function handleCreateChunk() {
  if (!newChunk.documentId || !newChunk.content.trim()) {
    message.warning("请填写文档ID和内容");
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
    message.success("新增分片成功");
    Object.assign(newChunk, { documentId: null, chunkIndex: 0, content: "", startOffset: 0, endOffset: 0 });
    await Promise.all([loadChunks(), loadBase()]);
  } catch (err: unknown) {
    message.error((err as Error).message || "新增分片失败");
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
    message.success("更新成功");
    chunkModalVisible.value = false;
    await loadChunks();
  } catch (err: unknown) {
    message.error((err as Error).message || "更新失败");
  } finally {
    chunkModalLoading.value = false;
  }
}

async function handleDeleteChunk(chunkId: number) {
  try {
    await deleteChunk(knowledgeBaseId, chunkId);
    message.success("删除成功");
    await Promise.all([loadChunks(), loadBase()]);
  } catch (err: unknown) {
    message.error((err as Error).message || "删除失败");
  }
}

onMounted(async () => {
  await Promise.all([loadBase(), loadDocuments()]);
});
</script>

<style scoped>
.toolbar {
  margin-bottom: 12px;
}
</style>
