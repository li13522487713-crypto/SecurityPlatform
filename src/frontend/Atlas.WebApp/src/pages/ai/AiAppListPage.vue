<template>
  <a-card :title="t('ai.app.listTitle')" :bordered="false">
    <div class="toolbar">
      <a-space wrap>
        <a-input-search
          v-model:value="keyword"
          :placeholder="t('ai.app.searchPlaceholder')"
          style="width: 260px"
          @search="loadData"
        />
        <a-button @click="handleReset">{{ t("common.reset") }}</a-button>
        <a-button type="primary" @click="goCreate">{{ t("ai.app.newApp") }}</a-button>
      </a-space>
    </div>

    <a-table row-key="id" :columns="columns" :data-source="list" :loading="loading" :pagination="false">
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'status'">
          <a-tag :color="record.status === 1 ? 'green' : 'default'">
            {{ record.status === 1 ? t("ai.app.statusPublished") : t("ai.app.statusDraft") }}
          </a-tag>
        </template>
        <template v-if="column.key === 'action'">
          <a-space>
            <a-button type="link" @click="goEdit(record.id)">{{ t("common.edit") }}</a-button>
            <a-button type="link" @click="handlePublish(record.id)">{{ t("ai.workflow.publish") }}</a-button>
            <a-button type="link" @click="showVersion(record.id)">{{ t("ai.workflow.colVersion") }}</a-button>
            <a-button type="link" @click="openCopy(record.id)">{{ t("ai.app.resourceCopy") }}</a-button>
            <a-popconfirm :title="t('ai.app.deleteConfirm')" @confirm="handleDelete(record.id)">
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
        @change="loadData"
      />
    </div>

    <a-modal
      v-model:open="versionModalOpen"
      :title="t('ai.app.versionModalTitle')"
      :footer="null"
    >
      <a-descriptions v-if="versionInfo" bordered :column="1" size="small">
        <a-descriptions-item :label="t('ai.app.currentPublishVersion')">{{ versionInfo.currentPublishVersion }}</a-descriptions-item>
        <a-descriptions-item :label="t('ai.app.latestVersion')">{{ versionInfo.latestVersion ?? "-" }}</a-descriptions-item>
        <a-descriptions-item :label="t('ai.app.latestPublishedAt')">{{ versionInfo.latestPublishedAt ?? "-" }}</a-descriptions-item>
      </a-descriptions>
    </a-modal>

    <a-modal
      v-model:open="copyModalOpen"
      :title="t('ai.app.copyModalTitle')"
      :confirm-loading="copySubmitting"
      @ok="submitCopyTask"
      @cancel="copyModalOpen = false"
    >
      <a-form layout="vertical">
        <a-form-item :label="t('ai.app.labelSourceAppId')">
          <a-input-number v-model:value="copySourceAppId" :min="1" style="width: 100%" />
        </a-form-item>
        <a-form-item v-if="copyProgress">
          <a-alert
            type="info"
            show-icon
            :message="copyTaskMessage"
            :description="copyTaskDescription"
          />
        </a-form-item>
      </a-form>
    </a-modal>
  </a-card>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, onUnmounted } from "vue";
import { useI18n } from "vue-i18n";

const { t } = useI18n();

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { useRouter } from "vue-router";
import { message } from "ant-design-vue";
import {
  checkAiAppVersion,
  deleteAiApp,
  getAiAppLatestResourceCopyTask,
  getAiAppsPaged,
  publishAiApp,
  submitAiAppResourceCopy,
  type AiAppListItem,
  type AiAppResourceCopyTaskProgress,
  type AiAppVersionCheckResult
} from "@/services/api-ai-app";

const router = useRouter();
const keyword = ref("");
const list = ref<AiAppListItem[]>([]);
const loading = ref(false);
const pageIndex = ref(1);
const pageSize = ref(20);
const total = ref(0);

const columns = computed(() => [
  { title: t("ai.promptLib.colName"), dataIndex: "name", key: "name", width: 220 },
  { title: t("ai.promptLib.labelDescription"), dataIndex: "description", key: "description", ellipsis: true },
  { title: t("ai.workflow.colStatus"), key: "status", width: 100 },
  { title: t("ai.app.colPublishVersion"), dataIndex: "publishVersion", key: "publishVersion", width: 100 },
  { title: t("ai.workflow.colUpdatedAt"), dataIndex: "updatedAt", key: "updatedAt", width: 200 },
  { title: t("ai.colActions"), key: "action", width: 360 }
]);

const versionModalOpen = ref(false);
const versionInfo = ref<AiAppVersionCheckResult | null>(null);

const copyModalOpen = ref(false);
const copyAppId = ref<number | null>(null);
const copySourceAppId = ref<number | undefined>(undefined);
const copySubmitting = ref(false);
const copyProgress = ref<AiAppResourceCopyTaskProgress | null>(null);

const copyTaskMessage = computed(() => {
  if (!copyProgress.value) return "";
  return t("ai.app.copyTaskMsg", {
    taskId: copyProgress.value.taskId,
    status: copyStatusLabel(copyProgress.value.status)
  });
});

const copyTaskDescription = computed(() => {
  if (!copyProgress.value) return "";
  const p = copyProgress.value;
  return t("ai.app.copyTaskDesc", {
    total: p.totalItems,
    copied: p.copiedItems,
    err: p.errorMessage ? t("ai.app.copyTaskError", { msg: p.errorMessage }) : ""
  });
});

async function loadData() {
  loading.value = true;
  try {
    const result  = await getAiAppsPaged(
      { pageIndex: pageIndex.value, pageSize: pageSize.value },
      keyword.value || undefined
    );

    if (!isMounted.value) return;
    list.value = result.items;
    total.value = Number(result.total);
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.app.loadFailed"));
  } finally {
    loading.value = false;
  }
}

function handleReset() {
  keyword.value = "";
  pageIndex.value = 1;
  void loadData();
}

function goCreate() {
  void router.push("/ai/apps/0/edit");
}

function goEdit(id: number) {
  void router.push(`/ai/apps/${id}/edit`);
}

async function handlePublish(id: number) {
  try {
    await publishAiApp(id);

    if (!isMounted.value) return;
    message.success(t("ai.app.publishSuccess"));
    await loadData();

    if (!isMounted.value) return;
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.app.publishFailed"));
  }
}

async function showVersion(id: number) {
  try {
    versionInfo.value = await checkAiAppVersion(id);

    if (!isMounted.value) return;
    versionModalOpen.value = true;
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.app.versionQueryFailed"));
  }
}

async function openCopy(id: number) {
  copyAppId.value = id;
  copySourceAppId.value = undefined;
  copyProgress.value = null;
  copyModalOpen.value = true;
  try {
    copyProgress.value = await getAiAppLatestResourceCopyTask(id);

    if (!isMounted.value) return;
  } catch {
    copyProgress.value = null;
  }
}

async function submitCopyTask() {
  if (!copyAppId.value || !copySourceAppId.value) {
    message.warning(t("ai.app.warnSourceApp"));
    return;
  }

  copySubmitting.value = true;
  try {
    await submitAiAppResourceCopy(copyAppId.value, copySourceAppId.value);

    if (!isMounted.value) return;
    message.success(t("ai.app.copyTaskSubmitted"));
    copyProgress.value = await getAiAppLatestResourceCopyTask(copyAppId.value);

    if (!isMounted.value) return;
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.app.copyTaskFailed"));
  } finally {
    copySubmitting.value = false;
  }
}

async function handleDelete(id: number) {
  try {
    await deleteAiApp(id);

    if (!isMounted.value) return;
    message.success(t("crud.deleteSuccess"));
    await loadData();

    if (!isMounted.value) return;
  } catch (error: unknown) {
    message.error((error as Error).message || t("crud.deleteFailed"));
  }
}

function copyStatusLabel(status: number) {
  if (status === 1) return t("ai.database.importRunning");
  if (status === 2) return t("ai.database.importDone");
  if (status === 3) return t("ai.database.importFailed");
  return t("ai.database.importPending");
}

onMounted(() => {
  void loadData();
});
</script>

<style scoped>
.toolbar {
  margin-bottom: 16px;
}

.pager {
  margin-top: 16px;
  display: flex;
  justify-content: flex-end;
}
</style>
