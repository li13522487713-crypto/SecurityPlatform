<template>
  <a-card title="在线用户" :bordered="false">
    <div class="crud-toolbar">
      <a-space wrap>
        <a-input-search
          v-model:value="keyword"
          placeholder="搜索用户名或IP"
          allow-clear
          style="width: 260px"
          @search="handleSearch"
        />
        <a-button @click="handleSearch">刷新</a-button>
      </a-space>
    </div>

    <a-table
      :columns="columns"
      :data-source="dataList"
      :loading="loading"
      :pagination="pagination"
      row-key="sessionId"
      :locale="{ emptyText: '暂无在线用户' }"
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
            title="确认强制下线该用户？此操作不可撤销。"
            ok-text="强制下线"
            ok-type="danger"
            cancel-text="取消"
            @confirm="handleForceLogout(record.sessionId)"
          >
            <a-button type="link" danger size="small">强制下线</a-button>
          </a-popconfirm>
        </template>
      </template>
    </a-table>
  </a-card>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from "vue";
import { message } from "ant-design-vue";
import type { TablePaginationConfig } from "ant-design-vue";
import { getOnlineUsers, forceLogout, type OnlineUserDto } from "@/services/sessions";

const keyword = ref("");
const dataList = ref<OnlineUserDto[]>([]);
const loading = ref(false);
const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: false
});

const columns = [
  { title: "用户名", dataIndex: "username", key: "username" },
  { title: "IP地址", dataIndex: "ipAddress", key: "ipAddress" },
  { title: "客户端类型", dataIndex: "clientType", key: "clientType" },
  { title: "登录时间", key: "loginTime", width: 180 },
  { title: "最后活跃", key: "lastSeenAt", width: 180 },
  { title: "过期时间", key: "expiresAt", width: 180 },
  { title: "操作", key: "actions", width: 100, fixed: "right" as const }
];

function formatTime(val: string) {
  if (!val) return "-";
  return new Date(val).toLocaleString("zh-CN", { hour12: false });
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
  } catch (e: any) {
    message.error(e.message || "加载失败");
  } finally {
    loading.value = false;
  }
}

function handleSearch() {
  pagination.current = 1;
  loadData();
}

function onTableChange(pag: TablePaginationConfig) {
  pagination.current = pag.current ?? 1;
  loadData();
}

async function handleForceLogout(sessionId: string) {
  try {
    await forceLogout(sessionId);
    message.success("已强制下线");
    loadData();
  } catch (e: any) {
    message.error(e.message || "操作失败");
  }
}

onMounted(() => {
  loadData();
});
</script>

<style scoped>
.crud-toolbar {
  margin-bottom: 16px;
}
</style>
