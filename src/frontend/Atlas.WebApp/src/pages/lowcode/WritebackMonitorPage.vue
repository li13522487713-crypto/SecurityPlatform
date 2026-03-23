<template>
  <div class="writeback-monitor-page">
    <a-page-header :title="t('lowcode.writeback.title')" :sub-title="t('lowcode.writeback.subtitle')">
      <template #extra>
        <a-space>
          <a-select
            v-model:value="selectedAppId"
            style="width: 260px"
            :loading="appLoading"
            :options="appOptions"
            allow-clear
            show-search
            :placeholder="t('lowcode.writeback.phApp')"
            @change="handleAppScopeChange"
          />
          <a-button :loading="loading" @click="loadData">{{ t("lowcode.writeback.refresh") }}</a-button>
        </a-space>
      </template>
    </a-page-header>

    <div class="monitor-content">
      <a-card size="small" style="margin-bottom: 16px">
        <a-space>
          <a-select v-model:value="retryStrategy" style="width: 180px">
            <a-select-option value="Immediate">{{ t("lowcode.writeback.retryImmediate") }}</a-select-option>
            <a-select-option value="Backoff">{{ t("lowcode.writeback.retryBackoff") }}</a-select-option>
            <a-select-option value="ManualOnly">{{ t("lowcode.writeback.retryManual") }}</a-select-option>
          </a-select>
          <a-switch v-model:checked="alertEnabled" />
          <span>{{ t("lowcode.writeback.failAlert") }}</span>
        </a-space>
      </a-card>

      <a-row :gutter="16" style="margin-bottom: 16px">
        <a-col :span="8">
          <a-card>
            <a-statistic
              :title="t('lowcode.writeback.statUnresolved')"
              :value="failureList.length"
              :value-style="{ color: failureList.length > 0 ? '#ff4d4f' : '#52c41a' }"
            />
          </a-card>
        </a-col>
      </a-row>

      <a-table
        :data-source="failureList"
        :columns="columns"
        :loading="loading"
        row-key="id"
        :pagination="{ pageSize: 20 }"
        size="middle"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'targetStatus'">
            <a-tag color="orange">{{ record.targetStatus }}</a-tag>
          </template>
          <template v-if="column.key === 'retryCount'">
            <a-badge :count="record.retryCount" :overflow-count="99" color="red" />
          </template>
          <template v-if="column.key === 'firstFailedAt'">
            {{ formatDate(record.firstFailedAt) }}
          </template>
          <template v-if="column.key === 'lastAttemptAt'">
            {{ formatDate(record.lastAttemptAt) }}
          </template>
          <template v-if="column.key === 'errorMessage'">
            <a-tooltip :title="record.errorMessage">
              <span class="error-msg-ellipsis">{{ record.errorMessage }}</span>
            </a-tooltip>
          </template>
          <template v-if="column.key === 'actions'">
            <a-button
              type="link"
              size="small"
              :loading="retryingIds.has(record.id)"
              @click="handleRetry(record)"
            >{{ t("lowcode.writeback.manualRetry") }}</a-button>
          </template>
        </template>
      </a-table>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, onUnmounted } from "vue";
import { useI18n } from "vue-i18n";

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { message } from "ant-design-vue";
import { requestApi } from "@/services/api-core";
import type { ApiResponse } from "@/types/api";
import { getLowCodeAppsPaged } from "@/services/lowcode";
import { getCurrentAppIdFromStorage, setCurrentAppIdToStorage } from "@/utils/app-context";

const { t, locale } = useI18n();

interface WritebackFailureDto {
  id: number
  businessKey: string
  targetStatus: string
  retryCount: number
  errorMessage: string
  firstFailedAt: string
  lastAttemptAt: string
  isResolved: boolean
}

const loading = ref(false);
const appLoading = ref(false);
const failureList = ref<WritebackFailureDto[]>([]);
const retryingIds = ref(new Set<number>());
const selectedAppId = ref<string | undefined>(getCurrentAppIdFromStorage() ?? undefined);
const appOptions = ref<Array<{ label: string; value: string }>>([]);
const retryStrategy = ref<"Immediate" | "Backoff" | "ManualOnly">("Backoff");
const alertEnabled = ref(true);

const columns = computed(() => [
  { title: "BusinessKey", dataIndex: "businessKey", key: "businessKey", width: 200 },
  { title: t("lowcode.writeback.colTargetStatus"), dataIndex: "targetStatus", key: "targetStatus", width: 100 },
  { title: t("lowcode.writeback.colRetries"), dataIndex: "retryCount", key: "retryCount", width: 80, align: "center" as const },
  { title: t("lowcode.writeback.colError"), dataIndex: "errorMessage", key: "errorMessage", ellipsis: true },
  { title: t("lowcode.writeback.colFirstFailed"), dataIndex: "firstFailedAt", key: "firstFailedAt", width: 150 },
  { title: t("lowcode.writeback.colLastAttempt"), dataIndex: "lastAttemptAt", key: "lastAttemptAt", width: 150 },
  { title: t("lowcode.writeback.colActions"), key: "actions", width: 90 },
]);

const loadData = async () => {
  loading.value = true;
  try {
    const params = new URLSearchParams({ limit: "100" });
    if (selectedAppId.value) {
      params.set("appId", selectedAppId.value);
    }
    const res = await requestApi<ApiResponse<WritebackFailureDto[]>>(`/approval/writeback-failures?${params.toString()}`);
    failureList.value = res.data ?? [];
  } catch (e) {
    message.error((e as Error)?.message || t("lowcode.writeback.loadFailed"));
  } finally {
    loading.value = false;
  }
};

const loadAppOptions = async () => {
  appLoading.value = true;
  try {
    const result = await getLowCodeAppsPaged({ pageIndex: 1, pageSize: 200 });
    appOptions.value = result.items.map((item) => ({
      label: `${item.name} (${item.appKey})`,
      value: item.id
    }));
  } catch (e) {
    message.error((e as Error)?.message || t("lowcode.writeback.loadAppsFailed"));
  } finally {
    appLoading.value = false;
  }
};

const handleAppScopeChange = (value: string | undefined) => {
  setCurrentAppIdToStorage(value);
  void loadData();
};

const handleRetry = async (record: WritebackFailureDto) => {
  retryingIds.value.add(record.id);
  try {
    const query = new URLSearchParams({
      strategy: retryStrategy.value,
      alertEnabled: alertEnabled.value ? "true" : "false"
    }).toString();
    await requestApi(`/approval/writeback-failures/${record.id}/retry?${query}`, { method: "POST" });
    message.success(t("lowcode.writeback.retryOk"));
    await loadData();
  } catch (e) {
    message.error((e as Error)?.message || t("lowcode.writeback.retryFailed"));
  } finally {
    retryingIds.value.delete(record.id);
  }
};

const formatDate = (iso: string) =>
  iso ? new Date(iso).toLocaleString(locale.value === "en-US" ? "en-US" : "zh-CN") : "-";

onMounted(async () => {
  await loadAppOptions();
  await loadData();
});
</script>

<style scoped>
.writeback-monitor-page {
  padding: 16px;
}

.monitor-content {
  padding: 0 16px 16px;
}

.error-msg-ellipsis {
  max-width: 250px;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  display: inline-block;
  vertical-align: bottom;
}
</style>
