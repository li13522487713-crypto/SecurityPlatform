<template>
  <div class="migration-page">
    <a-page-header :title="t('migration.title')" :sub-title="t('migration.subtitle')" />

    <a-card class="migration-card">
      <a-tabs v-model:activeKey="activeTab">
        <!-- Dynamic Migrations -->
        <a-tab-pane key="dynamic" :tab="t('migration.dynamicTab')">
          <a-form layout="inline" style="margin-bottom: 16px">
            <a-form-item :label="t('migration.tableKey')">
              <a-input v-model:value="detectTableKey" :placeholder="t('migration.tableKeyPlaceholder')" style="width: 240px" />
            </a-form-item>
            <a-form-item>
              <a-button type="primary" :loading="detecting" :disabled="!detectTableKey" @click="handleDetect">
                {{ t("migration.detect") }}
              </a-button>
            </a-form-item>
          </a-form>

          <a-alert v-if="detectResult" :type="detectResult.success ? 'success' : 'error'" :message="detectResult.message" style="margin-bottom: 16px" />

          <div v-if="currentMigration" class="migration-detail">
            <a-descriptions bordered :column="2" size="small">
              <a-descriptions-item :label="t('migration.id')">{{ currentMigration.id }}</a-descriptions-item>
              <a-descriptions-item :label="t('migration.status')">
                <a-tag :color="statusColor(currentMigration.status)">{{ currentMigration.status }}</a-tag>
              </a-descriptions-item>
              <a-descriptions-item :label="t('migration.tableKey')" :span="2">{{ currentMigration.tableKey }}</a-descriptions-item>
            </a-descriptions>

            <div class="migration-actions">
              <a-space>
                <a-button :loading="precheckLoading" @click="handlePrecheck">
                  {{ t("migration.precheck") }}
                </a-button>
                <a-button type="primary" :loading="executeLoading" :disabled="!precheckPassed" @click="handleExecute">
                  {{ t("migration.execute") }}
                </a-button>
                <a-button danger :loading="retryLoading" @click="handleRetry">
                  {{ t("migration.retry") }}
                </a-button>
              </a-space>
            </div>

            <a-alert v-if="precheckResult" :type="precheckResult.safe ? 'success' : 'warning'" :message="precheckResult.message" style="margin-top: 12px" />
            <a-alert v-if="executeResult" :type="executeResult.success ? 'success' : 'error'" :message="executeResult.message" style="margin-top: 12px" />
          </div>
        </a-tab-pane>

        <!-- App Migrations -->
        <a-tab-pane key="app" :tab="t('migration.appTab')">
          <a-form layout="inline" style="margin-bottom: 16px">
            <a-form-item :label="t('migration.migrationId')">
              <a-input-number v-model:value="appMigrationId" :min="1" style="width: 200px" />
            </a-form-item>
            <a-form-item>
              <a-button type="primary" :disabled="!appMigrationId" :loading="appLoading" @click="handleLoadAppMigration">
                {{ t("migration.load") }}
              </a-button>
            </a-form-item>
          </a-form>

          <div v-if="appMigration" class="migration-detail">
            <a-descriptions bordered :column="2" size="small">
              <a-descriptions-item :label="t('migration.id')">{{ appMigration.id }}</a-descriptions-item>
              <a-descriptions-item :label="t('migration.status')">
                <a-tag :color="statusColor(appMigration.status)">{{ appMigration.status }}</a-tag>
              </a-descriptions-item>
            </a-descriptions>

            <div class="migration-actions">
              <a-space>
                <a-button :loading="appPrecheckLoading" @click="handleAppPrecheck">
                  {{ t("migration.precheck") }}
                </a-button>
                <a-button type="primary" :loading="appStartLoading" @click="handleAppStart">
                  {{ t("migration.start") }}
                </a-button>
                <a-button @click="handleAppProgress">
                  {{ t("migration.progress") }}
                </a-button>
                <a-button danger @click="handleAppRollback">
                  {{ t("migration.rollback") }}
                </a-button>
              </a-space>
            </div>

            <a-alert v-if="appResult" :type="appResult.success ? 'success' : 'error'" :message="appResult.message" style="margin-top: 12px" />
          </div>
        </a-tab-pane>
      </a-tabs>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref } from "vue";
import { useI18n } from "vue-i18n";
import { requestApi } from "@/services/api-core";
import type { ApiResponse } from "@atlas/shared-core";

const { t } = useI18n();

const activeTab = ref("dynamic");

// Dynamic Migrations
const detectTableKey = ref("");
const detecting = ref(false);
const detectResult = ref<{ success: boolean; message: string } | null>(null);
const currentMigration = ref<{ id: number; tableKey: string; status: string } | null>(null);
const precheckLoading = ref(false);
const precheckPassed = ref(false);
const precheckResult = ref<{ safe: boolean; message: string } | null>(null);
const executeLoading = ref(false);
const executeResult = ref<{ success: boolean; message: string } | null>(null);
const retryLoading = ref(false);

// App Migrations
const appMigrationId = ref<number | null>(null);
const appLoading = ref(false);
const appMigration = ref<{ id: number; status: string } | null>(null);
const appPrecheckLoading = ref(false);
const appStartLoading = ref(false);
const appResult = ref<{ success: boolean; message: string } | null>(null);

function statusColor(status: string): string {
  const map: Record<string, string> = {
    Pending: "blue",
    Running: "processing",
    Completed: "success",
    Failed: "error",
    Rollback: "warning"
  };
  return map[status] ?? "default";
}

async function handleDetect() {
  detecting.value = true;
  detectResult.value = null;
  currentMigration.value = null;
  try {
    const resp = await requestApi<ApiResponse<{ id: number; tableKey: string; status: string }>>(
      `/api/v1/dynamic-migrations/detect/${detectTableKey.value}`,
      { method: "POST" }
    );
    if (resp.success && resp.data) {
      currentMigration.value = resp.data;
      detectResult.value = { success: true, message: t("migration.detectSuccess") };
    } else {
      detectResult.value = { success: false, message: resp.message || t("migration.detectFailed") };
    }
  } catch (e: unknown) {
    detectResult.value = { success: false, message: e instanceof Error ? e.message : String(e) };
  } finally {
    detecting.value = false;
  }
}

async function handlePrecheck() {
  if (!currentMigration.value) return;
  precheckLoading.value = true;
  precheckResult.value = null;
  try {
    const resp = await requestApi<ApiResponse<{ safe: boolean; message?: string }>>(
      `/api/v1/dynamic-migrations/${currentMigration.value.id}/precheck`,
      { method: "POST" }
    );
    if (resp.success && resp.data) {
      precheckPassed.value = resp.data.safe;
      precheckResult.value = { safe: resp.data.safe, message: resp.data.message || (resp.data.safe ? "OK" : "Not safe") };
    }
  } catch (e: unknown) {
    precheckResult.value = { safe: false, message: e instanceof Error ? e.message : String(e) };
  } finally {
    precheckLoading.value = false;
  }
}

async function handleExecute() {
  if (!currentMigration.value) return;
  executeLoading.value = true;
  executeResult.value = null;
  try {
    const resp = await requestApi<ApiResponse<{ success: boolean; message?: string }>>(
      `/api/v1/dynamic-migrations/${currentMigration.value.id}/execute`,
      { method: "POST" }
    );
    executeResult.value = {
      success: resp.success,
      message: resp.success ? t("migration.executeSuccess") : (resp.message || t("migration.executeFailed"))
    };
  } catch (e: unknown) {
    executeResult.value = { success: false, message: e instanceof Error ? e.message : String(e) };
  } finally {
    executeLoading.value = false;
  }
}

async function handleRetry() {
  if (!currentMigration.value) return;
  retryLoading.value = true;
  try {
    await requestApi(`/api/v1/dynamic-migrations/${currentMigration.value.id}/retry`, { method: "POST" });
  } finally {
    retryLoading.value = false;
  }
}

async function handleLoadAppMigration() {
  if (!appMigrationId.value) return;
  appLoading.value = true;
  appMigration.value = null;
  try {
    const resp = await requestApi<ApiResponse<{ id: number; status: string }>>(
      `/api/v1/app-migrations/${appMigrationId.value}`
    );
    if (resp.success && resp.data) {
      appMigration.value = resp.data;
    }
  } finally {
    appLoading.value = false;
  }
}

async function handleAppPrecheck() {
  if (!appMigration.value) return;
  appPrecheckLoading.value = true;
  appResult.value = null;
  try {
    const resp = await requestApi<ApiResponse<{ safe: boolean; message?: string }>>(
      `/api/v1/app-migrations/${appMigration.value.id}/precheck`,
      { method: "POST" }
    );
    appResult.value = {
      success: resp.success && !!resp.data?.safe,
      message: resp.data?.message || (resp.success ? "OK" : resp.message || "Precheck failed")
    };
  } finally {
    appPrecheckLoading.value = false;
  }
}

async function handleAppStart() {
  if (!appMigration.value) return;
  appStartLoading.value = true;
  appResult.value = null;
  try {
    const resp = await requestApi<ApiResponse<unknown>>(`/api/v1/app-migrations/${appMigration.value.id}/start`, { method: "POST" });
    appResult.value = {
      success: resp.success,
      message: resp.success ? t("migration.startSuccess") : (resp.message || "Start failed")
    };
  } finally {
    appStartLoading.value = false;
  }
}

async function handleAppProgress() {
  if (!appMigration.value) return;
  const resp = await requestApi<ApiResponse<{ progress: number; status: string }>>(
    `/api/v1/app-migrations/${appMigration.value.id}/progress`
  );
  if (resp.success && resp.data) {
    appResult.value = { success: true, message: `Progress: ${JSON.stringify(resp.data)}` };
  }
}

async function handleAppRollback() {
  if (!appMigration.value) return;
  const resp = await requestApi<ApiResponse<unknown>>(`/api/v1/app-migrations/${appMigration.value.id}/rollback`, { method: "POST" });
  appResult.value = {
    success: resp.success,
    message: resp.success ? t("migration.rollbackSuccess") : (resp.message || "Rollback failed")
  };
}
</script>

<style scoped>
.migration-page {
  padding: 16px;
}

.migration-card {
  margin-top: 16px;
}

.migration-detail {
  margin-top: 16px;
}

.migration-actions {
  margin-top: 16px;
}
</style>
