<template>
  <PageContainer title="指令中心">
    <a-form layout="vertical">
      <a-row :gutter="12">
        <a-col :span="8">
          <a-form-item label="目标应用">
            <a-select
              v-model:value="appKey"
              :options="appOptions"
              :loading="loadingOnlineApps"
              placeholder="请选择目标应用"
            />
          </a-form-item>
        </a-col>
        <a-col :span="8">
          <a-form-item label="指令类型">
            <a-select
              v-model:value="commandType"
              :options="commandOptions"
              :loading="loadingOperations"
              :disabled="!appKey"
              placeholder="请选择指令"
            />
          </a-form-item>
        </a-col>
        <a-col :span="8">
          <a-form-item label="备注">
            <a-input v-model:value="messageText" />
          </a-form-item>
        </a-col>
      </a-row>
      <a-button
        type="primary"
        :loading="dispatching"
        :disabled="!canExecuteCommands"
        @click="submitCommand"
      >
        下发指令
      </a-button>
    </a-form>
    <a-divider />
    <CommandDispatchPanel :logs="commandHistory" />
  </PageContainer>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from "vue";
import { message } from "ant-design-vue";
import {
  appendConnectorCommandLog,
  createConnectorCommandFailureLog,
  createConnectorRequestContext,
  fetchConnectorOnlineApps,
  listConnectorOperations,
  type ConnectorCommandLogEntry,
  type ConnectorOnlineAppSummary,
  type ConnectorOperation,
  executeConnectorCommand,
  getConnectorCommandHistory
} from "@atlas/connector-core";
import { getAuthProfile, hasPermission } from "@atlas/shared-core";
import { PageContainer } from "@atlas/shared-ui";
import { requestApi } from "@/services/api-core";
import { CommandDispatchPanel } from "@atlas/connector-core";

const rows = ref<ConnectorOnlineAppSummary[]>([]);
const appKey = ref<string>("");
const commandType = ref<string>("");
const messageText = ref<string>("");
const operations = ref<ConnectorOperation[]>([]);
const commandHistory = ref<ConnectorCommandLogEntry[]>([]);
const loadingOnlineApps = ref(false);
const loadingOperations = ref(false);
const dispatching = ref(false);
const profile = ref(getAuthProfile());

const appOptions = computed(() => rows.value.map((row) => ({
  label: `${row.appName} (${row.appKey})`,
  value: row.appKey,
})));

const commandOptions = computed(() => operations.value.map((operation) => ({
  label: `${operation.operationId} (${operation.method.toUpperCase()} ${operation.path})`,
  value: operation.operationId
})));

const canExecuteCommands = computed(() => hasPermission(profile.value, "connectors:execute"));

const context = createConnectorRequestContext({ requestApi });

async function loadOnlineApps() {
  loadingOnlineApps.value = true;
  try {
    rows.value = await fetchConnectorOnlineApps(context);
  } catch (error) {
    rows.value = [];
    message.error((error as Error).message || "加载连接器列表失败");
  } finally {
    loadingOnlineApps.value = false;
  }
}

function parseConnectorId() {
  const target = Number(appKey.value);
  if (!Number.isFinite(target)) return null;
  return Number(target);
}

async function loadOperations() {
  const connectorId = parseConnectorId();
  if (connectorId === null) {
    operations.value = [];
    return;
  }

  loadingOperations.value = true;
  try {
    operations.value = await listConnectorOperations(context, connectorId);
    if (operations.value.length === 0) {
      commandType.value = "";
      return;
    }
    commandType.value = operations.value[0]?.operationId ?? "";
  } catch (error) {
    operations.value = [];
    commandType.value = "";
    message.error((error as Error).message || "加载可用指令失败");
  } finally {
    loadingOperations.value = false;
  }
}

async function submitCommand() {
  if (!canExecuteCommands.value) {
    message.warning("当前账号暂无下发指令权限");
    return;
  }
  const connectorId = parseConnectorId();
  if (connectorId === null) {
    message.warning("请先选择目标应用");
    return;
  }
  if (!commandType.value) {
    message.warning("请先选择命令类型");
    return;
  }

  dispatching.value = true;
  try {
    const payloadText = messageText.value ? JSON.stringify({ reason: messageText.value }) : null;
    const logEntry = await executeConnectorCommand(context, {
      connectorId,
      commandType: commandType.value,
      payload: payloadText
    });
    commandHistory.value = appendConnectorCommandLog(logEntry);
    message.success("指令已下发");
    messageText.value = "";
  } catch (error) {
    const log = createConnectorCommandFailureLog({
      appKey: appKey.value,
      commandType: commandType.value,
      message: (error as Error).message || "指令下发失败"
    });
    commandHistory.value = appendConnectorCommandLog(log);
    message.error(log.message);
  } finally {
    dispatching.value = false;
  }
}

watch(appKey, () => {
  void loadOperations();
});

onMounted(() => {
  void loadOnlineApps();
  commandHistory.value = getConnectorCommandHistory();
});

</script>
