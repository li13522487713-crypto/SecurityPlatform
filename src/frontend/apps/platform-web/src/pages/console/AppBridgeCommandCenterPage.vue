<template>
  <section class="appbridge-page">
    <h2>命令中心</h2>
    <a-form layout="vertical">
      <a-row :gutter="12">
        <a-col :span="6">
          <a-form-item label="实例ID">
            <a-input v-model:value="appInstanceId" />
          </a-form-item>
        </a-col>
        <a-col :span="6">
          <a-form-item label="命令类型">
            <a-input v-model:value="commandType" />
          </a-form-item>
        </a-col>
        <a-col :span="12">
          <a-form-item label="Payload(JSON)">
            <a-input v-model:value="payloadJson" />
          </a-form-item>
        </a-col>
      </a-row>
      <a-row :gutter="12">
        <a-col :span="12">
          <a-form-item label="原因（高风险命令必填）">
            <a-input v-model:value="reason" />
          </a-form-item>
        </a-col>
      </a-row>
      <a-space>
        <a-switch v-model:checked="dryRun" checked-children="DryRun" un-checked-children="Execute" />
        <a-button type="primary" :loading="creating" @click="createCommand">下发命令</a-button>
      </a-space>
    </a-form>

    <a-divider />

    <a-table
      row-key="commandId"
      :columns="columns"
      :data-source="rows"
      :loading="loading"
      :pagination="pagination"
      @change="onTableChange"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'actions'">
          <a-button type="link" size="small" @click="previewCommand(record.commandId)">查看结果</a-button>
        </template>
      </template>
    </a-table>

    <a-modal
      v-model:open="previewVisible"
      title="命令结果预览"
      :footer="null"
      width="860px"
      :confirm-loading="previewLoading"
    >
      <a-typography-paragraph><strong>状态：</strong>{{ previewStatus }}</a-typography-paragraph>
      <a-typography-paragraph><strong>风险等级：</strong>{{ previewRiskLevel }}</a-typography-paragraph>
      <a-typography-paragraph><strong>消息：</strong>{{ previewMessage }}</a-typography-paragraph>
      <a-textarea :value="previewContent" :rows="12" readonly />
    </a-modal>
  </section>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import type { TableColumnsType } from "ant-design-vue";
import { message } from "ant-design-vue";
import type { PagedRequest } from "@atlas/shared-core";
import type { AppCommandListItem } from "@/types/platform-console";
import { createAppBridgeCommand, getAppBridgeCommandDetail, getAppBridgeCommands } from "@/services/api-appbridge";

const appInstanceId = ref("1");
const commandType = ref("organization.sync-structure");
const payloadJson = ref("{\"force\":true}");
const reason = ref("");
const dryRun = ref(false);

const creating = ref(false);
const loading = ref(false);
const rows = ref<AppCommandListItem[]>([]);
const total = ref(0);
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

const columns = computed<TableColumnsType<AppCommandListItem>>(() => [
  { title: "命令ID", dataIndex: "commandId", key: "commandId", width: 180 },
  { title: "实例ID", dataIndex: "appInstanceId", key: "appInstanceId", width: 120 },
  { title: "命令类型", dataIndex: "commandType", key: "commandType", width: 180 },
  { title: "风险等级", dataIndex: "riskLevel", key: "riskLevel", width: 120 },
  {
    title: "DryRun",
    dataIndex: "dryRun",
    key: "dryRun",
    width: 90,
    customRender: ({ record }) => (record.dryRun ? "是" : "否")
  },
  { title: "状态", dataIndex: "status", key: "status", width: 120 },
  { title: "发起人", dataIndex: "initiator", key: "initiator", width: 100 },
  { title: "更新时间", dataIndex: "updatedAt", key: "updatedAt", width: 220 },
  { title: "操作", key: "actions", width: 120, fixed: "right" }
]);

const pagination = computed(() => ({
  current: request.value.pageIndex,
  pageSize: request.value.pageSize,
  total: total.value,
  showSizeChanger: true
}));

async function loadCommands() {
  loading.value = true;
  try {
    const data = await getAppBridgeCommands(request.value, appInstanceId.value || undefined);
    rows.value = data.items;
    total.value = data.total;
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
    message.success("命令已创建");
    await loadCommands();
  } catch (error) {
    message.error((error as Error).message || "创建命令失败");
  } finally {
    creating.value = false;
  }
}

function onTableChange(page: { current?: number; pageSize?: number }) {
  request.value = {
    ...request.value,
    pageIndex: page.current ?? 1,
    pageSize: page.pageSize ?? 20
  };
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
    message.error((error as Error).message || "加载命令结果失败");
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
</style>
