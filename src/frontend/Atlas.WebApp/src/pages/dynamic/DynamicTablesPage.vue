<template>
  <div class="data-workbench">
    <!-- 顶部工具栏 -->
    <div class="workbench-header">
      <div class="header-title">{{ t('dynamic.workbenchTitle') }}</div>
      <div class="header-actions">
        <a-select
          v-model:value="selectedAppId"
          class="app-selector"
          :options="appOptions"
          :loading="appLoading"
          show-search
          :filter-option="filterAppOption"
          :placeholder="t('dynamic.selectAppScope')"
          @change="handleAppScopeChange"
        />
        <a-divider type="vertical" />
        <a-button @click="refreshAll">
          <template #icon><ReloadOutlined /></template>
          {{ t("dynamic.refresh") }}
        </a-button>
        <a-button type="primary" :disabled="!selectedAppId" @click="openDataDesigner()">
          <template #icon><PartitionOutlined /></template>
          {{ t("dynamicDesigner.centerTitle") }}
        </a-button>
        <a-button type="primary" :disabled="!selectedAppId" @click="openCreateTableModal()">
          <template #icon><PlusOutlined /></template>
          {{ t("dynamic.createTable") }}
        </a-button>
        <a-button :disabled="!selectedAppId" @click="openERDCanvas()">{{ t("dynamicDesigner.modeRelation") }}</a-button>
        <a-button :disabled="!selectedAppId" @click="openDataDesigner('view')">{{ t("dynamicDesigner.modeView") }}</a-button>
        <a-button :disabled="!selectedAppId" @click="openDataDesigner('transform')">{{ t("dynamicDesigner.modeTransform") }}</a-button>
      </div>
    </div>

    <div class="workbench-body">
      <!-- 左侧：表与视图目录树/列表 -->
      <div class="workbench-sidebar">
        <div class="sidebar-search">
          <a-input
            v-model:value="sidebarKeyword"
            :placeholder="t('dynamic.searchSidebarPlaceholder')"
            allow-clear
          >
            <template #prefix><SearchOutlined style="color: rgba(0,0,0,.25)" /></template>
          </a-input>
        </div>
        
        <div class="sidebar-content">
          <a-spin :spinning="tableLoading">
            <a-empty v-if="!selectedAppId" :description="t('dynamic.selectAppFirst')" class="sidebar-empty" />
            <div v-else-if="filteredTables.length === 0 && filteredViews.length === 0" class="sidebar-empty">
              <a-empty :description="t('dynamic.noDataFound')" :image="false" />
            </div>
            <div v-else class="sidebar-menu">
              <!-- 视图分组 -->
              <div class="menu-group">
                <div class="group-title">
                  <PartitionOutlined class="group-icon" />
                  <span>{{ t('dynamic.viewListTitle') }}</span>
                  <span class="group-count">{{ filteredViews.length }}</span>
                </div>
                <div class="group-list">
                  <div
                    v-for="view in filteredViews"
                    :key="view.id"
                    class="menu-item"
                    @click="openERDCanvas(view.id)"
                  >
                    <div class="item-main">
                      <span class="item-name" :title="view.name">{{ view.name }}</span>
                      <span class="item-desc">{{ t("dynamic.viewTablesCount", { count: view.nodeCount }) }}</span>
                    </div>
                  </div>
                </div>
              </div>

              <!-- 数据表分组 -->
              <div class="menu-group">
                <div class="group-title">
                  <TableOutlined class="group-icon" />
                  <span>{{ t('dynamic.tableDirectory') }}</span>
                  <span class="group-count">{{ filteredTables.length }}</span>
                </div>
                <div class="group-list">
                  <div
                    v-for="table in filteredTables"
                    :key="table.tableKey"
                    class="menu-item"
                    :class="{ 'is-active': table.tableKey === selectedTableKey }"
                    @click="selectTable(table.tableKey)"
                  >
                    <div class="item-main">
                      <span class="item-name" :title="table.displayName || table.tableKey">{{ table.displayName || table.tableKey }}</span>
                      <span class="item-desc" :title="table.tableKey">{{ table.tableKey }}</span>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </a-spin>
        </div>
      </div>

      <!-- 右侧：详情面板 -->
      <div class="workbench-main">
        <div v-if="!selectedAppId" class="main-empty">
          <a-empty :description="t('dynamic.selectAppFirst')" />
        </div>
        <div v-else-if="tableState === 'empty'" class="main-empty">
          <a-empty :description="t('dynamic.emptyNoTables')">
            <a-button type="primary" style="margin-top: 16px" @click="openCreateTableModal()">
              {{ t("dynamic.createFirstTable") }}
            </a-button>
          </a-empty>
        </div>
        <div v-else-if="!selectedTable" class="main-empty">
          <a-empty :description="t('dynamic.selectTableFirst')" :image="false" />
        </div>
        <div v-else class="table-detail-panel">
          <div class="panel-header">
            <div class="panel-title">
              <span class="title-text">{{ selectedTable.displayName || selectedTable.tableKey }}</span>
              <a-tag color="blue">{{ selectedTable.tableKey }}</a-tag>
            </div>
            <div class="panel-actions">
              <a-button @click="openTableDesign(selectedTable.tableKey)">
                <template #icon><EditOutlined /></template>
                {{ t("dynamic.openFieldDesign") }}
              </a-button>
              <a-button @click="openTableCrud(selectedTable.tableKey)">
                <template #icon><DatabaseOutlined /></template>
                {{ t("dynamic.openCrud") }}
              </a-button>
              <a-button @click="openTableNative(selectedTable.tableKey)">
                <template #icon><ProfileOutlined /></template>
                {{ t("dynamic.openNativeView") }}
              </a-button>
              <a-popconfirm
                :title="t('dynamic.deleteTableConfirm', '确认删除该数据表？')"
                :ok-text="t('common.confirm', '确定')"
                :cancel-text="t('common.cancel', '取消')"
                @confirm="handleDeleteTable(selectedTable.tableKey)"
              >
                <a-button danger>
                  {{ t("common.delete", "删除") }}
                </a-button>
              </a-popconfirm>
              <a-dropdown>
                <template #overlay>
                  <a-menu>
                    <a-menu-item @click="openTableCrud(selectedTable.tableKey)">
                      {{ t("dynamic.bindApproval") }}
                    </a-menu-item>
                    <a-menu-item @click="openCreateTableModal(selectedTable.tableKey)">
                      {{ t("dynamic.createRelatedTable") }}
                    </a-menu-item>
                  </a-menu>
                </template>
                <a-button>
                  <MoreOutlined />
                </a-button>
              </a-dropdown>
            </div>
          </div>

          <div class="panel-content">
            <a-spin :spinning="detailLoading">
              <div class="stats-grid">
                <div class="stat-card">
                  <div class="stat-label">{{ t('dynamic.fieldsCount') }}</div>
                  <div class="stat-value">{{ selectedFieldCount }}</div>
                </div>
                <div class="stat-card">
                  <div class="stat-label">{{ t('dynamic.indexesCount') }}</div>
                  <div class="stat-value">{{ selectedIndexCount }}</div>
                </div>
                <div class="stat-card">
                  <div class="stat-label">{{ t('dynamic.dbType') }}</div>
                  <div class="stat-value">{{ selectedDbType }}</div>
                </div>
                <div class="stat-card">
                  <div class="stat-label">{{ t('dynamic.approvalBinding') }}</div>
                  <div class="stat-value">
                    <a-badge :status="selectedApprovalBound ? 'success' : 'default'" />
                    {{ selectedApprovalBound ? t('dynamic.approvalBound') : t('dynamic.approvalUnbound') }}
                  </div>
                </div>
              </div>

              <div class="detail-section">
                <div class="section-title">{{ t('dynamic.fieldListOverview') }}</div>
                <a-table
                  v-if="selectedTableDetail"
                  :dataSource="selectedTableDetail.fields"
                  :columns="fieldColumns"
                  size="small"
                  :pagination="{ pageSize: 10 }"
                  rowKey="name"
                  class="field-table"
                >
                  <template #bodyCell="{ column, record }">
                    <template v-if="column.key === 'name'">
                      <span class="field-name">
                        <KeyOutlined v-if="record.isPrimaryKey" style="color: #faad14; margin-right: 4px" title="主键" />
                        {{ record.name }}
                      </span>
                    </template>
                    <template v-else-if="column.key === 'allowNull'">
                      <a-tag :color="record.allowNull ? 'default' : 'red'">
                        {{ record.allowNull ? 'Yes' : 'No' }}
                      </a-tag>
                    </template>
                  </template>
                </a-table>
              </div>
            </a-spin>
          </div>
        </div>
      </div>
    </div>
  </div>

  <a-modal
    v-model:open="createModalOpen"
    :title="t('dynamic.createTableModalTitle')"
    :confirm-loading="creating"
    @ok="handleCreateTable"
  >
    <a-form ref="createFormRef" layout="vertical" :model="createForm" :rules="createFormRules">
      <a-form-item :label="t('dynamic.selectedTableKey')" name="tableKey">
        <a-input v-model:value="createForm.tableKey" :placeholder="t('dynamic.tableKeyPlaceholder')" />
      </a-form-item>
      <a-form-item :label="t('dynamic.selectedTableName')" name="displayName">
        <a-input v-model:value="createForm.displayName" :placeholder="t('dynamic.tableNamePlaceholder')" />
      </a-form-item>
      <a-form-item :label="t('common.description')" name="description">
        <a-textarea v-model:value="createForm.description" :rows="3" />
      </a-form-item>
    </a-form>
  </a-modal>

  <a-modal
    v-model:open="blockerModalOpen"
    :title="t('dynamic.deleteBlockedTitle', '删除被阻断')"
    :footer="null"
    width="680px"
  >
    <div style="margin-bottom: 12px;">
      {{ t("dynamic.deleteBlockedHint", "检测到以下引用，请先解除后再删除：") }}
    </div>
    <a-table
      :dataSource="blockerRows"
      :columns="blockerColumns"
      :pagination="false"
      size="small"
      rowKey="id"
    />
    <div v-if="deleteWarnings.length > 0" style="margin-top: 12px;">
      <a-alert
        type="warning"
        show-icon
        :message="deleteWarnings.join('；')"
      />
    </div>
  </a-modal>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, reactive, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { useRoute, useRouter } from "vue-router";
import { message } from "ant-design-vue";
import {
  ReloadOutlined,
  PlusOutlined,
  SearchOutlined,
  TableOutlined,
  PartitionOutlined,
  EditOutlined,
  DatabaseOutlined,
  ProfileOutlined,
  MoreOutlined,
  KeyOutlined
} from "@ant-design/icons-vue";
import type { Rule } from "ant-design-vue/es/form";
import {
  createDynamicTable,
  deleteDynamicTable,
  getAppScopedDynamicTables,
  getDynamicTableDeleteCheck,
  getDynamicTableDetail,
  type AppScopedDynamicTableListItem
} from "@/services/dynamic-tables";
import { getLowCodeAppsPaged } from "@/services/lowcode";
import { getCurrentAppIdFromStorage, setCurrentAppIdToStorage } from "@/utils/app-context";
import type { DynamicTableDetail } from "@/types/dynamic-tables";
import type { DeleteCheckBlocker } from "@/services/dynamic-tables";

const { t } = useI18n();
const route = useRoute();
const router = useRouter();

const isMounted = ref(false);
onMounted(() => {
  isMounted.value = true;
});
onUnmounted(() => {
  isMounted.value = false;
});

const appLoading = ref(false);
const tableLoading = ref(false);
const detailLoading = ref(false);
const sidebarKeyword = ref("");
const selectedAppId = ref<string | undefined>(
  typeof route.params.appId === "string" && route.params.appId.trim()
    ? route.params.appId
    : getCurrentAppIdFromStorage() ?? undefined
);
const selectedTableKey = ref("");
const appOptions = ref<Array<{ label: string; value: string }>>([]);
const tableDirectory = ref<AppScopedDynamicTableListItem[]>([]);
const selectedTableDetail = ref<DynamicTableDetail | null>(null);
const relationViewItems = ref<Array<{ id: string; name: string; nodeCount: number }>>([]);
const blockerModalOpen = ref(false);
const blockerRows = ref<DeleteCheckBlocker[]>([]);
const deleteWarnings = ref<string[]>([]);

const filteredTables = computed(() => {
  const kw = sidebarKeyword.value.trim().toLowerCase();
  if (!kw) return tableDirectory.value;
  return tableDirectory.value.filter(
    (t) =>
      t.tableKey.toLowerCase().includes(kw) ||
      (t.displayName ?? "").toLowerCase().includes(kw)
  );
});

const filteredViews = computed(() => {
  const kw = sidebarKeyword.value.trim().toLowerCase();
  if (!kw) return relationViewItems.value;
  return relationViewItems.value.filter((v) => v.name.toLowerCase().includes(kw));
});

const fieldColumns = computed(() => [
  { title: t("designer.entityModeling.fieldName"), dataIndex: "name", key: "name" },
  { title: t("designer.entityModeling.displayName"), dataIndex: "displayName", key: "displayName" },
  { title: t("designer.entityModeling.fieldType"), dataIndex: "fieldType", key: "fieldType" },
  { title: t("designer.entityModeling.allowNull"), dataIndex: "allowNull", key: "allowNull", width: 100 }
]);

const blockerColumns = computed(() => [
  { title: t("dynamic.blockerType", "类型"), dataIndex: "type", key: "type", width: 120 },
  { title: t("dynamic.blockerName", "名称"), dataIndex: "name", key: "name" },
  { title: t("dynamic.blockerPath", "路径"), dataIndex: "path", key: "path" }
]);

const createModalOpen = ref(false);
const creating = ref(false);
const createFormRef = ref();
const createForm = reactive({
  tableKey: "",
  displayName: "",
  description: ""
});

const tableState = computed<"empty" | "single" | "multi">(() => {
  if (tableDirectory.value.length === 0) {
    return "empty";
  }
  return tableDirectory.value.length === 1 ? "single" : "multi";
});

const selectedTable = computed(() => {
  if (tableDirectory.value.length === 0) {
    return null;
  }
  return tableDirectory.value.find((item) => item.tableKey === selectedTableKey.value) ?? tableDirectory.value[0];
});

const selectedFieldCount = computed(() => selectedTableDetail.value?.fields.length ?? 0);
const selectedIndexCount = computed(() => selectedTableDetail.value?.indexes.length ?? 0);
const selectedDbType = computed(() => selectedTableDetail.value?.dbType ?? "Sqlite");
const selectedApprovalBound = computed(() => !!selectedTableDetail.value?.approvalFlowDefinitionId);

const createFormRules: Record<string, Rule[]> = {
  tableKey: [
    { required: true, message: t("validation.required") },
    { pattern: /^[A-Za-z][A-Za-z0-9_]{1,63}$/, message: t("dynamic.tableKeyRule") }
  ],
  displayName: [{ required: true, message: t("validation.required") }]
};

const filterAppOption = (input: string, option: { label?: string; value?: string }) => {
  const label = (option.label ?? "").toString().toLowerCase();
  return label.includes(input.toLowerCase());
};

const loadAppOptions = async () => {
  appLoading.value = true;
  try {
    const result = await getLowCodeAppsPaged({ pageIndex: 1, pageSize: 200 });
    if (!isMounted.value) {
      return;
    }
    appOptions.value = result.items.map((item) => ({
      label: `${item.name} (${item.appKey})`,
      value: item.id
    }));
  } catch (error) {
    if (!isMounted.value) {
      return;
    }
    message.error((error as Error).message || t("dynamic.loadAppsFailed"));
  } finally {
    if (isMounted.value) {
      appLoading.value = false;
    }
  }
};

const loadTableDirectory = async () => {
  if (!selectedAppId.value) {
    tableDirectory.value = [];
    selectedTableKey.value = "";
    selectedTableDetail.value = null;
    return;
  }
  tableLoading.value = true;
  try {
    const result = await getAppScopedDynamicTables(selectedAppId.value);
    if (!isMounted.value) {
      return;
    }
    tableDirectory.value = result;
    if (result.length === 0) {
      selectedTableKey.value = "";
      selectedTableDetail.value = null;
      return;
    }
    if (!result.some((item) => item.tableKey === selectedTableKey.value)) {
      selectedTableKey.value = result[0].tableKey;
    }
  } catch (error) {
    if (!isMounted.value) {
      return;
    }
    message.error((error as Error).message || t("dynamic.loadTablesFailed"));
  } finally {
    if (isMounted.value) {
      tableLoading.value = false;
    }
  }
};

const loadSelectedTableDetail = async () => {
  const tableKey = selectedTableKey.value;
  if (!tableKey) {
    selectedTableDetail.value = null;
    return;
  }
  detailLoading.value = true;
  try {
    const detail = await getDynamicTableDetail(tableKey);
    if (!isMounted.value || selectedTableKey.value !== tableKey) {
      return;
    }
    selectedTableDetail.value = detail;
  } catch (error) {
    if (!isMounted.value) {
      return;
    }
    selectedTableDetail.value = null;
    message.error((error as Error).message || t("dynamic.loadTableDetailFailed"));
  } finally {
    if (isMounted.value) {
      detailLoading.value = false;
    }
  }
};

const loadRelationViewItems = () => {
  relationViewItems.value = [];
  const appId = selectedAppId.value;
  if (!appId) {
    return;
  }
  try {
    const key = `atlas_relation_views_${appId}`;
    const raw = localStorage.getItem(key);
    const customViews = raw ? (JSON.parse(raw) as Array<{ id: string; name: string; layout?: { nodes?: unknown[] } }>) : [];
    const items = customViews
      .filter((item) => item && typeof item.id === "string" && typeof item.name === "string")
      .map((item) => ({
        id: item.id,
        name: item.name,
        nodeCount: Array.isArray(item.layout?.nodes) ? item.layout!.nodes!.length : 0
      }));
    relationViewItems.value = [{ id: "__overview__", name: t("dynamic.overviewViewName"), nodeCount: tableDirectory.value.length }, ...items];
  } catch {
    relationViewItems.value = [{ id: "__overview__", name: t("dynamic.overviewViewName"), nodeCount: tableDirectory.value.length }];
  }
};

const refreshAll = async () => {
  await loadTableDirectory();
  await loadSelectedTableDetail();
  loadRelationViewItems();
};

const selectTable = (tableKey: string) => {
  selectedTableKey.value = tableKey;
};

const syncSelectedAppFromRoute = () => {
  const routeAppId = typeof route.params.appId === "string" ? route.params.appId.trim() : "";
  const nextAppId = routeAppId || getCurrentAppIdFromStorage() || undefined;
  if (nextAppId !== selectedAppId.value) {
    selectedAppId.value = nextAppId;
  }
};

const handleAppScopeChange = (value: string | undefined) => {
  setCurrentAppIdToStorage(value);
  if (value && value !== route.params.appId) {
    void router.push(`/apps/${value}/data`);
    return;
  }
  void refreshAll();
};

const openTableCrud = (tableKey: string) => {
  if (!selectedAppId.value || !tableKey) {
    return;
  }
  void router.push(`/apps/${selectedAppId.value}/data/${encodeURIComponent(tableKey)}?tab=data`);
};

const openTableNative = (tableKey: string) => {
  if (!selectedAppId.value || !tableKey) {
    return;
  }
  void router.push(`/apps/${selectedAppId.value}/data/${encodeURIComponent(tableKey)}/native`);
};

const openTableDesign = (tableKey: string) => {
  if (!selectedAppId.value || !tableKey) {
    return;
  }
  void router.push(`/apps/${selectedAppId.value}/data/${encodeURIComponent(tableKey)}/design`);
};


const openDataDesigner = (mode?: "view" | "transform") => {
  if (!selectedAppId.value) {
    return;
  }

  const query = mode ? `?mode=${mode}` : "";
  void router.push(`/apps/${selectedAppId.value}/data/designer${query}`);
};

const openERDCanvas = (viewId?: string) => {
  if (!selectedAppId.value) {
    return;
  }
  if (viewId) {
    void router.push(`/apps/${selectedAppId.value}/data/erd?viewId=${encodeURIComponent(viewId)}`);
    return;
  }
  void router.push(`/apps/${selectedAppId.value}/data/erd`);
};

const openCreateTableModal = (baseTableKey?: string | null) => {
  if (!selectedAppId.value) {
    message.warning(t("dynamic.selectAppFirst"));
    return;
  }
  const suffix = "_detail";
  const candidateKey = baseTableKey ? `${baseTableKey}${suffix}` : "";
  createForm.tableKey = candidateKey;
  createForm.displayName = baseTableKey ? `${baseTableKey}${suffix}` : "";
  createForm.description = "";
  createModalOpen.value = true;
};

const handleCreateTable = async () => {
  if (!selectedAppId.value) {
    return;
  }
  try {
    await createFormRef.value?.validate();
  } catch {
    return;
  }

  creating.value = true;
  try {
    await createDynamicTable({
      appId: selectedAppId.value,
      tableKey: createForm.tableKey.trim(),
      displayName: createForm.displayName.trim(),
      description: createForm.description.trim() || null,
      dbType: "Sqlite",
      fields: [
        {
          name: "id",
          displayName: "ID",
          fieldType: "Long",
          allowNull: false,
          isPrimaryKey: true,
          isAutoIncrement: true,
          isUnique: true,
          defaultValue: null,
          sortOrder: 1
        },
        {
          name: "name",
          displayName: "名称",
          fieldType: "String",
          length: 128,
          allowNull: false,
          isPrimaryKey: false,
          isAutoIncrement: false,
          isUnique: false,
          defaultValue: "",
          sortOrder: 2
        }
      ],
      indexes: []
    });
    message.success(t("dynamic.createTableSuccess"));
    createModalOpen.value = false;
    await loadTableDirectory();
    selectedTableKey.value = createForm.tableKey.trim();
    await loadSelectedTableDetail();
  } catch (error) {
    message.error((error as Error).message || t("dynamic.createTableFailed"));
  } finally {
    creating.value = false;
  }
};

const handleDeleteTable = async (tableKey: string) => {
  if (!selectedAppId.value || !tableKey) {
    return;
  }

  try {
    const check = await getDynamicTableDeleteCheck(tableKey);
    if (!check.canDelete) {
      blockerRows.value = check.blockers;
      deleteWarnings.value = check.warnings;
      blockerModalOpen.value = true;
      message.warning(t("dynamic.deleteBlockedTip", "删除被引用阻断，请先解除引用。"));
      return;
    }

    await deleteDynamicTable(tableKey);
    message.success(t("dynamic.deleteSuccess", "删除成功"));
    await loadTableDirectory();
    await loadSelectedTableDetail();
    loadRelationViewItems();
  } catch (error) {
    message.error((error as Error).message || t("dynamic.deleteFailed", "删除失败"));
  }
};

onMounted(() => {
  void loadAppOptions();
  void refreshAll();
});

watch(
  () => route.params.appId,
  () => {
    syncSelectedAppFromRoute();
    void refreshAll();
  }
);

watch(selectedTableKey, () => {
  void loadSelectedTableDetail();
});

watch([selectedAppId, tableDirectory], () => {
  loadRelationViewItems();
});
</script>

<style scoped>
.data-workbench {
  display: flex;
  flex-direction: column;
  height: calc(100vh - 120px);
  background: #fff;
  border-radius: 8px;
  box-shadow: 0 1px 2px 0 rgba(0, 0, 0, 0.03), 0 1px 6px -1px rgba(0, 0, 0, 0.02), 0 2px 4px 0 rgba(0, 0, 0, 0.02);
  overflow: hidden;
}

.workbench-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px 24px;
  border-bottom: 1px solid #f0f0f0;
  background: #fff;
}

.header-title {
  font-size: 16px;
  font-weight: 600;
  color: #1f1f1f;
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}

.app-selector {
  width: 260px;
}

.workbench-body {
  display: flex;
  flex: 1;
  overflow: hidden;
}

.workbench-sidebar {
  width: 280px;
  display: flex;
  flex-direction: column;
  border-right: 1px solid #f0f0f0;
  background: #fafafa;
}

.sidebar-search {
  padding: 16px;
  border-bottom: 1px solid #f0f0f0;
}

.sidebar-content {
  flex: 1;
  overflow-y: auto;
}

.sidebar-empty {
  padding: 40px 16px;
  text-align: center;
}

.sidebar-menu {
  padding: 8px 0;
}

.menu-group {
  margin-bottom: 16px;
}

.group-title {
  display: flex;
  align-items: center;
  padding: 8px 16px;
  font-size: 12px;
  font-weight: 600;
  color: #8c8c8c;
}

.group-icon {
  margin-right: 6px;
  font-size: 14px;
}

.group-count {
  margin-left: auto;
  background: #e6f4ff;
  color: #1677ff;
  padding: 0 6px;
  border-radius: 10px;
  font-size: 11px;
}

.menu-item {
  padding: 10px 16px 10px 36px;
  cursor: pointer;
  transition: all 0.2s;
  border-right: 3px solid transparent;
}

.menu-item:hover {
  background: #f0f0f0;
}

.menu-item.is-active {
  background: #e6f4ff;
  border-right-color: #1677ff;
}

.item-main {
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.item-name {
  font-size: 14px;
  color: #262626;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.item-desc {
  font-size: 12px;
  color: #8c8c8c;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.workbench-main {
  flex: 1;
  display: flex;
  flex-direction: column;
  background: #fff;
  overflow: hidden;
}

.main-empty {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 100%;
}

.table-detail-panel {
  display: flex;
  flex-direction: column;
  height: 100%;
}

.panel-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 20px 24px;
  border-bottom: 1px solid #f0f0f0;
}

.panel-title {
  display: flex;
  align-items: center;
  gap: 12px;
}

.title-text {
  font-size: 20px;
  font-weight: 600;
  color: #1f1f1f;
}

.panel-actions {
  display: flex;
  gap: 8px;
}

.panel-content {
  flex: 1;
  padding: 24px;
  overflow-y: auto;
}

.stats-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
  gap: 16px;
  margin-bottom: 32px;
}

.stat-card {
  padding: 16px;
  background: #fafafa;
  border-radius: 6px;
  border: 1px solid #f0f0f0;
}

.stat-label {
  font-size: 13px;
  color: #8c8c8c;
  margin-bottom: 8px;
}

.stat-value {
  font-size: 24px;
  font-weight: 600;
  color: #262626;
}

.detail-section {
  background: #fff;
}

.section-title {
  font-size: 16px;
  font-weight: 600;
  color: #1f1f1f;
  margin-bottom: 16px;
}

.field-table {
  border: 1px solid #f0f0f0;
  border-radius: 6px;
}

.field-name {
  font-family: monospace;
  font-size: 13px;
}
</style>




