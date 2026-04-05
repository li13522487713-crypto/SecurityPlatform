<template>
  <div style="padding: 24px;">
    <a-card :title="t('systemOnlineUsers.cardTitle')">
      <div style="margin-bottom: 16px;">
        <a-space wrap>
          <a-input-search
            v-model:value="keyword"
            :placeholder="t('systemOnlineUsers.searchPlaceholder')"
            allow-clear
            style="width: 260px"
            @search="handleSearch"
          />
          <a-button @click="handleSearch">{{ t("common.refresh") }}</a-button>
        </a-space>
      </div>

      <a-table
        :columns="columns"
        :data-source="dataList"
        :loading="loading"
        :pagination="pagination"
        row-key="sessionId"
        @change="onTableChange"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'loginTime'">
            {{ formatTime(record.loginTime) }}
          </template>
          <template v-else-if="column.key === 'lastSeenAt'">
            {{ formatTime(record.lastSeenAt) }}
          </template>
          <template v-else-if="column.key === 'expiresAt'">
            {{ formatTime(record.expiresAt) }}
          </template>
          <template v-else-if="column.key === 'actions'">
            <a-popconfirm
              :title="t('systemOnlineUsers.forceLogoutConfirm')"
              :ok-text="t('systemOnlineUsers.forceLogoutOk')"
              ok-type="danger"
              :cancel-text="t('common.cancel')"
              @confirm="handleForceLogout(record.sessionId)"
            >
              <a-button type="link" danger size="small">{{ t("systemOnlineUsers.forceLogoutBtn") }}</a-button>
            </a-popconfirm>
          </template>
        </template>
      </a-table>
    </a-card>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, computed, onMounted } from "vue";
import { message } from "ant-design-vue";
import type { TablePaginationConfig } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import { getOnlineUsers, forceLogout } from "@/services/api-sessions";
import type { OnlineUserDto } from "@/services/api-sessions";

const { t, locale } = useI18n();

const keyword = ref("");
const dataList = ref<OnlineUserDto[]>([]);
const loading = ref(false);
const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: false
});

const columns = computed(() => [
  { title: t("systemOnlineUsers.colUsername"), dataIndex: "username", key: "username" },
  { title: t("systemOnlineUsers.colIp"), dataIndex: "ipAddress", key: "ipAddress" },
  { title: t("systemOnlineUsers.colClientType"), dataIndex: "clientType", key: "clientType" },
  { title: t("systemOnlineUsers.colLoginTime"), key: "loginTime", width: 180 },
  { title: t("systemOnlineUsers.colLastSeen"), key: "lastSeenAt", width: 180 },
  { title: t("systemOnlineUsers.colExpiresAt"), key: "expiresAt", width: 180 },
  { title: t("systemOnlineUsers.colActions"), key: "actions", width: 100, fixed: "right" as const }
]);

function formatTime(val: string) {
  if (!val) return "-";
  const loc = locale.value === "en-US" ? "en-US" : "zh-CN";
  return new Date(val).toLocaleString(loc, { hour12: false });
}

async function loadData() {
  loading.value = true;
  try {
    const result = await getOnlineUsers({
      pageIndex: pagination.current ?? 1,
      pageSize: pagination.pageSize ?? 20,
      keyword: keyword.value || undefined
    });
    dataList.value = result.items as OnlineUserDto[];
    pagination.total = Number(result.total);
  } catch {
    message.error(t("systemOnlineUsers.loadFailed"));
  } finally {
    loading.value = false;
  }
}

function handleSearch() {
  pagination.current = 1;
  void loadData();
}

function onTableChange(pag: TablePaginationConfig) {
  pagination.current = pag.current ?? 1;
  void loadData();
}

async function handleForceLogout(sessionId: string) {
  try {
    await forceLogout(sessionId);
    message.success(t("systemOnlineUsers.forceLogoutSuccess"));
    void loadData();
  } catch (e: unknown) {
    message.error(e instanceof Error ? e.message : t("systemOnlineUsers.operationFailed"));
  }
}

onMounted(() => void loadData());
</script>
