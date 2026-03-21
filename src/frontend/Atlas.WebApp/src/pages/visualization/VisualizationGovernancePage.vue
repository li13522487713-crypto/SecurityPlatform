<template>
  <a-card :title="t('visualization.governanceTitle')" class="page-card" :loading="loading">
    <a-row :gutter="[16, 16]">
      <a-col :span="24">
        <a-alert type="info" show-icon :message="t('visualization.governanceBanner')" />
      </a-col>
      <a-col :span="24">
        <a-card size="small" :title="t('visualization.cardAudit')">
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
import { computed, onMounted, ref, onUnmounted } from "vue";
import { useI18n } from "vue-i18n";

const isMounted = ref(false);
onMounted(() => {
  isMounted.value = true;
});
onUnmounted(() => {
  isMounted.value = false;
});

import { getVisualizationAudit } from "@/services/api";
import type { AuditListItem } from "@/types/api";
import { message } from "ant-design-vue";

const { t } = useI18n();

interface TablePagination {
  current?: number;
  pageSize?: number;
}

const audits = ref<AuditListItem[]>([]);
const loading = ref(false);
const pagination = ref({ current: 1, pageSize: 10, total: 0 });

const columns = computed(() => [
  { title: t("visualization.colTime"), dataIndex: "occurredAt", key: "occurredAt" },
  { title: t("visualization.colActor"), dataIndex: "actor", key: "actor" },
  { title: t("visualization.colAction"), dataIndex: "action", key: "action" },
  { title: t("visualization.colResult"), dataIndex: "result", key: "result" },
  { title: t("visualization.colTarget"), dataIndex: "target", key: "target" }
]);

const loadData = async () => {
  try {
    loading.value = true;
    const result = await getVisualizationAudit({
      pageIndex: pagination.value.current,
      pageSize: pagination.value.pageSize
    });
    if (!isMounted.value) return;
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
