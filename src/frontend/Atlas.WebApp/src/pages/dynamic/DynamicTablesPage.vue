<template>
  <div class="data-workbench">
    <!-- 顶部：页头 + 应用选择器 + 操作区 -->
    <div class="workbench-header">
      <div class="header-left">
        <span class="header-title">{{ t('dynamic.workbenchTitle') }}</span>
      </div>
      <div class="header-actions">
        <a-select
          v-model:value="selectedAppId"
          class="app-selector"
          :options="appOptions"
          :loading="appLoading"
          show-search
          :filter-option="false"
          :placeholder="t('dynamic.selectAppScope')"
          @search="handleAppSearch"
          @change="handleAppScopeChange"
        />
        <a-divider type="vertical" />
        <a-button @click="refreshAll">
          <template #icon><ReloadOutlined /></template>
          {{ t("dynamic.refresh") }}
        </a-button>
        <a-button type="primary" :disabled="!selectedAppId" @click="openCreateTableModal()">
          <template #icon><PlusOutlined /></template>
          {{ t("dynamic.createTable") }}
        </a-button>
      </div>
    </div>

    <!-- 模式切换 Tab -->
    <div class="mode-tabs">
      <a-tabs v-model:activeKey="activeMode" size="small" @change="handleModeChange">
        <a-tab-pane key="tables" :tab="t('dynamic.modeTableModel')" />
        <a-tab-pane key="relations" :tab="t('dynamic.modeRelationModel')" />
        <a-tab-pane key="views" :tab="t('dynamic.modeDataViews')" />
        <a-tab-pane key="changes" :tab="t('dynamic.modeChangeRecords')" />
      </a-tabs>
    </div>

    <div class="workbench-body">
      <!-- 左侧：表目录 -->
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
            <div v-else-if="filteredTables.length === 0" class="sidebar-empty">
              <a-empty :description="t('dynamic.noDataFound')" :image="false" />
            </div>
            <div v-else class="sidebar-menu">
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
                      <div class="item-row">
                        <span class="item-name" :title="table.displayName || table.tableKey">
                          {{ table.displayName || table.tableKey }}
                        </span>
                        <a-tag v-if="table.status" :color="statusTagColor(table.status)" class="item-status-tag">
                          {{ statusLabel(table.status) }}
                        </a-tag>
                      </div>
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
          <!-- 详情头部 -->
          <div class="panel-header">
            <div class="panel-title">
              <span class="title-text">{{ selectedTable.displayName || selectedTable.tableKey }}</span>
              <a-tag color="blue">{{ selectedTable.tableKey }}</a-tag>
              <a-tag v-if="selectedTableDetail" :color="statusTagColor(selectedTableDetail.status)">
                {{ statusLabel(selectedTableDetail.status) }}
              </a-tag>
            </div>
            <div class="panel-actions">
              <a-button type="primary" @click="openTableDesign(selectedTable.tableKey)">
                <template #icon><EditOutlined /></template>
                {{ t("dynamic.openFieldDesign") }}
              </a-button>
              <a-button @click="openRelationsPage(selectedTable.tableKey)">
                <template #icon><PartitionOutlined /></template>
                {{ t("dynamic.openRelationsDesign") }}
              </a-button>
              <a-button @click="openTableCrud(selectedTable.tableKey)">
                <template #icon><DatabaseOutlined /></template>
                {{ t("dynamic.openCrud") }}
              </a-button>
              <a-button @click="openTableNative(selectedTable.tableKey)">
                <template #icon><EyeOutlined /></template>
                {{ t("dynamic.openDataPreview") }}
              </a-button>
              <a-button @click="openApprovalBinding(selectedTable.tableKey)">
                <template #icon><AuditOutlined /></template>
                {{ t("dynamic.openApprovalBinding") }}
              </a-button>
              <a-dropdown>
                <template #overlay>
                  <a-menu>
                    <a-menu-item
                      v-if="selectedTableDetail?.status === 'Archived'"
                      key="restore"
                      @click="handleRestoreTable(selectedTable.tableKey)"
                    >
                      <RollbackOutlined />
                      {{ t("dynamic.restoreTable") }}
                    </a-menu-item>
                    <a-menu-item
                      v-else
                      key="archive"
                      @click="handleArchiveTable(selectedTable.tableKey)"
                    >
                      <InboxOutlined />
                      {{ t("dynamic.archiveTable") }}
                    </a-menu-item>
                    <a-menu-divider />
                    <a-menu-item key="delete">
                      <a-popconfirm
                        :title="t('dynamic.deleteTableConfirm')"
                        :ok-text="t('common.confirm', '确定')"
                        :cancel-text="t('common.cancel', '取消')"
                        @confirm="handleDeleteTable(selectedTable.tableKey)"
                      >
                        <span style="color: #ff4d4f">
                          <DeleteOutlined />
                          {{ t("dynamic.deleteTable") }}
                        </span>
                      </a-popconfirm>
                    </a-menu-item>
                  </a-menu>
                </template>
                <a-button>
                  <MoreOutlined />
                </a-button>
              </a-dropdown>
            </div>
          </div>

          <!-- 详情内容 -->
          <div class="panel-content">
            <a-spin :spinning="detailLoading">
              <!-- 6 张概览卡 -->
              <div class="stats-grid">
                <div class="stat-card">
                  <div class="stat-label">{{ t('dynamic.fieldsCount') }}</div>
                  <div class="stat-value">{{ selectedTableDetail?.fieldCount ?? '-' }}</div>
                </div>
                <div class="stat-card">
                  <div class="stat-label">{{ t('dynamic.indexesCount') }}</div>
                  <div class="stat-value">{{ selectedTableDetail?.indexCount ?? '-' }}</div>
                </div>
                <div class="stat-card">
                  <div class="stat-label">{{ t('dynamic.relationsCount') }}</div>
                  <div class="stat-value">{{ selectedTableDetail?.relationCount ?? '-' }}</div>
                </div>
                <div class="stat-card">
                  <div class="stat-label">{{ t('dynamic.dbType') }}</div>
                  <div class="stat-value stat-value--text">{{ selectedTableDetail?.dbType ?? '-' }}</div>
                </div>
                <div class="stat-card">
                  <div class="stat-label">{{ t('dynamic.approvalBinding') }}</div>
                  <div class="stat-value stat-value--text">
                    <a-badge :status="selectedApprovalBound ? 'success' : 'default'" />
                    {{ selectedApprovalBound ? t('dynamic.approvalBound') : t('dynamic.approvalUnbound') }}
                  </div>
                </div>
                <div class="stat-card">
                  <div class="stat-label">{{ t('dynamic.referenceCount') }}</div>
                  <div class="stat-value">{{ selectedTableDetail?.referenceCount ?? '-' }}</div>
                </div>
              </div>

              <!-- 字段概览 -->
              <div class="detail-section">
                <div class="section-title">{{ t('dynamic.fieldListOverview') }}</div>
                <a-table
                  v-if="selectedTableDetail"
                  :dataSource="selectedTableDetail.previewFields"
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

  <!-- 新建表弹窗 -->
  <a-modal
    v-model:open="createModalOpen"
    :title="t('dynamic.createTableModalTitle')"
    :confirm-loading="creating"
    width="520px"
    @ok="handleCreateTable"
  >
    <a-form ref="createFormRef" layout="vertical" :model="createForm" :rules="createFormRules">
      <a-form-item :label="t('dynamic.selectedTableName')" name="displayName">
        <a-input v-model:value="createForm.displayName" :placeholder="t('dynamic.tableNamePlaceholder')" @change="onDisplayNameChange" />
      </a-form-item>
      <a-form-item :label="t('dynamic.selectedTableKey')" name="tableKey">
        <a-input v-model:value="createForm.tableKey" :placeholder="t('dynamic.tableKeyPlaceholder')" />
      </a-form-item>
      <a-form-item :label="t('common.description')" name="description">
        <a-textarea v-model:value="createForm.description" :rows="2" :placeholder="t('dynamic.tableDescPlaceholder')" />
      </a-form-item>
      <a-form-item :label="t('dynamic.templateLabel')" name="template">
        <a-radio-group v-model:value="createForm.template" button-style="solid">
          <a-radio-button value="basic">{{ t('dynamic.templateBasic') }}</a-radio-button>
          <a-radio-button value="approval">{{ t('dynamic.templateApproval') }}</a-radio-button>
          <a-radio-button value="dict">{{ t('dynamic.templateDict') }}</a-radio-button>
        </a-radio-group>
      </a-form-item>
      <a-form-item name="includeSystemFields">
        <a-checkbox v-model:checked="createForm.includeSystemFields">
          {{ t('dynamic.systemFieldsLabel') }}
        </a-checkbox>
        <div class="form-item-help">{{ t('dynamic.systemFieldsHelp') }}</div>
      </a-form-item>
      <a-form-item name="includeExtraSystemFields">
        <a-checkbox v-model:checked="createForm.includeExtraSystemFields" :disabled="!createForm.includeSystemFields">
          {{ t('dynamic.extraSystemFieldsLabel') }}
        </a-checkbox>
        <div class="form-item-help">{{ t('dynamic.extraSystemFieldsHelp') }}</div>
      </a-form-item>
    </a-form>
  </a-modal>

  <!-- 删除被阻断弹窗 -->
  <a-modal
    v-model:open="blockerModalOpen"
    :title="t('dynamic.deleteBlockedTitle')"
    :footer="null"
    width="680px"
  >
    <div style="margin-bottom: 12px;">{{ t("dynamic.deleteBlockedHint") }}</div>
    <a-table
      :dataSource="blockerRows"
      :columns="blockerColumns"
      :pagination="false"
      size="small"
      rowKey="id"
    />
    <div v-if="deleteWarnings.length > 0" style="margin-top: 12px;">
      <a-alert type="warning" show-icon :message="deleteWarnings.join('；')" />
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
  EyeOutlined,
  AuditOutlined,
  MoreOutlined,
  KeyOutlined,
  InboxOutlined,
  RollbackOutlined,
  DeleteOutlined
} from "@ant-design/icons-vue";
import type { Rule } from "ant-design-vue/es/form";
import {
  archiveDynamicTable,
  createDynamicTable,
  deleteDynamicTable,
  getAppScopedDynamicTables,
  getDynamicTableDeleteCheck,
  getDynamicTableSummary,
  restoreDynamicTable,
  type AppScopedDynamicTableListItem
} from "@/services/dynamic-tables";
import { getLowCodeAppsPaged } from "@/services/lowcode";
import { getCurrentAppIdFromStorage, setCurrentAppIdToStorage } from "@/utils/app-context";
import type { DynamicTableSummary } from "@/types/dynamic-tables";
import type { DeleteCheckBlocker } from "@/services/dynamic-tables";

const { t } = useI18n();
const route = useRoute();
const router = useRouter();

const isMounted = ref(true);
onUnmounted(() => {
  isMounted.value = false;
});

const activeMode = ref<"tables" | "relations" | "views" | "changes">("tables");
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
const selectedTableDetail = ref<DynamicTableSummary | null>(null);
let refreshRequestId = 0;
const blockerModalOpen = ref(false);
const blockerRows = ref<DeleteCheckBlocker[]>([]);
const deleteWarnings = ref<string[]>([]);

const statusLabel = (status: string): string => {
  const map: Record<string, string> = {
    Draft: t("dynamic.statusDraft"),
    Active: t("dynamic.statusActive"),
    HasUnpublishedChanges: t("dynamic.statusHasUnpublishedChanges"),
    Archived: t("dynamic.statusArchived"),
    Disabled: t("dynamic.statusDisabled")
  };
  return map[status] ?? status;
};

const statusTagColor = (status: string): string => {
  const map: Record<string, string> = {
    Draft: "orange",
    Active: "green",
    HasUnpublishedChanges: "gold",
    Archived: "default",
    Disabled: "red"
  };
  return map[status] ?? "default";
};

const filteredTables = computed(() => {
  const kw = sidebarKeyword.value.trim().toLowerCase();
  if (!kw) return tableDirectory.value;
  return tableDirectory.value.filter(
    (tbl) =>
      tbl.tableKey.toLowerCase().includes(kw) ||
      (tbl.displayName ?? "").toLowerCase().includes(kw)
  );
});

const fieldColumns = computed(() => [
  { title: t("designer.entityModeling.fieldName"), dataIndex: "name", key: "name" },
  { title: t("designer.entityModeling.displayName"), dataIndex: "displayName", key: "displayName" },
  { title: t("designer.entityModeling.fieldType"), dataIndex: "fieldType", key: "fieldType" },
  { title: t("designer.entityModeling.allowNull"), dataIndex: "allowNull", key: "allowNull", width: 100 }
]);

const blockerColumns = computed(() => [
  { title: t("dynamic.blockerType"), dataIndex: "type", key: "type", width: 120 },
  { title: t("dynamic.blockerName"), dataIndex: "name", key: "name" },
  { title: t("dynamic.blockerPath"), dataIndex: "path", key: "path" }
]);

const createModalOpen = ref(false);
const creating = ref(false);
const createFormRef = ref();
const createForm = reactive({
  tableKey: "",
  displayName: "",
  description: "",
  template: "basic" as "basic" | "approval" | "dict",
  includeSystemFields: true,
  includeExtraSystemFields: false
});

const tableKeyAutoGenerated = ref(false);

const toSnakeCase = (str: string): string => {
  return str
    .normalize("NFKD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/[^\w\s]/g, "")
    .replace(/\s+/g, "_")
    .replace(/[^A-Za-z0-9_]/g, "")
    .toLowerCase()
    .replace(/^[^a-z]+/, "");
};

const onDisplayNameChange = () => {
  if (tableKeyAutoGenerated.value || createForm.tableKey === "") {
    const generated = toSnakeCase(createForm.displayName);
    if (generated) {
      createForm.tableKey = generated;
      tableKeyAutoGenerated.value = true;
    }
  }
};

const tableState = computed<"empty" | "hasData">(() => {
  return tableDirectory.value.length === 0 ? "empty" : "hasData";
});

const selectedTable = computed(() => {
  if (tableDirectory.value.length === 0) return null;
  return (
    tableDirectory.value.find((item) => item.tableKey === selectedTableKey.value) ??
    tableDirectory.value[0]
  );
});

const selectedApprovalBound = computed(() => !!selectedTableDetail.value?.approvalFlowDefinitionId);

const createFormRules: Record<string, Rule[]> = {
  tableKey: [
    { required: true, message: t("validation.required") },
    { pattern: /^[A-Za-z][A-Za-z0-9_]{1,63}$/, message: t("dynamic.tableKeyRule") }
  ],
  displayName: [{ required: true, message: t("validation.required") }]
};

const loadAppOptions = async (keyword?: string) => {
  appLoading.value = true;
  try {
    const result = await getLowCodeAppsPaged({ pageIndex: 1, pageSize: 20, keyword });
    if (!isMounted.value) return;
    appOptions.value = result.items.map((item) => ({
      label: `${item.name} (${item.appKey})`,
      value: item.id
    }));
  } catch (error) {
    if (!isMounted.value) return;
    message.error((error as Error).message || t("dynamic.loadAppsFailed"));
  } finally {
    if (isMounted.value) appLoading.value = false;
  }
};

const loadTableDirectory = async (appIdSnapshot: string | undefined = selectedAppId.value) => {
  if (!appIdSnapshot) {
    tableDirectory.value = [];
    selectedTableKey.value = "";
    selectedTableDetail.value = null;
    return;
  }
  tableLoading.value = true;
  try {
    const result = await getAppScopedDynamicTables(appIdSnapshot);
    if (!isMounted.value || selectedAppId.value !== appIdSnapshot) return;
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
    if (!isMounted.value) return;
    message.error((error as Error).message || t("dynamic.loadTablesFailed"));
  } finally {
    if (isMounted.value) tableLoading.value = false;
  }
};

const loadSelectedTableDetail = async (
  tableKeySnapshot: string = selectedTableKey.value,
  appIdSnapshot: string | undefined = selectedAppId.value
) => {
  if (!tableKeySnapshot) {
    selectedTableDetail.value = null;
    return;
  }
  detailLoading.value = true;
  try {
    const detail = await getDynamicTableSummary(tableKeySnapshot);
    if (!isMounted.value || selectedTableKey.value !== tableKeySnapshot || selectedAppId.value !== appIdSnapshot) return;
    selectedTableDetail.value = detail;
  } catch (error) {
    if (!isMounted.value) return;
    selectedTableDetail.value = null;
    message.error((error as Error).message || t("dynamic.loadTableDetailFailed"));
  } finally {
    if (isMounted.value) detailLoading.value = false;
  }
};

const refreshAll = async () => {
  const appIdSnapshot = selectedAppId.value;
  const requestId = ++refreshRequestId;
  await loadTableDirectory(appIdSnapshot);
  if (requestId !== refreshRequestId || appIdSnapshot !== selectedAppId.value) return;
  const tableKeySnapshot = selectedTableKey.value;
  if (!tableKeySnapshot) {
    selectedTableDetail.value = null;
    return;
  }
  queueMicrotask(() => {
    if (requestId !== refreshRequestId || appIdSnapshot !== selectedAppId.value || selectedTableKey.value !== tableKeySnapshot) return;
    void loadSelectedTableDetail(tableKeySnapshot, appIdSnapshot);
  });
};

const selectTable = (tableKey: string) => {
  selectedTableKey.value = tableKey;
  void loadSelectedTableDetail();
};

const syncSelectedAppFromRoute = () => {
  const routeAppId = typeof route.params.appId === "string" ? route.params.appId.trim() : "";
  const nextAppId = routeAppId || getCurrentAppIdFromStorage() || undefined;
  if (nextAppId !== selectedAppId.value) selectedAppId.value = nextAppId;
};

const handleAppScopeChange = (value: string | undefined) => {
  selectedAppId.value = value;
  setCurrentAppIdToStorage(value);
  if (value && value !== route.params.appId) {
    void router.push(`/apps/${value}/data`);
  }
};

const handleModeChange = (key: string) => {
  if (!selectedAppId.value) return;
  if (key === "relations") {
    void router.push(`/apps/${selectedAppId.value}/data/erd`);
  } else if (key === "views") {
    void router.push(`/apps/${selectedAppId.value}/data/views`);
  } else if (key === "changes") {
    void router.push(`/apps/${selectedAppId.value}/data/changes`);
  }
};

const openTableCrud = (tableKey: string) => {
  if (!selectedAppId.value || !tableKey) return;
  void router.push(`/apps/${selectedAppId.value}/data/${encodeURIComponent(tableKey)}?tab=data`);
};

const openTableNative = (tableKey: string) => {
  if (!selectedAppId.value || !tableKey) return;
  void router.push(`/apps/${selectedAppId.value}/data/${encodeURIComponent(tableKey)}/native`);
};

const openTableDesign = (tableKey: string) => {
  if (!selectedAppId.value || !tableKey) return;
  void router.push(`/apps/${selectedAppId.value}/data/${encodeURIComponent(tableKey)}/design`);
};

const openRelationsPage = (tableKey: string) => {
  if (!selectedAppId.value || !tableKey) return;
  void router.push(`/apps/${selectedAppId.value}/data/${encodeURIComponent(tableKey)}/relations`);
};

const openApprovalBinding = (tableKey: string) => {
  if (!selectedAppId.value || !tableKey) return;
  void router.push(`/apps/${selectedAppId.value}/data/${encodeURIComponent(tableKey)}/approval`);
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
  createForm.template = "basic";
  createForm.includeSystemFields = true;
  createForm.includeExtraSystemFields = false;
  tableKeyAutoGenerated.value = false;
  createModalOpen.value = true;
};

const handleCreateTable = async () => {
  if (!selectedAppId.value) return;
  try {
    await createFormRef.value?.validate();
  } catch {
    return;
  }
  creating.value = true;
  try {
    const tableKey = createForm.tableKey.trim();
    const fields: import("@/types/dynamic-tables").DynamicFieldDefinition[] = [
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
      }
    ];
    let sortOrder = 2;
    if (createForm.template === "basic" || createForm.template === "approval") {
      fields.push({
        name: "name",
        displayName: "名称",
        fieldType: "String",
        length: 128,
        allowNull: false,
        isPrimaryKey: false,
        isAutoIncrement: false,
        isUnique: false,
        defaultValue: "",
        sortOrder: sortOrder++
      });
    }
    if (createForm.template === "dict") {
      fields.push(
        {
          name: "code",
          displayName: "字典码",
          fieldType: "String",
          length: 64,
          allowNull: false,
          isPrimaryKey: false,
          isAutoIncrement: false,
          isUnique: true,
          defaultValue: "",
          sortOrder: sortOrder++
        },
        {
          name: "label",
          displayName: "显示值",
          fieldType: "String",
          length: 128,
          allowNull: false,
          isPrimaryKey: false,
          isAutoIncrement: false,
          isUnique: false,
          defaultValue: "",
          sortOrder: sortOrder++
        },
        {
          name: "sort_order",
          displayName: "排序",
          fieldType: "Int",
          allowNull: true,
          isPrimaryKey: false,
          isAutoIncrement: false,
          isUnique: false,
          defaultValue: "0",
          sortOrder: sortOrder++
        }
      );
    }
    if (createForm.includeSystemFields) {
      fields.push(
        {
          name: "created_at",
          displayName: "创建时间",
          fieldType: "DateTime",
          allowNull: true,
          isPrimaryKey: false,
          isAutoIncrement: false,
          isUnique: false,
          defaultValue: null,
          sortOrder: sortOrder++
        },
        {
          name: "updated_at",
          displayName: "更新时间",
          fieldType: "DateTime",
          allowNull: true,
          isPrimaryKey: false,
          isAutoIncrement: false,
          isUnique: false,
          defaultValue: null,
          sortOrder: sortOrder++
        }
      );
    }
    if (createForm.includeExtraSystemFields && createForm.includeSystemFields) {
      fields.push(
        {
          name: "created_by",
          displayName: "创建人",
          fieldType: "Long",
          allowNull: true,
          isPrimaryKey: false,
          isAutoIncrement: false,
          isUnique: false,
          defaultValue: null,
          sortOrder: sortOrder++
        },
        {
          name: "updated_by",
          displayName: "更新人",
          fieldType: "Long",
          allowNull: true,
          isPrimaryKey: false,
          isAutoIncrement: false,
          isUnique: false,
          defaultValue: null,
          sortOrder: sortOrder++
        },
        {
          name: "is_deleted",
          displayName: "已删除",
          fieldType: "Bool",
          allowNull: false,
          isPrimaryKey: false,
          isAutoIncrement: false,
          isUnique: false,
          defaultValue: "false",
          sortOrder: sortOrder++
        }
      );
    }
    await createDynamicTable({
      appId: selectedAppId.value,
      tableKey,
      displayName: createForm.displayName.trim(),
      description: createForm.description.trim() || null,
      dbType: "Sqlite",
      fields,
      indexes: []
    });
    message.success(t("dynamic.createTableSuccess"));
    createModalOpen.value = false;
    await loadTableDirectory();
    selectedTableKey.value = tableKey;
    // 创建成功后自动跳转字段设计页
    openTableDesign(tableKey);
  } catch (error) {
    message.error((error as Error).message || t("dynamic.createTableFailed"));
  } finally {
    creating.value = false;
  }
};

const handleDeleteTable = async (tableKey: string) => {
  if (!selectedAppId.value || !tableKey) return;
  try {
    const check = await getDynamicTableDeleteCheck(tableKey);
    if (!check.canDelete) {
      blockerRows.value = check.blockers;
      deleteWarnings.value = check.warnings;
      blockerModalOpen.value = true;
      message.warning(t("dynamic.deleteBlockedTip"));
      return;
    }
    await deleteDynamicTable(tableKey);
    message.success(t("dynamic.deleteSuccess"));
    await loadTableDirectory();
    await loadSelectedTableDetail();
  } catch (error) {
    message.error((error as Error).message || t("dynamic.deleteFailed"));
  }
};

const handleArchiveTable = async (tableKey: string) => {
  if (!selectedAppId.value || !tableKey) return;
  try {
    await archiveDynamicTable(tableKey);
    message.success(t("dynamic.archiveTableSuccess"));
    await loadTableDirectory();
    await loadSelectedTableDetail();
  } catch (error) {
    message.error((error as Error).message || t("dynamic.archiveTableFailed"));
  }
};

const handleRestoreTable = async (tableKey: string) => {
  if (!selectedAppId.value || !tableKey) return;
  try {
    await restoreDynamicTable(tableKey);
    message.success(t("dynamic.restoreTableSuccess"));
    await loadTableDirectory();
    await loadSelectedTableDetail();
  } catch (error) {
    message.error((error as Error).message || t("dynamic.restoreTableFailed"));
  }
};

const handleAppSearch = (keyword: string) => {
  void loadAppOptions(keyword.trim() || undefined);
};

onMounted(() => {
  syncSelectedAppFromRoute();
  void loadAppOptions();
});

watch(
  () => route.params.appId,
  () => { syncSelectedAppFromRoute(); }
);

watch(
  selectedAppId,
  (next, previous) => {
    if (next === previous) return;
    selectedTableKey.value = "";
    selectedTableDetail.value = null;
    void refreshAll();
  },
  { immediate: true }
);

watch(selectedTableKey, (next) => {
  if (next) void loadSelectedTableDetail(next, selectedAppId.value);
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
  flex-shrink: 0;
}

.header-left {
  display: flex;
  align-items: center;
  gap: 12px;
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

.mode-tabs {
  padding: 0 24px;
  border-bottom: 1px solid #f0f0f0;
  background: #fff;
  flex-shrink: 0;
}

.mode-tabs :deep(.ant-tabs-nav) {
  margin-bottom: 0;
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
  flex-shrink: 0;
}

.sidebar-search {
  padding: 12px 16px;
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
  margin-bottom: 8px;
}

.group-title {
  display: flex;
  align-items: center;
  padding: 8px 16px;
  font-size: 12px;
  font-weight: 600;
  color: #8c8c8c;
  user-select: none;
}

.group-icon {
  margin-right: 6px;
  font-size: 13px;
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
  padding: 8px 16px 8px 28px;
  cursor: pointer;
  transition: background 0.15s;
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
  gap: 3px;
}

.item-row {
  display: flex;
  align-items: center;
  gap: 6px;
}

.item-name {
  font-size: 13px;
  color: #262626;
  flex: 1;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.item-status-tag {
  font-size: 11px;
  line-height: 18px;
  padding: 0 4px;
  flex-shrink: 0;
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
  padding: 16px 24px;
  border-bottom: 1px solid #f0f0f0;
  flex-shrink: 0;
}

.panel-title {
  display: flex;
  align-items: center;
  gap: 10px;
  flex: 1;
  min-width: 0;
}

.title-text {
  font-size: 18px;
  font-weight: 600;
  color: #1f1f1f;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.panel-actions {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-shrink: 0;
}

.panel-content {
  flex: 1;
  padding: 24px;
  overflow-y: auto;
}

.stats-grid {
  display: grid;
  grid-template-columns: repeat(3, 1fr);
  gap: 16px;
  margin-bottom: 28px;
}

@media (max-width: 1200px) {
  .stats-grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

.stat-card {
  padding: 16px 20px;
  background: #fafafa;
  border-radius: 8px;
  border: 1px solid #f0f0f0;
  transition: border-color 0.2s;
}

.stat-card:hover {
  border-color: #d0e4ff;
}

.stat-label {
  font-size: 13px;
  color: #8c8c8c;
  margin-bottom: 8px;
}

.stat-value {
  font-size: 28px;
  font-weight: 700;
  color: #1677ff;
  line-height: 1;
}

.stat-value--text {
  font-size: 15px;
  font-weight: 600;
  color: #262626;
}

.detail-section {
  background: #fff;
}

.section-title {
  font-size: 15px;
  font-weight: 600;
  color: #1f1f1f;
  margin-bottom: 12px;
}

.field-table {
  border: 1px solid #f0f0f0;
  border-radius: 6px;
}

.field-name {
  font-family: 'Courier New', monospace;
  font-size: 13px;
}

.form-item-help {
  font-size: 12px;
  color: #8c8c8c;
  margin-top: 2px;
}
</style>
