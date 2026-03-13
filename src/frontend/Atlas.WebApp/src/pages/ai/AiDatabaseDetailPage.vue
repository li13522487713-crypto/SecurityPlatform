<template>
  <a-space direction="vertical" style="width: 100%" :size="16">
    <a-card :title="`数据库详情 #${databaseId}`" :bordered="false">
      <template #extra>
        <a-space>
          <a-button @click="goBack">返回</a-button>
          <a-button :loading="schemaValidating" @click="validateSchema">校验 Schema</a-button>
          <a-button :loading="templateDownloading" @click="downloadTemplate">下载模板</a-button>
          <a-button type="primary" @click="openImport">导入数据</a-button>
        </a-space>
      </template>

      <a-descriptions :column="2" bordered size="small">
        <a-descriptions-item label="名称">{{ detail?.name ?? "-" }}</a-descriptions-item>
        <a-descriptions-item label="记录数">{{ detail?.recordCount ?? 0 }}</a-descriptions-item>
        <a-descriptions-item label="Bot">{{ detail?.botId ?? "-" }}</a-descriptions-item>
        <a-descriptions-item label="更新时间">{{ detail?.updatedAt ?? "-" }}</a-descriptions-item>
        <a-descriptions-item label="描述" :span="2">{{ detail?.description ?? "-" }}</a-descriptions-item>
      </a-descriptions>

      <a-divider orientation="left">Schema</a-divider>
      <a-typography-paragraph>
        <pre class="schema-block">{{ detail?.tableSchema }}</pre>
      </a-typography-paragraph>
    </a-card>

    <a-card title="数据记录" :bordered="false">
      <template #extra>
        <a-space>
          <a-button @click="refreshImportProgress">刷新导入进度</a-button>
          <a-button type="primary" @click="openCreateRecord">新增记录</a-button>
        </a-space>
      </template>

      <a-alert
        v-if="importProgress"
        type="info"
        show-icon
        style="margin-bottom: 12px"
        :message="`导入任务 #${importProgress.taskId} 状态：${importStatusLabel(importProgress.status)}`"
        :description="`总计 ${importProgress.totalRows}，成功 ${importProgress.succeededRows}，失败 ${importProgress.failedRows}${importProgress.errorMessage ? `，错误：${importProgress.errorMessage}` : ''}`"
      />

      <a-table row-key="id" :columns="columns" :data-source="records" :loading="recordsLoading" :pagination="false">
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'dataJson'">
            <a-typography-paragraph :ellipsis="{ rows: 2, expandable: true, symbol: '展开' }">
              {{ record.dataJson }}
            </a-typography-paragraph>
          </template>
          <template v-if="column.key === 'action'">
            <a-space>
              <a-button type="link" @click="openEditRecord(record.id, record.dataJson)">编辑</a-button>
              <a-popconfirm title="确认删除该记录？" @confirm="handleDeleteRecord(record.id)">
                <a-button type="link" danger>删除</a-button>
              </a-popconfirm>
            </a-space>
          </template>
        </template>
      </a-table>

      <div class="pager">
        <a-pagination
          v-model:current="pageIndex"
          v-model:page-size="pageSize"
          :total="total"
          show-size-changer
          :page-size-options="['10', '20', '50']"
          @change="loadRecords"
        />
      </div>
    </a-card>

    <a-modal
      v-model:open="recordModalOpen"
      :title="editingRecordId ? '编辑记录' : '新增记录'"
      :confirm-loading="recordSubmitting"
      width="760px"
      @ok="submitRecord"
      @cancel="closeRecordModal"
    >
      <a-form ref="recordFormRef" :model="recordForm" layout="vertical" :rules="recordRules">
        <a-form-item label="记录 JSON" name="dataJson">
          <a-textarea v-model:value="recordForm.dataJson" :rows="12" />
        </a-form-item>
      </a-form>
    </a-modal>

    <database-import-modal
      :open="importOpen"
      :database-id="databaseId"
      @cancel="closeImport"
      @success="handleImportSuccess"
    />
  </a-space>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import type { FormInstance } from "ant-design-vue";
import { message } from "ant-design-vue";
import DatabaseImportModal from "@/components/ai/database/DatabaseImportModal.vue";
import {
  createAiDatabaseRecord,
  deleteAiDatabaseRecord,
  downloadAiDatabaseTemplate,
  getAiDatabaseById,
  getAiDatabaseImportProgress,
  getAiDatabaseRecordsPaged,
  updateAiDatabaseRecord,
  validateAiDatabaseSchema,
  type AiDatabaseDetail,
  type AiDatabaseImportProgress,
  type AiDatabaseRecordListItem
} from "@/services/api-ai-database";

const route = useRoute();
const router = useRouter();
const databaseId = computed(() => Number(route.params.id));

const detail = ref<AiDatabaseDetail | null>(null);
const records = ref<AiDatabaseRecordListItem[]>([]);
const recordsLoading = ref(false);
const pageIndex = ref(1);
const pageSize = ref(20);
const total = ref(0);
const importProgress = ref<AiDatabaseImportProgress | null>(null);
const schemaValidating = ref(false);
const templateDownloading = ref(false);

const columns = [
  { title: "ID", dataIndex: "id", key: "id", width: 180 },
  { title: "数据", key: "dataJson" },
  { title: "更新时间", dataIndex: "updatedAt", key: "updatedAt", width: 200 },
  { title: "操作", key: "action", width: 150 }
];

const recordModalOpen = ref(false);
const recordSubmitting = ref(false);
const editingRecordId = ref<number | null>(null);
const recordFormRef = ref<FormInstance>();
const recordForm = reactive({
  dataJson: "{}"
});
const recordRules = {
  dataJson: [{ required: true, message: "请输入 JSON 数据" }]
};

const importOpen = ref(false);

function goBack() {
  void router.push("/ai/databases");
}

async function loadDetail() {
  if (!Number.isFinite(databaseId.value) || databaseId.value <= 0) {
    message.error("数据库ID无效");
    return;
  }

  try {
    detail.value = await getAiDatabaseById(databaseId.value);
  } catch (error: unknown) {
    message.error((error as Error).message || "加载数据库详情失败");
  }
}

async function loadRecords() {
  recordsLoading.value = true;
  try {
    const result = await getAiDatabaseRecordsPaged(databaseId.value, {
      pageIndex: pageIndex.value,
      pageSize: pageSize.value
    });
    records.value = result.items;
    total.value = Number(result.total);
  } catch (error: unknown) {
    message.error((error as Error).message || "加载数据库记录失败");
  } finally {
    recordsLoading.value = false;
  }
}

async function refreshImportProgress() {
  try {
    importProgress.value = await getAiDatabaseImportProgress(databaseId.value);
  } catch {
    importProgress.value = null;
  }
}

async function validateSchema() {
  if (!detail.value) {
    return;
  }

  schemaValidating.value = true;
  try {
    const result = await validateAiDatabaseSchema(databaseId.value, detail.value.tableSchema);
    if (result.isValid) {
      message.success("Schema 校验通过");
    } else {
      message.error(`Schema 校验失败：${result.errors.join("；")}`);
    }
  } catch (error: unknown) {
    message.error((error as Error).message || "Schema 校验失败");
  } finally {
    schemaValidating.value = false;
  }
}

async function downloadTemplate() {
  templateDownloading.value = true;
  try {
    const blob = await downloadAiDatabaseTemplate(databaseId.value);
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `ai-database-${databaseId.value}-template.csv`;
    link.click();
    URL.revokeObjectURL(url);
  } catch (error: unknown) {
    message.error((error as Error).message || "下载模板失败");
  } finally {
    templateDownloading.value = false;
  }
}

function openCreateRecord() {
  editingRecordId.value = null;
  recordForm.dataJson = "{}";
  recordModalOpen.value = true;
}

function openEditRecord(id: number, dataJson: string) {
  editingRecordId.value = id;
  recordForm.dataJson = dataJson;
  recordModalOpen.value = true;
}

function closeRecordModal() {
  recordModalOpen.value = false;
  recordFormRef.value?.resetFields();
}

async function submitRecord() {
  try {
    await recordFormRef.value?.validate();
  } catch {
    return;
  }

  recordSubmitting.value = true;
  try {
    if (editingRecordId.value) {
      await updateAiDatabaseRecord(databaseId.value, editingRecordId.value, { dataJson: recordForm.dataJson });
      message.success("记录更新成功");
    } else {
      await createAiDatabaseRecord(databaseId.value, { dataJson: recordForm.dataJson });
      message.success("记录创建成功");
    }

    recordModalOpen.value = false;
    await Promise.all([loadDetail(), loadRecords()]);
  } catch (error: unknown) {
    message.error((error as Error).message || "提交记录失败");
  } finally {
    recordSubmitting.value = false;
  }
}

async function handleDeleteRecord(recordId: number) {
  try {
    await deleteAiDatabaseRecord(databaseId.value, recordId);
    message.success("删除成功");
    await Promise.all([loadDetail(), loadRecords()]);
  } catch (error: unknown) {
    message.error((error as Error).message || "删除记录失败");
  }
}

function openImport() {
  importOpen.value = true;
}

function closeImport() {
  importOpen.value = false;
}

function handleImportSuccess() {
  importOpen.value = false;
  void Promise.all([loadDetail(), loadRecords(), refreshImportProgress()]);
}

function importStatusLabel(status: number) {
  if (status === 1) return "进行中";
  if (status === 2) return "已完成";
  if (status === 3) return "失败";
  return "待执行";
}

onMounted(() => {
  void Promise.all([loadDetail(), loadRecords(), refreshImportProgress()]);
});
</script>

<style scoped>
.schema-block {
  margin: 0;
  padding: 12px;
  border-radius: 8px;
  background: #fafafa;
  max-height: 300px;
  overflow: auto;
  font-size: 12px;
  white-space: pre-wrap;
  word-break: break-all;
}

.pager {
  margin-top: 16px;
  display: flex;
  justify-content: flex-end;
}
</style>
