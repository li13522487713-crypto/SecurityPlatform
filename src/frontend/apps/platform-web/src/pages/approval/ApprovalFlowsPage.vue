<template>
  <CrudPageLayout :title="t('approvalFlowList.pageTitle')">
    <template #toolbar-actions>
      <a-button @click="importModalOpen = true">{{ t("approvalFlowList.importJson") }}</a-button>
      <a-button type="primary" @click="handleCreate">{{ t("approvalFlowList.createFlow") }}</a-button>
    </template>
    <template #table>
      <a-table
        :columns="tableColumns"
        :data-source="dataSource"
        :pagination="{ ...pagination, showTotal: (total: number) => t('crud.totalItems', { total }) }"
        :loading="loading"
        row-key="id"
        @change="onTableChange"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'status'">
            <a-tag :color="getStatusColor(record.status)">
              {{ getStatusText(record.status) }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'action'">
            <a-space>
              <a-button type="link" size="small" @click="handleDesign(record.id)">{{ t("approvalFlowList.design") }}</a-button>
              <a-button type="link" size="small" @click="handleCopy(record.id)">{{ t("approvalFlowList.copy") }}</a-button>
              <a-button type="link" size="small" @click="handleExport(record.id)">{{ t("approvalFlowList.export") }}</a-button>
              <a-button type="link" size="small" @click="openCompareModal(record)">{{ t("approvalFlowList.compare") }}</a-button>
              <a-button v-if="record.status === 0" type="link" size="small" @click="handlePublish(record.id)">
                {{ t("approvalFlowList.publish") }}
              </a-button>
              <a-button v-if="record.status === 1" type="link" size="small" danger @click="handleDisable(record.id)">
                {{ t("approvalFlowList.disable") }}
              </a-button>
              <a-popconfirm :title="t('approvalFlowList.deleteConfirm')" @confirm="handleDelete(record.id)">
                <a-button type="link" size="small" danger>{{ t("approvalFlowList.delete") }}</a-button>
              </a-popconfirm>
            </a-space>
          </template>
        </template>
      </a-table>

      <a-modal
        v-model:open="importModalOpen"
        :title="t('approvalFlowList.importModalTitle')"
        :ok-text="t('approvalFlowList.importOk')"
        :cancel-text="t('common.cancel')"
        :confirm-loading="importLoading"
        @ok="handleImportConfirm"
      >
        <a-form layout="vertical">
          <a-form-item :label="t('approvalFlowList.flowName')" required>
            <a-input v-model:value="importName" :placeholder="t('approvalFlowList.flowNamePlaceholder')" />
          </a-form-item>
          <a-form-item :label="t('approvalFlowList.definitionJson')" required>
            <a-textarea
              v-model:value="importDefinitionJson"
              :rows="10"
              :placeholder="t('approvalFlowList.definitionJsonPlaceholder')"
            />
          </a-form-item>
        </a-form>
      </a-modal>

      <a-modal
        v-model:open="compareModalOpen"
        :title="t('approvalFlowList.compareModalTitle')"
        :ok-text="t('approvalFlowList.compareOk')"
        :cancel-text="t('approvalFlowList.compareClose')"
        :confirm-loading="compareLoading"
        @ok="handleCompareConfirm"
      >
        <a-form layout="vertical">
          <a-form-item :label="t('approvalFlowList.targetVersion')" required>
            <a-input-number
              v-model:value="compareTargetVersion"
              :min="1"
              style="width: 100%"
              :placeholder="t('approvalFlowList.targetVersionPlaceholder')"
            />
          </a-form-item>
        </a-form>

        <a-alert
          v-if="compareResult"
          :type="compareResult.isSame ? 'success' : 'info'"
          :message="compareResult.summary"
          show-icon
        />
        <a-list
          v-if="compareResult && compareResult.differences.length > 0"
          size="small"
          bordered
          style="margin-top: 12px"
          :data-source="compareResult.differences"
        >
          <template #renderItem="{ item }">
            <a-list-item>
              <div>
                <div>
                  <strong>{{ item.path }}</strong>
                </div>
                <div>{{ t("approvalFlowList.diffCurrentLabel") }}{{ item.sourceValue }}</div>
                <div>{{ t("approvalFlowList.diffTargetLabel") }}{{ item.targetValue }}</div>
              </div>
            </a-list-item>
          </template>
        </a-list>
      </a-modal>
    </template>
  </CrudPageLayout>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, reactive, ref } from "vue";
import { useI18n } from "vue-i18n";

import { useRouter } from "vue-router";
import {
  getApprovalFlowsPaged,
  deleteApprovalFlow,
  publishApprovalFlow,
  disableApprovalFlow,
  copyApprovalFlow,
  exportApprovalFlow,
  importApprovalFlow,
  compareApprovalFlowVersion
} from "@/services/api-approval";
import type { TablePaginationConfig } from "ant-design-vue";
import { ApprovalFlowStatus, type ApprovalFlowCompareResponse, type ApprovalFlowDefinitionListItem } from "@atlas/shared-core";
import { message } from "ant-design-vue";
import { CrudPageLayout } from "@atlas/shared-ui";

const { t } = useI18n();
const router = useRouter();

const isMounted = ref(false);
onUnmounted(() => {
  isMounted.value = false;
});

const tableColumns = computed(() => [
  { title: t("approvalFlowList.colName"), dataIndex: "name", key: "name" },
  { title: t("approvalFlowList.colVersion"), dataIndex: "version", key: "version" },
  { title: t("approvalFlowList.colStatus"), key: "status" },
  { title: t("approvalFlowList.colPublishedAt"), dataIndex: "publishedAt", key: "publishedAt" },
  { title: t("approvalFlowList.colActions"), key: "action", width: 200 }
]);

const dataSource = ref<ApprovalFlowDefinitionListItem[]>([]);
const loading = ref(false);
const importModalOpen = ref(false);
const importLoading = ref(false);
const importName = ref("");
const importDefinitionJson = ref("");
const compareModalOpen = ref(false);
const compareLoading = ref(false);
const compareFlowId = ref<string>();
const compareTargetVersion = ref<number>(1);
const compareResult = ref<ApprovalFlowCompareResponse | null>(null);
const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 10,
  total: 0
});

const fetchData = async () => {
  loading.value = true;
  try {
    const result = await getApprovalFlowsPaged({
      pageIndex: pagination.current ?? 1,
      pageSize: pagination.pageSize ?? 10
    });

    if (!isMounted.value) return;
    dataSource.value = result.items;
    pagination.total = result.total;
  } catch (err) {
    message.error(err instanceof Error ? err.message : t("crud.queryFailed"));
  } finally {
    loading.value = false;
  }
};

const onTableChange = (pager: TablePaginationConfig) => {
  pagination.current = pager.current;
  pagination.pageSize = pager.pageSize;
  void fetchData();
};

const getStatusColor = (status: ApprovalFlowStatus) => {
  switch (status) {
    case ApprovalFlowStatus.Draft:
      return "default";
    case ApprovalFlowStatus.Published:
      return "green";
    case ApprovalFlowStatus.Disabled:
      return "red";
    default:
      return "default";
  }
};

const getStatusText = (status: ApprovalFlowStatus) => {
  switch (status) {
    case ApprovalFlowStatus.Draft:
      return t("approvalFlowList.statusDraft");
    case ApprovalFlowStatus.Published:
      return t("approvalFlowList.statusPublished");
    case ApprovalFlowStatus.Disabled:
      return t("approvalFlowList.statusDisabled");
    default:
      return t("approvalWorkspace.statusUnknown");
  }
};

const handleCreate = () => {
  void router.push({ name: "approval-designer" });
};

const handleDesign = (id: string) => {
  void router.push({ name: "approval-designer", params: { id } });
};

const handleCopy = async (id: string) => {
  try {
    const result = await copyApprovalFlow(id);

    if (!isMounted.value) return;
    message.success(t("approvalFlowList.copySuccessDraft"));
    void router.push({ name: "approval-designer", params: { id: result.id } });
  } catch (err) {
    message.error(err instanceof Error ? err.message : t("approvalFlowList.copyFailed"));
  }
};

const handleExport = async (id: string) => {
  try {
    const result = await exportApprovalFlow(id);

    if (!isMounted.value) return;
    const content = JSON.stringify(result, null, 2);
    const blob = new Blob([content], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = `${result.name}-v${result.version}.json`;
    anchor.click();
    URL.revokeObjectURL(url);
    message.success(t("approvalFlowList.exportSuccess"));
  } catch (err) {
    message.error(err instanceof Error ? err.message : t("approvalFlowList.exportFailed"));
  }
};

const openCompareModal = (record: ApprovalFlowDefinitionListItem) => {
  compareFlowId.value = record.id;
  compareTargetVersion.value = Math.max(1, record.version - 1);
  compareResult.value = null;
  compareModalOpen.value = true;
};

const handleImportConfirm = async () => {
  if (!importName.value.trim()) {
    message.warning(t("approvalFlowList.warnFlowName"));
    return;
  }
  if (!importDefinitionJson.value.trim()) {
    message.warning(t("approvalFlowList.warnDefinitionJson"));
    return;
  }

  importLoading.value = true;
  try {
    await importApprovalFlow({
      name: importName.value.trim(),
      definitionJson: importDefinitionJson.value.trim()
    });

    if (!isMounted.value) return;
    message.success(t("approvalFlowList.importSuccess"));
    importModalOpen.value = false;
    importName.value = "";
    importDefinitionJson.value = "";
    await fetchData();

    if (!isMounted.value) return;
  } catch (err) {
    message.error(err instanceof Error ? err.message : t("approvalFlowList.importFailed"));
  } finally {
    importLoading.value = false;
  }
};

const handleCompareConfirm = async () => {
  if (!compareFlowId.value) {
    message.warning(t("approvalFlowList.warnNoFlowSelected"));
    return;
  }
  if (!compareTargetVersion.value || compareTargetVersion.value <= 0) {
    message.warning(t("approvalFlowList.warnInvalidTargetVersion"));
    return;
  }

  compareLoading.value = true;
  try {
    compareResult.value = await compareApprovalFlowVersion(compareFlowId.value, compareTargetVersion.value);

    if (!isMounted.value) return;
    message.success(t("approvalFlowList.compareComplete"));
  } catch (err) {
    compareResult.value = null;
    message.error(err instanceof Error ? err.message : t("approvalFlowList.compareFailed"));
  } finally {
    compareLoading.value = false;
  }
};

const handlePublish = async (id: string) => {
  try {
    await publishApprovalFlow(id);

    if (!isMounted.value) return;
    message.success(t("approvalFlowList.publishSuccess"));
    void fetchData();
  } catch (err) {
    message.error(err instanceof Error ? err.message : t("approvalFlowList.publishFailed"));
  }
};

const handleDisable = async (id: string) => {
  try {
    await disableApprovalFlow(id);

    if (!isMounted.value) return;
    message.success(t("approvalFlowList.disableSuccess"));
    void fetchData();
  } catch (err) {
    message.error(err instanceof Error ? err.message : t("approvalFlowList.disableFailed"));
  }
};

const handleDelete = async (id: string) => {
  try {
    await deleteApprovalFlow(id);

    if (!isMounted.value) return;
    message.success(t("approvalFlowList.deleteSuccess"));
    void fetchData();
  } catch (err) {
    message.error(err instanceof Error ? err.message : t("approvalFlowList.deleteFailed"));
  }
};

onMounted(() => {
  isMounted.value = true;
  void fetchData();
});
</script>
