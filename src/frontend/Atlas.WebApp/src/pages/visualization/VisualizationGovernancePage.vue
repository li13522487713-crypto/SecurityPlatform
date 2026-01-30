<template>
  <a-card title="治理中心" class="page-card" :loading="loading">
    <a-row :gutter="[16, 16]">
      <a-col :span="24">
        <a-alert
          type="info"
          show-icon
          message="治理中心当前提供审计留痕与运行指标概览，口径管理与数据源清单将在后续版本完善。"
        />
      </a-col>
      <a-col :span="24">
        <a-card size="small" title="审计留痕">
          <a-table
            :data-source="audits"
            :columns="columns"
            :pagination="pagination"
            row-key="id"
            size="middle"
            @change="handleTableChange"
          />
        </a-card>
      </a-col>
    </a-row>
  </a-card>
</template>

<script setup lang="ts">
import { onMounted, ref } from "vue";
import { getVisualizationAudit } from "@/services/api";
import type { AuditListItem } from "@/types/api";
import { message } from "ant-design-vue";

interface TablePagination {
  current?: number;
  pageSize?: number;
}

const audits = ref<AuditListItem[]>([]);
const loading = ref(false);
const pagination = ref({ current: 1, pageSize: 10, total: 0 });

const columns = [
  { title: "时间", dataIndex: "occurredAt", key: "occurredAt" },
  { title: "操作人", dataIndex: "actor", key: "actor" },
  { title: "动作", dataIndex: "action", key: "action" },
  { title: "结果", dataIndex: "result", key: "result" },
  { title: "目标", dataIndex: "target", key: "target" }
];

const loadData = async () => {
  try {
    loading.value = true;
    const result = await getVisualizationAudit({
      pageIndex: pagination.value.current,
      pageSize: pagination.value.pageSize
    });
    audits.value = result.items;
    pagination.value.total = result.total;
  } catch (err) {
    message.error((err as Error).message);
  } finally {
    loading.value = false;
  }
};

const handleTableChange = (pager: TablePagination) => {
  pagination.value = {
    ...pagination.value,
    current: pager.current ?? pagination.value.current,
    pageSize: pager.pageSize ?? pagination.value.pageSize
  };
  loadData();
};

onMounted(loadData);
</script>
