<template>
  <div class="app-db-migration-page">
    <a-page-header :title="t('console.appDbMigration.title')" :sub-title="t('console.appDbMigration.subtitle')">
      <template #extra>
        <a-space class="toolbar-actions" :size="12" wrap>
          <a-select
            v-model:value="selectedAppInstanceId"
            show-search
            allow-clear
            :filter-option="false"
            :loading="appInstanceLoading"
            :options="appInstanceOptions"
            :placeholder="t('console.appDbMigration.selectPlaceholder')"
            style="width: 360px"
            @search="handleAppInstanceSearch"
            @dropdownVisibleChange="handleAppInstanceDropdownChange"
          />
          <a-button type="primary" :loading="creating" :disabled="!isValidAppInstanceId" @click="handleCreate">{{ t("console.appDbMigration.createTask") }}</a-button>
          <a-button
            :loading="repairingBinding"
            :disabled="!isValidAppInstanceId"
            @click="handleRepairPrimaryBinding"
          >
            {{ t("console.appDbMigration.repairPrimaryBinding") }}
          </a-button>
          <a-button :loading="loading" @click="loadTasks">{{ t("common.refresh") }}</a-button>
        </a-space>
      </template>
    </a-page-header>

    <a-card class="playbook-card" size="small" :title="t('console.appDbMigration.playbookTitle')">
      <a-typography-paragraph>
        {{ t("console.appDbMigration.playbookFlow") }}
      </a-typography-paragraph>
      <a-typography-text type="secondary">
        {{ t("console.appDbMigration.playbookHint") }}
      </a-typography-text>
      <a-divider style="margin: 12px 0" />
      <a-typography-paragraph style="margin-bottom: 8px">{{ t("console.appDbMigration.checklistTitle") }}</a-typography-paragraph>
      <a-space direction="vertical" size="small">
        <a-typography-text>{{ t("console.appDbMigration.scenarioA") }}</a-typography-text>
        <a-typography-text>{{ t("console.appDbMigration.scenarioB") }}</a-typography-text>
        <a-typography-text>{{ t("console.appDbMigration.scenarioC") }}</a-typography-text>
        <a-typography-text>{{ t("console.appDbMigration.scenarioD") }}</a-typography-text>
      </a-space>
    </a-card>

    <a-table
      row-key="id"
      :columns="columns"
      :data-source="tasks"
      :loading="loading"
      :pagination="false"
      :scroll="{ x: 1680 }"
      class="task-table"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'progress'">
          <a-progress :percent="Number(record.progressPercent || 0)" size="small" />
        </template>
        <template v-else-if="column.key === 'schemaRepair'">
          <a-tooltip v-if="record.schemaRepairLog?.trim()" :title="record.schemaRepairLog">
            <span class="schema-repair-preview">{{ truncateText(record.schemaRepairLog, 56) }}</span>
          </a-tooltip>
          <span v-else class="schema-repair-empty">{{ t("console.appDbMigration.schemaRepairEmpty") }}</span>
        </template>
        <template v-else-if="column.key === 'action'">
          <a-space>
            <a-button size="small" @click="runPrecheck(record.id)">{{ t("console.appDbMigration.precheck") }}</a-button>
            <a-button size="small" type="primary" @click="runStart(record.id)">{{ t("console.appDbMigration.start") }}</a-button>
            <a-button size="small" @click="runValidate(record.id)">{{ t("console.appDbMigration.validate") }}</a-button>
            <a-button size="small" danger @click="runCutover(record.id)">{{ t("console.appDbMigration.cutover") }}</a-button>
            <a-button size="small" @click="runRollback(record.id)">{{ t("console.appDbMigration.rollback") }}</a-button>
            <a-button size="small" :disabled="record.status !== 'Failed'" @click="runReset(record.id)">
              {{ t("console.appDbMigration.reset") }}
            </a-button>
          </a-space>
        </template>
      </template>
    </a-table>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { message } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import { getTenantAppDataSourceBindings, getTenantAppInstancesPaged } from "@/services/api-tenant-app-instances";
import {
  createAppMigrationTask,
  cutoverAppMigrationTask,
  precheckAppMigrationTask,
  queryAppMigrationTasks,
  repairAppMigrationPrimaryBinding,
  resetAppMigrationTask,
  rollbackAppMigrationTask,
  startAppMigrationTask,
  validateAppMigrationTask,
  type AppMigrationTaskListItem
} from "@/services/api-app-migration";
import type { TenantAppInstanceListItem } from "@/types/platform-v2";

const { t } = useI18n();
const loading = ref(false);
const creating = ref(false);
const repairingBinding = ref(false);
const appInstanceLoading = ref(false);
const selectedAppInstanceId = ref<string>();
const appInstanceOptions = ref<Array<{ label: string; value: string }>>([]);
const tasks = ref<AppMigrationTaskListItem[]>([]);
const isValidAppInstanceId = computed(() => /^\d+$/.test((selectedAppInstanceId.value ?? "").trim()));

const columns = computed(() => [
  { title: t("console.appDbMigration.colTaskId"), dataIndex: "id", key: "id", width: 180 },
  { title: t("console.appDbMigration.colAppInstance"), dataIndex: "appInstanceId", key: "appInstanceId", width: 120 },
  { title: t("console.appDbMigration.colStatus"), dataIndex: "status", key: "status", width: 140 },
  { title: t("console.appDbMigration.colPhase"), dataIndex: "phase", key: "phase", width: 140 },
  { title: t("console.appDbMigration.colProgress"), dataIndex: "progressPercent", key: "progress", width: 200 },
  { title: t("console.appDbMigration.colSchemaRepair"), key: "schemaRepair", width: 260 },
  { title: t("console.appDbMigration.colErrorSummary"), dataIndex: "errorSummary", key: "errorSummary", width: 220 },
  { title: t("console.appDbMigration.colActions"), key: "action", width: 420, fixed: "right" }
]);

function truncateText(value: string, maxLen: number): string {
  const s = value.trim();
  if (s.length <= maxLen) {
    return s;
  }
  return `${s.slice(0, maxLen)}…`;
}

async function loadTasks() {
  loading.value = true;
  try {
    const result = await queryAppMigrationTasks(1, 50);
    tasks.value = result.items;
  } catch (error) {
    message.error((error as Error).message || t("console.appDbMigration.loadTasksFailed"));
  } finally {
    loading.value = false;
  }
}

async function loadAppInstanceOptions(keyword?: string) {
  appInstanceLoading.value = true;
  try {
    const result = await getTenantAppInstancesPaged({
      pageIndex: 1,
      pageSize: 20,
      keyword: keyword?.trim() || undefined
    });
    const appIds = result.items.map((item) => item.id);
    const bindings = appIds.length > 0 ? await getTenantAppDataSourceBindings(appIds) : [];
    const validPrimaryBindings = new Set(
      bindings
        .filter((binding) =>
          binding.tenantAppInstanceId
          && binding.bindingType === "Primary"
          && binding.bindingActive !== false
          && !!binding.dataSourceId)
        .map((binding) => binding.tenantAppInstanceId)
    );

    const filtered = result.items.filter((item) => validPrimaryBindings.has(item.id));
    appInstanceOptions.value = filtered.map((item: TenantAppInstanceListItem) => ({
      value: item.id,
      label: t("console.appDbMigration.optionLabel", { name: item.name, appKey: item.appKey, id: item.id })
    }));
    if (appInstanceOptions.value.length === 0) {
      message.info(t("console.appDbMigration.noBoundPrimary"));
    }
  } catch (error) {
    message.error((error as Error).message || t("console.appDbMigration.loadAppInstancesFailed"));
  } finally {
    appInstanceLoading.value = false;
  }
}

function handleAppInstanceSearch(keyword: string) {
  void loadAppInstanceOptions(keyword);
}

function handleAppInstanceDropdownChange(open: boolean) {
  if (open && appInstanceOptions.value.length === 0) {
    void loadAppInstanceOptions();
  }
}

async function handleCreate() {
  if (!isValidAppInstanceId.value) {
    message.warning(t("console.appDbMigration.selectInstanceRequired"));
    return;
  }

  creating.value = true;
  try {
    await createAppMigrationTask((selectedAppInstanceId.value ?? "").trim());
    message.success(t("console.appDbMigration.createSuccess"));
    await loadTasks();
  } catch (error) {
    message.error((error as Error).message || t("console.appDbMigration.createFailed"));
  } finally {
    creating.value = false;
  }
}

async function handleRepairPrimaryBinding() {
  if (!isValidAppInstanceId.value) {
    message.warning(t("console.appDbMigration.selectInstanceRequired"));
    return;
  }

  const appInstanceId = (selectedAppInstanceId.value ?? "").trim();
  repairingBinding.value = true;
  try {
    const result = await repairAppMigrationPrimaryBinding(appInstanceId);
    message.success(result.message || t("console.appDbMigration.repairPrimaryBindingSuccess"));
    await loadAppInstanceOptions();
  } catch (error) {
    message.error((error as Error).message || t("console.appDbMigration.repairPrimaryBindingFailed"));
  } finally {
    repairingBinding.value = false;
  }
}

async function runPrecheck(taskId: string) {
  try {
    const result = await precheckAppMigrationTask(taskId);
    message.success(result.canStart ? t("console.appDbMigration.precheckPassed") : t("console.appDbMigration.precheckNotPassed"));
    await loadTasks();
  } catch (error) {
    message.error((error as Error).message || t("console.appDbMigration.precheckFailed"));
  }
}

async function runStart(taskId: string) {
  try {
    await startAppMigrationTask(taskId);
    message.success(t("console.appDbMigration.startSuccess"));
    await loadTasks();
  } catch (error) {
    message.error((error as Error).message || t("console.appDbMigration.startFailed"));
  }
}

async function runValidate(taskId: string) {
  try {
    const result = await validateAppMigrationTask(taskId);
    message.success(result.passed ? t("console.appDbMigration.validatePassed") : t("console.appDbMigration.validateNotPassed"));
    await loadTasks();
  } catch (error) {
    message.error((error as Error).message || t("console.appDbMigration.validateFailed"));
  }
}

async function runCutover(taskId: string) {
  try {
    await cutoverAppMigrationTask(taskId, true, false);
    message.success(t("console.appDbMigration.cutoverSuccess"));
    await loadTasks();
  } catch (error) {
    message.error((error as Error).message || t("console.appDbMigration.cutoverFailed"));
  }
}

async function runRollback(taskId: string) {
  try {
    await rollbackAppMigrationTask(taskId);
    message.success(t("console.appDbMigration.rollbackSuccess"));
    await loadTasks();
  } catch (error) {
    message.error((error as Error).message || t("console.appDbMigration.rollbackFailed"));
  }
}

async function runReset(taskId: string) {
  try {
    const result = await resetAppMigrationTask(taskId);
    if (!result.success) {
      message.warning(result.message || t("console.appDbMigration.resetFailed"));
      return;
    }
    message.success(result.message || t("console.appDbMigration.resetSuccess"));
    await loadTasks();
  } catch (error) {
    message.error((error as Error).message || t("console.appDbMigration.resetFailed"));
  }
}

onMounted(() => {
  void loadAppInstanceOptions();
  void loadTasks();
});
</script>

<style scoped>
.app-db-migration-page {
  padding: 24px;
}

.task-table {
  margin-top: 16px;
}

.playbook-card {
  margin-top: 16px;
}

.toolbar-actions {
  justify-content: flex-end;
}

.schema-repair-preview {
  cursor: help;
  word-break: break-all;
}

.schema-repair-empty {
  color: rgba(0, 0, 0, 0.25);
}
</style>
