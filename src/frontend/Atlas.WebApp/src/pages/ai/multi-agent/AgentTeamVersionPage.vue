<template>
  <a-card title="版本与发布" :bordered="false">
    <a-space direction="vertical" style="width: 100%">
      <a-button :loading="loading" @click="loadVersions">刷新</a-button>
      <a-table :columns="columns" :data-source="versions" row-key="id">
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'action'">
            <a-button size="small" @click="rollback(record.id)">回滚到该版本</a-button>
          </template>
        </template>
      </a-table>
      <a-card size="small" title="版本差异（简化）">
        <a-empty description="MVP 阶段先提供版本列表与回滚，差异详情后续增强" />
      </a-card>
    </a-space>
  </a-card>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useRoute } from "vue-router";
import { message } from "ant-design-vue";
import { getTeamVersions, rollbackTeamVersion, type TeamVersionItem } from "@/services/api-agent-team";

const route = useRoute();
const teamId = computed(() => Number(route.params.id || 0));
const loading = ref(false);
const versions = ref<TeamVersionItem[]>([]);

const columns = [
  { title: "VersionId", dataIndex: "id", key: "id", width: 120 },
  { title: "版本号", dataIndex: "versionNo", key: "versionNo", width: 120 },
  { title: "发布状态", dataIndex: "publishStatus", key: "publishStatus", width: 140 },
  { title: "发布人", dataIndex: "publishedBy", key: "publishedBy", width: 120 },
  { title: "发布时间", dataIndex: "publishedAt", key: "publishedAt", width: 200 },
  { title: "回滚来源", dataIndex: "rollbackFromVersionId", key: "rollbackFromVersionId", width: 120 },
  { title: "操作", key: "action", width: 140 }
];

async function loadVersions() {
  if (!teamId.value) return;
  loading.value = true;
  try {
    versions.value = await getTeamVersions(teamId.value);
  } catch (err) {
    message.error((err as Error).message || "加载版本失败");
  } finally {
    loading.value = false;
  }
}

async function rollback(versionId: number) {
  if (!teamId.value) return;
  try {
    await rollbackTeamVersion(teamId.value, versionId);
    message.success("回滚成功");
    await loadVersions();
  } catch (err) {
    message.error((err as Error).message || "回滚失败");
  }
}

onMounted(() => {
  void loadVersions();
});
</script>
