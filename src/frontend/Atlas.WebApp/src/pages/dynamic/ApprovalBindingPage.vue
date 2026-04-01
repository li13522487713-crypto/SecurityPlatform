<template>
  <div class="approval-binding-page">
    <!-- 顶部页头 -->
    <div class="page-header">
      <div class="header-left">
        <a-button type="text" @click="goBack">
          <template #icon><ArrowLeftOutlined /></template>
        </a-button>
        <span class="page-title">{{ tableDisplayName || tableKey }}</span>
        <span class="page-subtitle">— {{ t("approvalBinding.pageTitle") }}</span>
        <a-tag v-if="boundDetail" color="blue">
          {{ t("approvalBinding.boundCount", { count: boundDetail.boundActionCount }) }}
        </a-tag>
      </div>
      <div class="header-actions">
        <a-button @click="gotoFlowManage">
          <template #icon><ExportOutlined /></template>
          {{ t("approvalBinding.gotoFlowManage") }}
        </a-button>
        <a-button type="primary" :loading="saving" @click="handleSave">
          <template #icon><SaveOutlined /></template>
          {{ t("common.save") }}
        </a-button>
      </div>
    </div>

    <!-- 摘要卡片 -->
    <div class="summary-bar" v-if="boundDetail">
      <div class="summary-item">
        <span class="summary-label">{{ t("approvalBinding.boundCount", { count: boundDetail.boundActionCount }) }}</span>
      </div>
      <div class="summary-item" v-if="boundDetail.updatedAt">
        <span class="summary-meta">{{ t("approvalBinding.lastUpdated") }}：{{ formatTime(boundDetail.updatedAt) }}</span>
      </div>
    </div>

    <!-- 操作绑定表单 -->
    <div class="binding-body">
      <a-spin :spinning="loading">
        <a-card class="binding-card" :bordered="false">
          <div class="binding-description">
            {{ t("approvalBinding.description") }}
          </div>

          <div class="operation-list">
            <!-- 创建操作 -->
            <div class="operation-row">
              <div class="operation-label-col">
                <div class="operation-name">{{ t("approvalBinding.opCreate") }}</div>
                <div class="operation-desc">{{ t("approvalBinding.opCreateDesc") }}</div>
              </div>
              <div class="operation-select-col">
                <a-select
                  v-model:value="form.createFlowId"
                  :options="flowOptions"
                  :placeholder="t('approvalBinding.selectFlow')"
                  allow-clear
                  show-search
                  filter-option
                  style="width: 100%"
                />
              </div>
              <div class="operation-status-col">
                <a-badge
                  :color="form.createFlowId ? 'green' : 'default'"
                  :text="form.createFlowId ? t('approvalBinding.bound') : t('approvalBinding.unbound')"
                />
              </div>
            </div>

            <!-- 更新操作 -->
            <div class="operation-row">
              <div class="operation-label-col">
                <div class="operation-name">{{ t("approvalBinding.opUpdate") }}</div>
                <div class="operation-desc">{{ t("approvalBinding.opUpdateDesc") }}</div>
              </div>
              <div class="operation-select-col">
                <a-select
                  v-model:value="form.updateFlowId"
                  :options="flowOptions"
                  :placeholder="t('approvalBinding.selectFlow')"
                  allow-clear
                  show-search
                  filter-option
                  style="width: 100%"
                />
              </div>
              <div class="operation-status-col">
                <a-badge
                  :color="form.updateFlowId ? 'green' : 'default'"
                  :text="form.updateFlowId ? t('approvalBinding.bound') : t('approvalBinding.unbound')"
                />
              </div>
            </div>

            <!-- 删除操作 -->
            <div class="operation-row">
              <div class="operation-label-col">
                <div class="operation-name">{{ t("approvalBinding.opDelete") }}</div>
                <div class="operation-desc">{{ t("approvalBinding.opDeleteDesc") }}</div>
              </div>
              <div class="operation-select-col">
                <a-select
                  v-model:value="form.deleteFlowId"
                  :options="flowOptions"
                  :placeholder="t('approvalBinding.selectFlow')"
                  allow-clear
                  show-search
                  filter-option
                  style="width: 100%"
                />
              </div>
              <div class="operation-status-col">
                <a-badge
                  :color="form.deleteFlowId ? 'green' : 'default'"
                  :text="form.deleteFlowId ? t('approvalBinding.bound') : t('approvalBinding.unbound')"
                />
              </div>
            </div>

            <!-- 提交操作 -->
            <div class="operation-row">
              <div class="operation-label-col">
                <div class="operation-name">{{ t("approvalBinding.opSubmit") }}</div>
                <div class="operation-desc">{{ t("approvalBinding.opSubmitDesc") }}</div>
              </div>
              <div class="operation-select-col">
                <a-select
                  v-model:value="form.submitFlowId"
                  :options="flowOptions"
                  :placeholder="t('approvalBinding.selectFlow')"
                  allow-clear
                  show-search
                  filter-option
                  style="width: 100%"
                />
              </div>
              <div class="operation-status-col">
                <a-badge
                  :color="form.submitFlowId ? 'green' : 'default'"
                  :text="form.submitFlowId ? t('approvalBinding.bound') : t('approvalBinding.unbound')"
                />
              </div>
            </div>
          </div>
        </a-card>
      </a-spin>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from "vue";
import { useI18n } from "vue-i18n";
import { useRoute, useRouter } from "vue-router";
import { message } from "ant-design-vue";
import {
  ArrowLeftOutlined,
  ExportOutlined,
  SaveOutlined
} from "@ant-design/icons-vue";
import {
  getDynamicTableDetail,
  getMultiActionApprovalBinding,
  updateMultiActionApprovalBinding,
  type MultiActionApprovalBindingDetail
} from "@/services/dynamic-tables";
import { getApprovalFlowsPaged } from "@/services/api-approval";

const { t } = useI18n();
const route = useRoute();
const router = useRouter();

const appId = computed(() => (typeof route.params.appId === "string" ? route.params.appId : ""));
const tableKey = computed(() => (typeof route.params.tableKey === "string" ? route.params.tableKey : ""));

const tableDisplayName = ref("");
const loading = ref(false);
const saving = ref(false);
const boundDetail = ref<MultiActionApprovalBindingDetail | null>(null);

interface FlowOption {
  label: string;
  value: number;
}

const flowOptions = ref<FlowOption[]>([]);

const form = reactive<{
  createFlowId: number | null;
  updateFlowId: number | null;
  deleteFlowId: number | null;
  submitFlowId: number | null;
}>({
  createFlowId: null,
  updateFlowId: null,
  deleteFlowId: null,
  submitFlowId: null
});

const formatTime = (ts?: string | null): string => {
  if (!ts) return "";
  return new Date(ts).toLocaleString();
};

const loadFlowOptions = async () => {
  try {
    const result = await getApprovalFlowsPaged({ pageIndex: 1, pageSize: 100, keyword: "" });
    flowOptions.value = result.items.map((flow) => ({
      label: flow.name,
      value: Number(flow.id)
    }));
  } catch {
    // non-critical
  }
};

const loadData = async () => {
  if (!tableKey.value) return;
  loading.value = true;
  try {
    const [detail, binding] = await Promise.all([
      getDynamicTableDetail(tableKey.value),
      getMultiActionApprovalBinding(tableKey.value)
    ]);
    if (detail) {
      tableDisplayName.value = detail.displayName ?? tableKey.value;
    }
    boundDetail.value = binding;
    if (binding) {
      form.createFlowId = binding.createFlowId;
      form.updateFlowId = binding.updateFlowId;
      form.deleteFlowId = binding.deleteFlowId;
      form.submitFlowId = binding.submitFlowId;
    }
  } catch {
    message.error(t("dynamic.loadTableDetailFailed"));
  } finally {
    loading.value = false;
  }
};

const handleSave = async () => {
  if (!tableKey.value) return;
  saving.value = true;
  try {
    await updateMultiActionApprovalBinding(tableKey.value, {
      createFlowId: form.createFlowId,
      updateFlowId: form.updateFlowId,
      deleteFlowId: form.deleteFlowId,
      submitFlowId: form.submitFlowId
    });
    message.success(t("approvalBinding.saveSuccess"));
    // 刷新绑定详情
    const binding = await getMultiActionApprovalBinding(tableKey.value);
    boundDetail.value = binding;
  } catch (error) {
    message.error((error as Error).message || t("approvalBinding.saveFailed"));
  } finally {
    saving.value = false;
  }
};

const gotoFlowManage = () => {
  void router.push("/approval/flows");
};

const goBack = () => {
  void router.push(`/apps/${encodeURIComponent(appId.value)}/data`);
};

onMounted(() => {
  void Promise.all([loadData(), loadFlowOptions()]);
});
</script>

<style scoped>
.approval-binding-page {
  display: flex;
  flex-direction: column;
  min-height: calc(100vh - 120px);
  background: #fff;
  border-radius: 8px;
  box-shadow: 0 1px 2px 0 rgba(0,0,0,.03), 0 1px 6px -1px rgba(0,0,0,.02);
  overflow: hidden;
}

.page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 14px 24px;
  border-bottom: 1px solid #f0f0f0;
  background: #fff;
  flex-shrink: 0;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 10px;
}

.page-title {
  font-size: 16px;
  font-weight: 600;
  color: #1f1f1f;
}

.page-subtitle {
  font-size: 14px;
  color: #8c8c8c;
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

.summary-bar {
  display: flex;
  align-items: center;
  gap: 20px;
  padding: 10px 24px;
  background: #f6f8fa;
  border-bottom: 1px solid #f0f0f0;
  flex-shrink: 0;
}

.summary-label {
  font-size: 13px;
  font-weight: 600;
  color: #1677ff;
}

.summary-meta {
  font-size: 12px;
  color: #8c8c8c;
}

.binding-body {
  flex: 1;
  padding: 24px;
  overflow-y: auto;
}

.binding-card {
  max-width: 800px;
  margin: 0 auto;
}

.binding-description {
  font-size: 13px;
  color: #595959;
  margin-bottom: 24px;
  padding: 12px 16px;
  background: #f6f8fa;
  border-radius: 6px;
  line-height: 1.6;
}

.operation-list {
  display: flex;
  flex-direction: column;
  gap: 0;
}

.operation-row {
  display: flex;
  align-items: flex-start;
  gap: 16px;
  padding: 20px 0;
  border-bottom: 1px solid #f5f5f5;
}

.operation-row:last-child {
  border-bottom: none;
}

.operation-label-col {
  width: 160px;
  flex-shrink: 0;
}

.operation-name {
  font-size: 14px;
  font-weight: 600;
  color: #1f1f1f;
  margin-bottom: 4px;
}

.operation-desc {
  font-size: 12px;
  color: #8c8c8c;
  line-height: 1.4;
}

.operation-select-col {
  flex: 1;
  min-width: 0;
}

.operation-status-col {
  width: 80px;
  flex-shrink: 0;
  display: flex;
  align-items: center;
  padding-top: 4px;
}
</style>
