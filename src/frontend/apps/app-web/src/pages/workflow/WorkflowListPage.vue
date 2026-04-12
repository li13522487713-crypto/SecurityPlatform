<template>
  <a-card :bordered="false" data-testid="app-workflows-page">
    <template #title>{{ t("workflowList.pageTitle") }}</template>
    <template #extra>
      <a-button type="primary" data-testid="app-workflows-create" @click="createWorkflow">
        {{ t("workflowList.create") }}
      </a-button>
    </template>

    <a-table
      data-testid="app-workflows-table"
      row-key="id"
      :data-source="items"
      :columns="columns"
      :loading="loading"
      :pagination="pagination"
      @change="handleTableChange"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'name'">
          <a-button
            type="link"
            :data-testid="`app-workflows-open-${record.id}`"
            @click="openEditor(record.id)"
          >
            {{ record.name }}
          </a-button>
        </template>
        <template v-else-if="column.key === 'mode'">
          {{ renderMode(record.mode) }}
        </template>
        <template v-else-if="column.key === 'updatedAt'">
          {{ formatDate(record.updatedAt) }}
        </template>
        <template v-else-if="column.key === 'actions'">
          <a-space wrap>
            <a-button
              type="link"
              :data-testid="`app-workflows-edit-${record.id}`"
              @click="openEditor(record.id)"
            >
              {{ t("workflowList.actions.edit") }}
            </a-button>
            <a-button
              type="link"
              :data-testid="`app-workflows-view-${record.id}`"
              :disabled="!canOpenPublishedView(record)"
              @click="openPublishedView(record.id)"
            >
              {{ t("workflowList.actions.viewPublished") }}
            </a-button>
            <a-button
              type="link"
              :data-testid="`app-workflows-versions-${record.id}`"
              @click="openVersionHistory(record)"
            >
              {{ t("workflowList.actions.versionHistory") }}
            </a-button>
            <a-button
              type="link"
              danger
              :data-testid="`app-workflows-delete-${record.id}`"
              @click="removeWorkflow(record.id)"
            >
              {{ t("workflowList.actions.delete") }}
            </a-button>
          </a-space>
        </template>
      </template>
    </a-table>
  </a-card>

  <a-drawer
    :open="versionDrawerVisible"
    :title="versionDrawerTitle"
    :width="560"
    destroy-on-close
    data-testid="app-workflows-version-drawer"
    @close="closeVersionDrawer"
  >
    <a-spin :spinning="versionLoading">
      <a-empty v-if="versions.length === 0" :description="t('workflowList.versionDrawer.empty')" />
      <div v-else class="version-list">
        <div v-for="version in versions" :key="version.id" class="version-item">
          <div class="version-item__header">
            <div>
              <div class="version-item__title">v{{ version.versionNumber }}</div>
              <div class="version-item__time">{{ formatDate(version.publishedAt) }}</div>
            </div>
            <a-tag color="blue">ID {{ version.id }}</a-tag>
          </div>
          <div class="version-item__body">
            <div class="version-item__meta">
              <span>{{ t("workflowList.versionDrawer.publisher") }} {{ version.publishedByUserId }}</span>
            </div>
            <div class="version-item__log">
              {{ version.changeLog || t("workflowList.versionDrawer.noChangeLog") }}
            </div>
          </div>
          <div class="version-item__actions">
            <a-button size="small" @click="openVersionView(version.id)">
              {{ t("workflowList.versionDrawer.viewVersion") }}
            </a-button>
            <a-button size="small" @click="openPublishedView(activeWorkflowId)">
              {{ t("workflowList.versionDrawer.viewLatest") }}
            </a-button>
            <a-button
              size="small"
              type="primary"
              ghost
              @click="confirmRollback(version.id, version.versionNumber)"
            >
              {{ t("workflowList.versionDrawer.rollback") }}
            </a-button>
          </div>
        </div>
      </div>
    </a-spin>
  </a-drawer>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useI18n } from "vue-i18n";
import { message, Modal } from "ant-design-vue";
import { workflowV2Api } from "@/services/api-workflow-v2";
import type { WorkflowListItem, WorkflowVersionItem } from "@/types/workflow-v2";

const route = useRoute();
const router = useRouter();
const { t } = useI18n();

const loading = ref(false);
const pageIndex = ref(1);
const pageSize = ref(20);
const total = ref(0);
const items = ref<WorkflowListItem[]>([]);
const versionDrawerVisible = ref(false);
const versionLoading = ref(false);
const activeWorkflowId = ref("");
const activeWorkflowName = ref("");
const versions = ref<WorkflowVersionItem[]>([]);

const columns = computed(() => [
  { title: t("workflowList.columns.name"), dataIndex: "name", key: "name" },
  { title: t("workflowList.columns.mode"), dataIndex: "mode", key: "mode" },
  { title: t("workflowList.columns.version"), dataIndex: "latestVersionNumber", key: "version" },
  { title: t("workflowList.columns.updatedAt"), dataIndex: "updatedAt", key: "updatedAt" },
  { title: t("workflowList.columns.actions"), key: "actions", width: 260 }
]);

const pagination = computed(() => ({
  current: pageIndex.value,
  pageSize: pageSize.value,
  total: total.value,
  showSizeChanger: true
}));

const versionDrawerTitle = computed(() =>
  activeWorkflowName.value
    ? t("workflowList.versionDrawer.title", { name: activeWorkflowName.value })
    : t("workflowList.versionDrawer.fallbackTitle")
);

async function load() {
  loading.value = true;
  try {
    const res = await workflowV2Api.list(pageIndex.value, pageSize.value);
    if (res.success && res.data) {
      items.value = res.data.items;
      total.value = Number(res.data.total ?? 0);
    }
  } catch (error) {
    items.value = [];
    total.value = 0;
    message.error(error instanceof Error ? error.message : t("workflowList.loadFailed"));
  } finally {
    loading.value = false;
  }
}

function renderMode(mode: number) {
  return mode === 1 ? t("workflowList.modes.chatflow") : t("workflowList.modes.standard");
}

function formatDate(value?: string) {
  if (!value) {
    return "-";
  }

  const date = new Date(value);
  return Number.isNaN(date.getTime()) ? value : date.toLocaleString();
}

function openEditor(id: string) {
  void router.push({
    name: "app-workflow-editor",
    params: { appKey: String(route.params.appKey), id }
  });
}

function openPublishedView(id: string) {
  void router.push({
    name: "app-workflow-editor",
    params: { appKey: String(route.params.appKey), id },
    query: { readOnly: "1", source: "published" }
  });
}

function openVersionView(versionId: string) {
  if (!activeWorkflowId.value) {
    return;
  }

  void router.push({
    name: "app-workflow-editor",
    params: { appKey: String(route.params.appKey), id: activeWorkflowId.value },
    query: { readOnly: "1", source: "published", versionId }
  });
}

function canOpenPublishedView(record: WorkflowListItem) {
  return Number(record.status) === 1 && Number(record.latestVersionNumber ?? 0) > 0;
}

async function openVersionHistory(record: WorkflowListItem) {
  versionDrawerVisible.value = true;
  activeWorkflowId.value = String(record.id);
  activeWorkflowName.value = record.name;
  await loadVersions(activeWorkflowId.value, activeWorkflowName.value);
}

async function loadVersions(workflowId: string, workflowName: string) {
  versionLoading.value = true;
  activeWorkflowId.value = workflowId;
  activeWorkflowName.value = workflowName;
  try {
    const res = await workflowV2Api.getVersions(workflowId);
    versions.value = res.data ?? [];
  } catch (error) {
    versions.value = [];
    message.error(error instanceof Error ? error.message : t("workflowList.versionDrawer.loadFailed"));
  } finally {
    versionLoading.value = false;
  }
}

function closeVersionDrawer() {
  versionDrawerVisible.value = false;
  activeWorkflowId.value = "";
  activeWorkflowName.value = "";
  versions.value = [];
}

async function createWorkflow() {
  const res = await workflowV2Api.create({
    name: `${t("workflowList.newWorkflowNamePrefix")}_${Date.now().toString().slice(-6)}`,
    mode: 0
  });
  if (res.success && res.data?.id) {
    message.success(t("workflowList.createSuccess"));
    openEditor(res.data.id);
  }
}

function confirmRollback(versionId: string, versionNumber: number) {
  if (!activeWorkflowId.value) {
    return;
  }

  Modal.confirm({
    title: t("workflowList.versionDrawer.rollbackConfirmTitle"),
    content: t("workflowList.versionDrawer.rollbackConfirmContent", { version: versionNumber }),
    async onOk() {
      const res = await workflowV2Api.rollbackVersion(activeWorkflowId.value, versionId);
      if (res.success) {
        message.success(t("workflowList.versionDrawer.rollbackSuccess"));
        await Promise.all([load(), loadVersions(activeWorkflowId.value, activeWorkflowName.value)]);
      }
    }
  });
}

function removeWorkflow(id: string) {
  Modal.confirm({
    title: t("workflowList.deleteConfirmTitle"),
    content: t("workflowList.deleteConfirmContent"),
    async onOk() {
      const res = await workflowV2Api.delete(id);
      if (res.success) {
        message.success(t("workflowList.deleteSuccess"));
        await load();
      }
    }
  });
}

function handleTableChange(pag: { current?: number; pageSize?: number }) {
  pageIndex.value = pag.current ?? 1;
  pageSize.value = pag.pageSize ?? 20;
  void load();
}

onMounted(() => {
  void load();
});
</script>

<style scoped>
.version-list {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.version-item {
  border: 1px solid #eef2f7;
  border-radius: 16px;
  padding: 16px;
  background: #fff;
}

.version-item__header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 12px;
}

.version-item__title {
  font-size: 16px;
  font-weight: 700;
  color: #111827;
}

.version-item__time {
  margin-top: 4px;
  color: #6b7280;
  font-size: 12px;
}

.version-item__body {
  margin-top: 12px;
  display: flex;
  flex-direction: column;
  gap: 8px;
}

.version-item__meta {
  color: #4b5563;
  font-size: 13px;
}

.version-item__log {
  color: #111827;
  line-height: 1.7;
  background: #f8fafc;
  border-radius: 12px;
  padding: 12px;
  white-space: pre-wrap;
  word-break: break-word;
}

.version-item__actions {
  margin-top: 12px;
  display: flex;
  gap: 8px;
  flex-wrap: wrap;
}
</style>
