<template>
  <section class="appbridge-page">
    <h2>{{ t("appBridgeCommandCenter.title") }}</h2>
    <a-form layout="vertical">
      <a-row :gutter="12">
        <a-col :span="6">
          <a-form-item :label="t('appBridgeCommandCenter.appInstanceId')">
            <a-input v-model:value="appInstanceId" />
          </a-form-item>
        </a-col>
        <a-col :span="6">
          <a-form-item :label="t('appBridgeCommandCenter.commandType')">
            <a-input v-model:value="commandType" />
          </a-form-item>
        </a-col>
        <a-col :span="12">
          <a-form-item :label="t('appBridgeCommandCenter.payloadJson')">
            <a-input v-model:value="payloadJson" />
          </a-form-item>
        </a-col>
      </a-row>
      <a-row :gutter="12">
        <a-col :span="12">
          <a-form-item :label="t('appBridgeCommandCenter.reason')">
            <a-input v-model:value="reason" />
          </a-form-item>
        </a-col>
      </a-row>
      <a-space>
        <a-switch
          v-model:checked="dryRun"
          :checked-children="t('appBridgeCommandCenter.dryRunEnabled')"
          :un-checked-children="t('appBridgeCommandCenter.dryRunDisabled')"
        />
        <a-button type="primary" :loading="creating" @click="createCommand">
          {{ t("appBridgeCommandCenter.createCommand") }}
        </a-button>
      </a-space>
    </a-form>

    <a-divider />

    <div class="appbridge-page__toolbar">
      <TableViewToolbar :controller="tableViewController" />
    </div>

    <a-table
      row-key="commandId"
      :columns="tableColumns"
      :data-source="rows"
      :loading="loading"
      :pagination="pagination"
      :size="tableSize"
      @change="onTableChange"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'actions'">
          <a-button type="link" size="small" @click="previewCommand(record.commandId)">
            {{ t("appBridgeCommandCenter.viewResult") }}
          </a-button>
        </template>
      </template>
    </a-table>

    <a-modal
      v-model:open="previewVisible"
      :title="t('appBridgeCommandCenter.previewTitle')"
      :footer="null"
      width="860px"
      :confirm-loading="previewLoading"
    >
      <a-typography-paragraph>
        <strong>{{ t("appBridgeCommandCenter.status") }}：</strong>{{ previewStatus }}
      </a-typography-paragraph>
      <a-typography-paragraph>
        <strong>{{ t("appBridgeCommandCenter.riskLevel") }}：</strong>{{ previewRiskLevel }}
      </a-typography-paragraph>
      <a-typography-paragraph>
        <strong>{{ t("appBridgeCommandCenter.message") }}：</strong>{{ previewMessage }}
      </a-typography-paragraph>
      <a-textarea :value="previewContent" :rows="12" readonly />
    </a-modal>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from "vue";
import { message } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import { useTableView, type PagedRequest, type TableViewColumn } from "@atlas/shared-core";
import { TableViewToolbar } from "@atlas/shared-ui";
import type { AppCommandListItem } from "@/types/platform-console";
import { createAppBridgeCommand, getAppBridgeCommandDetail, getAppBridgeCommands } from "@/services/api-appbridge";
import { tableViewApi } from "@/services/api-table-views";

const { t } = useI18n();

const appInstanceId = ref("1");
const commandType = ref("organization.sync-structure");
const payloadJson = ref("{\"force\":true}");
const reason = ref("");
const dryRun = ref(false);

const creating = ref(false);
const loading = ref(false);
const rows = ref<AppCommandListItem[]>([]);
const previewVisible = ref(false);
const previewLoading = ref(false);
const previewContent = ref("{}");
const previewStatus = ref("-");
const previewRiskLevel = ref("-");
const previewMessage = ref("-");
const request = ref<PagedRequest>({
  pageIndex: 1,
  pageSize: 20,
  keyword: "",
  sortBy: "",
  sortDesc: true
});
const pagination = reactive({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: true
});

const baseColumns = computed<TableViewColumn<AppCommandListItem>[]>(() => [
  { title: t("appBridgeCommandCenter.colCommandId"), dataIndex: "commandId", key: "commandId", width: 180 },
  { title: t("appBridgeCommandCenter.colAppInstanceId"), dataIndex: "appInstanceId", key: "appInstanceId", width: 120 },
  { title: t("appBridgeCommandCenter.colCommandType"), dataIndex: "commandType", key: "commandType", width: 180 },
  { title: t("appBridgeCommandCenter.colRiskLevel"), dataIndex: "riskLevel", key: "riskLevel", width: 120 },
  {
    title: t("appBridgeCommandCenter.colDryRun"),
    dataIndex: "dryRun",
    key: "dryRun",
    width: 90,
    customRender: ({ record }) =>
      (record.dryRun ? t("appBridgeCommandCenter.yes") : t("appBridgeCommandCenter.no"))
  },
  { title: t("appBridgeCommandCenter.colStatus"), dataIndex: "status", key: "status", width: 120 },
  { title: t("appBridgeCommandCenter.colInitiator"), dataIndex: "initiator", key: "initiator", width: 100 },
  { title: t("appBridgeCommandCenter.colUpdatedAt"), dataIndex: "updatedAt", key: "updatedAt", width: 220 },
  { title: t("appBridgeCommandCenter.colActions"), key: "actions", width: 120, fixed: "right", view: { canHide: false } }
]);

const { controller: tableViewController, tableColumns, tableSize } = useTableView<AppCommandListItem>({
  tableKey: "appbridge.command-center",
  columns: baseColumns,
  pagination,
  onRefresh: () => {
    void loadCommands();
  },
  api: tableViewApi,
  translate: (key: string, params?: Record<string, unknown>) =>
    String(t(key, (params ?? {}) as Record<string, string | number | boolean>))
});

async function loadCommands() {
  loading.value = true;
  try {
    const queryRequest: PagedRequest = {
      ...request.value,
      pageIndex: pagination.current ?? 1,
      pageSize: pagination.pageSize ?? 20
    };
    const data = await getAppBridgeCommands(queryRequest, appInstanceId.value || undefined);
    rows.value = data.items;
    pagination.total = Number(data.total) || 0;
  } finally {
    loading.value = false;
  }
}

async function createCommand() {
  creating.value = true;
  try {
    await createAppBridgeCommand({
      appInstanceId: appInstanceId.value,
      commandType: commandType.value,
      payloadJson: payloadJson.value,
      dryRun: dryRun.value,
      reason: reason.value || undefined
    });
    message.success(t("appBridgeCommandCenter.createSuccess"));
    await loadCommands();
  } catch (error) {
    message.error((error as Error).message || t("appBridgeCommandCenter.createFailed"));
  } finally {
    creating.value = false;
  }
}

function onTableChange(page: { current?: number; pageSize?: number }) {
  pagination.current = page.current ?? 1;
  pagination.pageSize = page.pageSize ?? 20;
  void loadCommands();
}

async function previewCommand(commandId: string) {
  previewVisible.value = true;
  previewLoading.value = true;
  try {
    const detail = await getAppBridgeCommandDetail(commandId);
    previewStatus.value = detail.status;
    previewRiskLevel.value = detail.riskLevel;
    previewMessage.value = detail.message;
    previewContent.value = detail.resultJson || detail.payloadJson || "{}";
  } catch (error) {
    message.error((error as Error).message || t("appBridgeCommandCenter.previewFailed"));
  } finally {
    previewLoading.value = false;
  }
}

onMounted(() => {
  void loadCommands();
});
</script>

<style scoped>
.appbridge-page {
  display: flex;
  flex-direction: column;
  gap: 12px;
}

.appbridge-page__toolbar {
  display: flex;
  justify-content: flex-end;
}
</style>
