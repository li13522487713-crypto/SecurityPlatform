<template>
  <PageContainer title="指令中心">
    <a-form layout="vertical">
      <a-row :gutter="12">
        <a-col :span="8">
          <a-form-item label="目标应用">
            <a-select v-model:value="appKey" :options="appOptions" />
          </a-form-item>
        </a-col>
        <a-col :span="8">
          <a-form-item label="指令类型">
            <a-select v-model:value="commandType" :options="commandOptions" />
          </a-form-item>
        </a-col>
        <a-col :span="8">
          <a-form-item label="备注">
            <a-input v-model:value="messageText" />
          </a-form-item>
        </a-col>
      </a-row>
      <a-button type="primary" @click="submitCommand">下发指令</a-button>
    </a-form>
    <a-divider />
    <CommandDispatchPanel />
  </PageContainer>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";
import { message } from "ant-design-vue";
import { dispatchMockCommand, getMockOnlineApps } from "@atlas/connector-core";
import { PageContainer } from "@atlas/shared-ui";
import { CommandDispatchPanel } from "@atlas/connector-core";

const rows = computed(() => getMockOnlineApps());
const appKey = ref<string>("");
const commandType = ref<string>("sync-organization");
const messageText = ref<string>("");

const appOptions = computed(() => rows.value.map((row) => ({
  label: `${row.appName} (${row.appKey})`,
  value: row.appKey,
})));

const commandOptions = [
  { label: "同步组织架构", value: "sync-organization" },
  { label: "刷新权限缓存", value: "refresh-permission-cache" },
  { label: "触发运行态刷新", value: "refresh-runtime" },
];

function submitCommand() {
  if (!appKey.value) {
    message.warning("请先选择目标应用");
    return;
  }

  dispatchMockCommand({
    appKey: appKey.value,
    commandType: commandType.value,
    message: messageText.value || undefined,
  });
  message.success("指令已下发");
  messageText.value = "";
}
</script>
