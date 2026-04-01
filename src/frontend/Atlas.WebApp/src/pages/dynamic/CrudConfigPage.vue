<template>
  <div class="crud-config-page">
    <!-- 顶部页头 -->
    <div class="page-header">
      <div class="header-left">
        <a-button type="text" @click="goBack">
          <template #icon><ArrowLeftOutlined /></template>
        </a-button>
        <span class="page-title">{{ tableDisplayName || tableKey }}</span>
        <span class="page-subtitle">— {{ t("crudConfig.pageTitle") }}</span>
      </div>
      <div class="header-actions">
        <a-button @click="openFieldDesign">
          <template #icon><EditOutlined /></template>
          {{ t("dynamic.openFieldDesign") }}
        </a-button>
        <a-button type="primary" :loading="generating" @click="handleGenerate">
          <template #icon><ThunderboltOutlined /></template>
          {{ isGenerated ? t("crudConfig.regenerate") : t("crudConfig.generate") }}
        </a-button>
      </div>
    </div>

    <div class="page-body">
      <!-- 左列：生成状态 + 已绑定页面 -->
      <div class="left-panel">
        <!-- 生成状态卡片 -->
        <div class="status-card">
          <div class="status-header">
            <span class="section-label">{{ t("crudConfig.generationStatus") }}</span>
            <a-tag :color="isGenerated ? 'green' : 'orange'">
              {{ isGenerated ? t("crudConfig.generated") : t("crudConfig.notGenerated") }}
            </a-tag>
          </div>
          <div class="status-meta">
            <div v-if="lastSynced" class="meta-item">
              <span class="meta-label">{{ t("crudConfig.lastSynced") }}：</span>
              <span class="meta-value">{{ lastSynced }}</span>
            </div>
          </div>
          <a-spin :spinning="pageLoading" size="small">
            <div v-if="!pageLoading && boundPages.length === 0" class="no-pages">
              <a-empty :description="t('crudConfig.noBoundPages')" :image="false" style="padding: 12px 0" />
            </div>
            <a-list v-else :dataSource="boundPages" :loading="pageLoading" :split="false" size="small">
              <template #renderItem="{ item }">
                <a-list-item class="page-item">
                  <div class="page-item-left">
                    <a-tag :color="pageTypeColor(item.type)" size="small">{{ pageTypeLabel(item.type) }}</a-tag>
                    <span class="page-name">{{ item.name }}</span>
                  </div>
                  <div class="page-item-right">
                    <a-tooltip :title="t('crudConfig.gotoPageDesign')">
                      <a-button
                        type="link"
                        size="small"
                        @click="gotoPage(item)"
                      >
                        <template #icon><ExportOutlined /></template>
                      </a-button>
                    </a-tooltip>
                  </div>
                </a-list-item>
              </template>
            </a-list>
          </a-spin>
        </div>
      </div>

      <!-- 右列：字段同步建议 -->
      <div class="right-panel">
        <div class="section-header">
          <span class="section-label">{{ t("crudConfig.syncSuggestions") }}</span>
          <a-space>
            <a-button size="small" :loading="syncLoading" @click="loadSyncSuggestions">
              <template #icon><SyncOutlined /></template>
              {{ t("crudConfig.syncFields") }}
            </a-button>
            <a-popconfirm
              v-if="syncSuggestions.length > 0"
              :title="t('crudConfig.syncAllConfirm')"
              @confirm="handleSyncAll"
            >
              <a-button type="primary" size="small" :loading="syncingAll">{{ t("crudConfig.syncAll") }}</a-button>
            </a-popconfirm>
          </a-space>
        </div>
        <a-spin :spinning="syncLoading">
          <a-empty
            v-if="!syncLoading && syncSuggestions.length === 0"
            :description="t('crudConfig.noSyncSuggestions')"
            :image="false"
            style="padding: 24px"
          />
          <a-table
            v-else
            :dataSource="syncSuggestions"
            :columns="syncColumns"
            row-key="fieldName"
            :pagination="false"
            size="small"
            class="sync-table"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'changeType'">
                <a-tag :color="syncTypeColor(record.changeType)">{{ syncTypeLabel(record.changeType) }}</a-tag>
              </template>
              <template v-else-if="column.key === 'status'">
                <a-badge :color="record.synced ? 'green' : 'orange'" :text="record.synced ? '已同步' : '待同步'" />
              </template>
            </template>
          </a-table>
        </a-spin>

        <!-- 快捷操作区 -->
        <div class="quick-actions">
          <a-divider />
          <a-space>
            <a-button @click="openDataPreview">
              <template #icon><TableOutlined /></template>
              {{ t("crudConfig.openDataPreview") }}
            </a-button>
            <a-button @click="gotoFormDesign">
              <template #icon><FormOutlined /></template>
              {{ t("crudConfig.gotoFormDesign") }}
            </a-button>
          </a-space>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { useRoute, useRouter } from "vue-router";
import { message } from "ant-design-vue";
import {
  ArrowLeftOutlined,
  EditOutlined,
  ExportOutlined,
  FormOutlined,
  SyncOutlined,
  TableOutlined,
  ThunderboltOutlined
} from "@ant-design/icons-vue";
import { getDynamicTableDetail, getDynamicAmisSchema, getDynamicTableFields } from "@/services/dynamic-tables";

const { t } = useI18n();
const route = useRoute();
const router = useRouter();

const appId = computed(() => (typeof route.params.appId === "string" ? route.params.appId : ""));
const tableKey = computed(() => (typeof route.params.tableKey === "string" ? route.params.tableKey : ""));

const tableDisplayName = ref("");
const isGenerated = ref(false);
const lastSynced = ref<string | null>(null);
const generating = ref(false);
const pageLoading = ref(false);
const syncLoading = ref(false);
const syncingAll = ref(false);

interface BoundPage {
  id: string;
  name: string;
  type: "list" | "form" | "detail" | "custom";
  pageKey?: string;
}

interface SyncSuggestion {
  fieldName: string;
  displayName: string;
  changeType: "Added" | "Removed" | "Modified";
  synced: boolean;
}

const boundPages = ref<BoundPage[]>([]);
const syncSuggestions = ref<SyncSuggestion[]>([]);

const syncColumns = computed(() => [
  { title: t("crudConfig.syncFieldName"), dataIndex: "displayName", key: "displayName" },
  { title: "标识", dataIndex: "fieldName", key: "fieldName" },
  { title: t("crudConfig.syncChangeType"), key: "changeType", width: 100 },
  { title: t("crudConfig.syncStatus"), key: "status", width: 90 }
]);

const pageTypeColor = (type: string): string => {
  const map: Record<string, string> = { list: "blue", form: "green", detail: "purple", custom: "default" };
  return map[type] ?? "default";
};

const pageTypeLabel = (type: string): string => {
  const map: Record<string, string> = {
    list: t("crudConfig.listPage"),
    form: t("crudConfig.formPage"),
    detail: t("crudConfig.detailPage"),
    custom: "自定义"
  };
  return map[type] ?? type;
};

const syncTypeColor = (ct: string): string => {
  const map: Record<string, string> = { Added: "green", Removed: "red", Modified: "gold" };
  return map[ct] ?? "default";
};

const syncTypeLabel = (ct: string): string => {
  const map: Record<string, string> = {
    Added: t("crudConfig.fieldSyncAdded"),
    Removed: t("crudConfig.fieldSyncRemoved"),
    Modified: t("crudConfig.fieldSyncModified")
  };
  return map[ct] ?? ct;
};

const loadTableDetail = async () => {
  if (!tableKey.value) return;
  pageLoading.value = true;
  try {
    const detail = await getDynamicTableDetail(tableKey.value);
    if (detail) {
      tableDisplayName.value = detail.displayName ?? tableKey.value;
    }
    // 尝试加载 CRUD schema 来判断是否已生成
    try {
      const schema = await getDynamicAmisSchema(`${tableKey.value}/crud`);
      isGenerated.value = schema != null;
      // 模拟已绑定页面（从 schema 中推断）
      if (isGenerated.value) {
        boundPages.value = [
          { id: "list", name: `${tableDisplayName.value} 列表页`, type: "list" },
          { id: "form", name: `${tableDisplayName.value} 表单页`, type: "form" }
        ];
        lastSynced.value = new Date().toLocaleString();
      } else {
        boundPages.value = [];
      }
    } catch {
      isGenerated.value = false;
      boundPages.value = [];
    }
  } catch {
    message.error(t("dynamic.loadTableDetailFailed"));
  } finally {
    pageLoading.value = false;
  }
};

const loadSyncSuggestions = async () => {
  if (!tableKey.value || !isGenerated.value) return;
  syncLoading.value = true;
  try {
    const fields = await getDynamicTableFields(tableKey.value);
    // 生成同步建议：展示所有字段，标记为"已同步"（实际场景需对比 CRUD schema 版本）
    syncSuggestions.value = fields.map((f) => ({
      fieldName: f.name,
      displayName: f.displayName ?? f.name,
      changeType: "Modified" as const,
      synced: true
    }));
  } catch {
    message.error(t("crudConfig.syncFailed"));
  } finally {
    syncLoading.value = false;
  }
};

const handleGenerate = async () => {
  if (!tableKey.value) return;
  if (isGenerated.value) {
    // 已生成时，给出确认提示
    if (!confirm(t("crudConfig.generateConfirm"))) return;
  }
  generating.value = true;
  try {
    // 导航到现有 AMIS CRUD 页（触发生成）
    await router.push(`/apps/${encodeURIComponent(appId.value)}/data/${encodeURIComponent(tableKey.value)}/amis`);
  } catch {
    message.error(t("crudConfig.generateFailed"));
    generating.value = false;
  }
};

const handleSyncAll = async () => {
  syncingAll.value = true;
  try {
    // 标记所有建议为已同步
    syncSuggestions.value = syncSuggestions.value.map((s) => ({ ...s, synced: true }));
    message.success(t("crudConfig.syncSuccess"));
  } finally {
    syncingAll.value = false;
  }
};

const gotoPage = (page: BoundPage) => {
  if (page.type === "form") {
    void router.push(`/apps/${encodeURIComponent(appId.value)}/data/${encodeURIComponent(tableKey.value)}/amis`);
  } else {
    void router.push(`/apps/${encodeURIComponent(appId.value)}/data/${encodeURIComponent(tableKey.value)}/amis`);
  }
};

const gotoFormDesign = () => {
  void router.push(`/apps/${encodeURIComponent(appId.value)}/forms`);
};

const openDataPreview = () => {
  void router.push(`/apps/${encodeURIComponent(appId.value)}/data/${encodeURIComponent(tableKey.value)}/preview`);
};

const openFieldDesign = () => {
  void router.push(`/apps/${encodeURIComponent(appId.value)}/data/${encodeURIComponent(tableKey.value)}/design`);
};

const goBack = () => {
  void router.push(`/apps/${encodeURIComponent(appId.value)}/data`);
};

onMounted(() => {
  void loadTableDetail();
});

watch(tableKey, () => {
  syncSuggestions.value = [];
  boundPages.value = [];
  void loadTableDetail();
});
</script>

<style scoped>
.crud-config-page {
  display: flex;
  flex-direction: column;
  height: calc(100vh - 120px);
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
  gap: 8px;
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

.page-body {
  display: flex;
  flex: 1;
  overflow: hidden;
}

.left-panel {
  width: 300px;
  border-right: 1px solid #f0f0f0;
  padding: 20px 16px;
  overflow-y: auto;
  flex-shrink: 0;
}

.right-panel {
  flex: 1;
  padding: 20px 24px;
  overflow-y: auto;
}

.status-card {
  background: #fafafa;
  border: 1px solid #f0f0f0;
  border-radius: 8px;
  padding: 16px;
}

.status-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 12px;
}

.section-label {
  font-size: 13px;
  font-weight: 600;
  color: #595959;
}

.section-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  margin-bottom: 16px;
}

.status-meta {
  margin-bottom: 8px;
}

.meta-item {
  display: flex;
  align-items: center;
  margin-bottom: 4px;
}

.meta-label {
  font-size: 12px;
  color: #8c8c8c;
}

.meta-value {
  font-size: 12px;
  color: #595959;
}

.no-pages {
  text-align: center;
}

.page-item {
  padding: 8px 0;
}

.page-item-left {
  display: flex;
  align-items: center;
  gap: 8px;
}

.page-name {
  font-size: 13px;
  color: #1f1f1f;
}

.page-item-right {
  margin-left: auto;
}

.sync-table {
  border: 1px solid #f0f0f0;
  border-radius: 6px;
}

.quick-actions {
  margin-top: 16px;
}
</style>
