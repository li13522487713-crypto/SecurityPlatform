<template>
  <a-space direction="vertical" style="width: 100%" :size="16">
    <a-card :title="t('ai.database.detailTitle', { id: databaseId })" :bordered="false">
      <template #extra>
        <a-space>
          <a-button @click="goBack">{{ t("ai.plugin.back") }}</a-button>
          <a-button :loading="schemaValidating" @click="validateSchema">{{ t("ai.database.validateSchema") }}</a-button>
          <a-button :loading="templateDownloading" @click="downloadTemplate">{{ t("ai.database.downloadTemplate") }}</a-button>
          <a-button type="primary" @click="openImport">{{ t("ai.database.importData") }}</a-button>
        </a-space>
      </template>

      <a-descriptions :column="2" bordered size="small">
        <a-descriptions-item :label="t('ai.promptLib.colName')">{{ detail?.name ?? "-" }}</a-descriptions-item>
        <a-descriptions-item :label="t('ai.database.colRecords')">{{ detail?.recordCount ?? 0 }}</a-descriptions-item>
        <a-descriptions-item :label="t('ai.database.colBot')">{{ detail?.botId ?? "-" }}</a-descriptions-item>
        <a-descriptions-item :label="t('ai.workflow.colUpdatedAt')">{{ detail?.updatedAt ?? "-" }}</a-descriptions-item>
        <a-descriptions-item :label="t('ai.promptLib.labelDescription')" :span="2">{{ detail?.description ?? "-" }}</a-descriptions-item>
      </a-descriptions>

      <a-divider orientation="left">{{ t("ai.database.sectionSchema") }}</a-divider>
      <a-typography-paragraph>
        <pre class="schema-block">{{ detail?.tableSchema }}</pre>
      </a-typography-paragraph>
    </a-card>

    <a-card :title="t('ai.database.dataRecords')" :bordered="false">
      <template #extra>
        <a-space>
          <a-button @click="refreshImportProgress">{{ t("ai.database.refreshImport") }}</a-button>
          <a-button type="primary" @click="openCreateRecord">{{ t("ai.database.newRecord") }}</a-button>
        </a-space>
      </template>

      <a-alert
        v-if="importProgress"
        type="info"
        show-icon
        style="margin-bottom: 12px"
        :message="importTaskMessage"
        :description="importTaskDescription"
      />

      <a-table row-key="id" :columns="columns" :data-source="records" :loading="recordsLoading" :pagination="false">
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'dataJson'">
            <a-typography-paragraph :ellipsis="{ rows: 2, expandable: true, symbol: t('ai.expand') }">
              {{ record.dataJson }}
            </a-typography-paragraph>
          </template>
          <template v-if="column.key === 'action'">
            <a-space>
              <a-button type="link" @click="openEditRecord(record.id, record.dataJson)">{{ t("common.edit") }}</a-button>
              <a-popconfirm :title="t('ai.database.deleteRecordConfirm')" @confirm="handleDeleteRecord(record.id)">
                <a-button type="link" danger>{{ t("common.delete") }}</a-button>
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
      :title="editingRecordId ? t('ai.database.modalRecordEdit') : t('ai.database.modalRecordCreate')"
      :confirm-loading="recordSubmitting"
      width="760px"
      @ok="submitRecord"
      @cancel="closeRecordModal"
    >
      <a-form ref="recordFormRef" :model="recordForm" layout="vertical" :rules="recordRules">
        <a-form-item :label="t('ai.database.labelRecordJson')" name="dataJson">
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
import { computed, onMounted, reactive, ref, onUnmounted } from "vue";
import { useI18n } from "vue-i18n";

const { t } = useI18n();

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

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

const columns = computed(() => [
  { title: t("ai.knowledgeBase.colId"), dataIndex: "id", key: "id", width: 180 },
  { title: t("ai.database.colData"), key: "dataJson" },
  { title: t("ai.workflow.colUpdatedAt"), dataIndex: "updatedAt", key: "updatedAt", width: 200 },
  { title: t("ai.colActions"), key: "action", width: 150 }
]);

const importTaskMessage = computed(() => {
  if (!importProgress.value) return "";
  return t("ai.database.importTaskMsg", {
    taskId: importProgress.value.taskId,
    status: importStatusLabel(importProgress.value.status)
  });
});

const importTaskDescription = computed(() => {
  if (!importProgress.value) return "";
  const p = importProgress.value;
  return t("ai.database.importTaskDesc", {
    total: p.totalRows,
    success: p.succeededRows,
    failed: p.failedRows,
    err: p.errorMessage ? t("ai.database.importTaskError", { msg: p.errorMessage }) : ""
  });
});

const recordModalOpen = ref(false);
const recordSubmitting = ref(false);
const editingRecordId = ref<number | null>(null);
const recordFormRef = ref<FormInstance>();
const recordForm = reactive({
  dataJson: "{}"
});
const recordRules = computed(() => ({
  dataJson: [{ required: true, message: t("ai.database.ruleRecordJson") }]
}));

const importOpen = ref(false);

function goBack() {
  void router.push("/ai/databases");
}

async function loadDetail() {
  if (!Number.isFinite(databaseId.value) || databaseId.value <= 0) {
    message.error(t("ai.database.invalidDbId"));
    return;
  }

  try {
    detail.value = await getAiDatabaseById(databaseId.value);

    if (!isMounted.value) return;
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.database.loadDetailFailed"));
  }
}

async function loadRecords() {
  recordsLoading.value = true;
  try {
    const result  = await getAiDatabaseRecordsPaged(databaseId.value, {
      pageIndex: pageIndex.value,
      pageSize: pageSize.value
    });

    if (!isMounted.value) return;
    records.value = result.items;
    total.value = Number(result.total);
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.database.loadRecordsFailed"));
  } finally {
    recordsLoading.value = false;
  }
}

async function refreshImportProgress() {
  try {
    importProgress.value = await getAiDatabaseImportProgress(databaseId.value);

    if (!isMounted.value) return;
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
    const result  = await validateAiDatabaseSchema(databaseId.value, detail.value.tableSchema);

    if (!isMounted.value) return;
    if (result.isValid) {
      message.success(t("ai.database.schemaValidOk"));
    } else {
      message.error(t("ai.database.schemaValidFailed", { errors: result.errors.join("; ") }));
    }
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.database.schemaValidError"));
  } finally {
    schemaValidating.value = false;
  }
}

async function downloadTemplate() {
  templateDownloading.value = true;
  try {
    const blob  = await downloadAiDatabaseTemplate(databaseId.value);

    if (!isMounted.value) return;
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `ai-database-${databaseId.value}-template.csv`;
    link.click();
    URL.revokeObjectURL(url);
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.database.downloadTemplateFailed"));
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

    if (!isMounted.value) return;
  } catch {
    return;
  }

  recordSubmitting.value = true;
  try {
    if (editingRecordId.value) {
      await updateAiDatabaseRecord(databaseId.value, editingRecordId.value, { dataJson: recordForm.dataJson });

      if (!isMounted.value) return;
      message.success(t("ai.database.recordUpdateOk"));
    } else {
      await createAiDatabaseRecord(databaseId.value, { dataJson: recordForm.dataJson });

      if (!isMounted.value) return;
      message.success(t("ai.database.recordCreateOk"));
    }

    recordModalOpen.value = false;
    await Promise.all([loadDetail(), loadRecords()]);

    if (!isMounted.value) return;
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.database.recordSubmitFailed"));
  } finally {
    recordSubmitting.value = false;
  }
}

async function handleDeleteRecord(recordId: number) {
  try {
    await deleteAiDatabaseRecord(databaseId.value, recordId);

    if (!isMounted.value) return;
    message.success(t("crud.deleteSuccess"));
    await Promise.all([loadDetail(), loadRecords()]);

    if (!isMounted.value) return;
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.database.deleteRecordFailed"));
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
  if (status === 1) return t("ai.database.importRunning");
  if (status === 2) return t("ai.database.importDone");
  if (status === 3) return t("ai.database.importFailed");
  return t("ai.database.importPending");
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
