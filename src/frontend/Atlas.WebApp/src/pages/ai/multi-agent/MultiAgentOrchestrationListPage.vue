<template>
  <a-card title="Agent 团队列表" :bordered="false">
    <div class="toolbar">
      <a-space wrap>
        <a-input-search
          v-model:value="keyword"
          placeholder="搜索团队名称 / 描述"
          style="width: 280px"
          @search="loadData"
        />
        <a-select v-model:value="statusFilter" style="width: 160px" allow-clear placeholder="状态">
          <a-select-option value="Draft">Draft</a-select-option>
          <a-select-option value="Ready">Ready</a-select-option>
          <a-select-option value="Published">Published</a-select-option>
          <a-select-option value="Disabled">Disabled</a-select-option>
          <a-select-option value="Archived">Archived</a-select-option>
        </a-select>
        <a-select v-model:value="riskLevelFilter" style="width: 140px" allow-clear placeholder="风险等级">
          <a-select-option value="Low">低风险</a-select-option>
          <a-select-option value="Medium">中风险</a-select-option>
          <a-select-option value="High">高风险</a-select-option>
        </a-select>
        <a-button type="primary" @click="openCreate">新建团队</a-button>
      </a-space>
    </div>

    <a-table :columns="columns" :data-source="rows" row-key="id" :loading="loading" :pagination="false">
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'status'">
          <a-tag :color="statusColor(record.status)">{{ record.status }}</a-tag>
        </template>
        <template v-else-if="column.key === 'riskLevel'">
          <a-tag :color="riskColor(record.riskLevel)">{{ record.riskLevel }}</a-tag>
        </template>
        <template v-else-if="column.key === 'action'">
          <a-space>
            <a-button type="link" @click="goDetail(record.id)">编辑</a-button>
            <a-button type="link" @click="goDebug(record.id)">调试</a-button>
            <a-button type="link" @click="goRun(record.id)">运行</a-button>
            <a-button type="link" @click="goVersion(record.id)">版本</a-button>
            <a-popconfirm title="确认删除该团队？" @confirm="handleDelete(record.id)">
              <a-button type="link" danger>删除</a-button>
            </a-popconfirm>
          </a-space>
        </template>
      </template>
    </a-table>

    <div class="pager">
      <a-pagination
        v-model:current="pageIndex"
        v-model:page-size="pageSize"
        :total="total"
        show-size-changer
        :page-size-options="['10', '20', '50']"
        @change="loadData"
      />
    </div>

    <a-modal v-model:open="createVisible" title="新建 Agent 团队" :confirm-loading="createLoading" @ok="handleCreate">
      <a-form :model="createForm" layout="vertical">
        <a-form-item label="团队名称" required>
          <a-input v-model:value="createForm.teamName" />
        </a-form-item>
        <a-form-item label="负责人" required>
          <a-input v-model:value="createForm.owner" />
        </a-form-item>
        <a-form-item label="描述">
          <a-textarea v-model:value="createForm.description" :rows="3" />
        </a-form-item>
      </a-form>
    </a-modal>
  </a-card>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from "vue";
import { message } from "ant-design-vue";
import { useRoute, useRouter } from "vue-router";
import type { AgentTeamListItem, TeamRiskLevel, TeamStatus } from "@/services/api-agent-team";
import { createAgentTeam, deleteAgentTeam, getAgentTeamPaged } from "@/services/api-agent-team";
import { resolveCurrentAppId } from "@/utils/app-context";

const route = useRoute();
const router = useRouter();
const rows = ref<AgentTeamListItem[]>([]);
const loading = ref(false);
const keyword = ref("");
const statusFilter = ref<TeamStatus>();
const riskLevelFilter = ref<TeamRiskLevel>();
const pageIndex = ref(1);
const pageSize = ref(20);
const total = ref(0);

const createVisible = ref(false);
const createLoading = ref(false);
const createForm = reactive({
  teamName: "",
  owner: "admin",
  description: ""
});

const columns = computed(() => [
  { title: "团队名称", dataIndex: "teamName", key: "teamName", width: 220 },
  { title: "负责人", dataIndex: "owner", key: "owner", width: 120 },
  { title: "状态", dataIndex: "status", key: "status", width: 120 },
  { title: "发布状态", dataIndex: "publishStatus", key: "publishStatus", width: 140 },
  { title: "风险等级", dataIndex: "riskLevel", key: "riskLevel", width: 120 },
  { title: "版本", dataIndex: "version", key: "version", width: 80 },
  { title: "更新时间", dataIndex: "updatedAt", key: "updatedAt", width: 210 },
  { title: "操作", key: "action", width: 380 }
]);

function buildBasePath() {
  const appId = resolveCurrentAppId(route);
  return appId ? `/apps/${appId}/multi-agent` : "/ai/multi-agent";
}

function goDetail(id: number) {
  void router.push(`${buildBasePath()}/${id}`);
}

function goDebug(id: number) {
  void router.push(`${buildBasePath()}/${id}/debug`);
}

function goRun(id: number) {
  void router.push(`${buildBasePath()}/${id}/run`);
}

function goVersion(id: number) {
  void router.push(`${buildBasePath()}/${id}/versions`);
}

function statusColor(status: TeamStatus) {
  if (status === "Published") return "green";
  if (status === "Ready") return "blue";
  if (status === "Disabled") return "orange";
  if (status === "Archived") return "default";
  return "purple";
}

function riskColor(level: TeamRiskLevel) {
  if (level === "High") return "red";
  if (level === "Medium") return "gold";
  return "green";
}

async function loadData() {
  loading.value = true;
  try {
    const result = await getAgentTeamPaged({
      pageIndex: pageIndex.value,
      pageSize: pageSize.value,
      keyword: keyword.value || undefined,
      status: statusFilter.value,
      riskLevel: riskLevelFilter.value
    });
    rows.value = result.items;
    total.value = Number(result.total);
  } catch (err) {
    message.error((err as Error).message || "查询团队失败");
  } finally {
    loading.value = false;
  }
}

function openCreate() {
  createForm.teamName = "";
  createForm.owner = "admin";
  createForm.description = "";
  createVisible.value = true;
}

async function handleCreate() {
  if (!createForm.teamName.trim()) {
    message.warning("请填写团队名称");
    return;
  }

  if (!createForm.owner.trim()) {
    message.warning("请填写负责人");
    return;
  }

  createLoading.value = true;
  try {
    const id = await createAgentTeam({
      teamName: createForm.teamName.trim(),
      description: createForm.description.trim() || undefined,
      owner: createForm.owner.trim(),
      collaborators: [],
      riskLevel: "Low",
      tags: [],
      defaultModelPolicyJson: "{}",
      budgetPolicyJson: "{}",
      permissionScopeJson: "{}"
    });
    createVisible.value = false;
    message.success("创建成功");
    await loadData();
    if (id > 0) {
      goDetail(id);
    }
  } catch (err) {
    message.error((err as Error).message || "创建失败");
  } finally {
    createLoading.value = false;
  }
}

async function handleDelete(id: number) {
  try {
    await deleteAgentTeam(id);
    message.success("删除成功");
    await loadData();
  } catch (err) {
    message.error((err as Error).message || "删除失败");
  }
}

onMounted(() => {
  void loadData();
});
</script>

<style scoped>
.toolbar {
  margin-bottom: 16px;
}

.pager {
  margin-top: 16px;
  display: flex;
  justify-content: flex-end;
}
</style>
