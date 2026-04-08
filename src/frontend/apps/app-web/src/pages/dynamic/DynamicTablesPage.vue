<template>
  <div class="data-workbench">
    <div class="workbench-header">
      <div class="header-left">
        <span class="header-title">{{ t('dynamicTable.workbenchTitle') }}</span>
      </div>
      <div class="header-actions">
        <a-button @click="refreshAll">
          <template #icon><ReloadOutlined /></template>
          {{ t("dynamicTable.refresh") }}
        </a-button>
        <a-button type="primary" @click="openCreateTableModal()">
          <template #icon><PlusOutlined /></template>
          {{ t("dynamicTable.createTable") }}
        </a-button>
      </div>
    </div>

    <div class="workbench-body">
      <div class="workbench-sidebar">
        <div class="sidebar-search">
          <a-input
            v-model:value="sidebarKeyword"
            :placeholder="t('dynamicTable.searchSidebarPlaceholder')"
            allow-clear
          >
            <template #prefix><SearchOutlined style="color: rgba(0,0,0,.25)" /></template>
          </a-input>
        </div>

        <div class="sidebar-content">
          <a-spin :spinning="tableLoading">
            <div v-if="filteredTables.length === 0" class="sidebar-empty">
              <a-empty :description="tableDirectory.length === 0 ? t('dynamicTable.emptyNoTables') : t('dynamicTable.noDataFound')" :image="false" />
            </div>
            <div v-else class="sidebar-menu">
              <div class="menu-group">
                <div class="group-title">
                  <TableOutlined class="group-icon" />
                  <span>{{ t('dynamicTable.tableDirectory') }}</span>
                  <span class="group-count">{{ filteredTables.length }}</span>
                </div>
                <div class="group-list">
                  <div
                    v-for="tbl in filteredTables"
                    :key="tbl.tableKey"
                    class="menu-item"
                    :class="{ 'is-active': tbl.tableKey === selectedTableKey }"
                    @click="selectTable(tbl.tableKey)"
                  >
                    <div class="item-main">
                      <div class="item-row">
                        <span class="item-name" :title="tbl.displayName || tbl.tableKey">
                          {{ tbl.displayName || tbl.tableKey }}
                        </span>
                        <a-tag v-if="tbl.status" :color="statusTagColor(tbl.status)" class="item-status-tag">
                          {{ statusLabel(tbl.status) }}
                        </a-tag>
                      </div>
                      <span class="item-desc" :title="tbl.tableKey">{{ tbl.tableKey }}</span>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </a-spin>
        </div>
      </div>

      <div class="workbench-main">
        <div v-if="tableDirectory.length === 0 && !tableLoading" class="main-empty">
          <a-empty :description="t('dynamicTable.emptyNoTables')">
            <a-button type="primary" style="margin-top: 16px" @click="openCreateTableModal()">
              {{ t("dynamicTable.createFirstTable") }}
            </a-button>
          </a-empty>
        </div>
        <div v-else-if="!selectedTable" class="main-empty">
          <a-empty :description="t('dynamicTable.selectTableFirst')" :image="false" />
        </div>
        <div v-else class="table-detail-panel">
          <div class="panel-header">
            <div class="panel-title">
              <span class="title-text">{{ selectedTable.displayName || selectedTable.tableKey }}</span>
              <a-tag color="blue">{{ selectedTable.tableKey }}</a-tag>
              <a-tag v-if="selectedTableDetail" :color="statusTagColor(selectedTableDetail.status)">
                {{ statusLabel(selectedTableDetail.status) }}
              </a-tag>
            </div>
            <div class="panel-actions">
              <a-button type="primary" @click="goToDesign(selectedTable.tableKey)">
                <template #icon><EditOutlined /></template>
                {{ t("dynamicTable.openFieldDesign") }}
              </a-button>
              <a-button @click="goToRecords(selectedTable.tableKey)">
                <template #icon><DatabaseOutlined /></template>
                {{ t("dynamicTable.openDataBrowse") }}
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
                      {{ t("dynamicTable.restoreTable") }}
                    </a-menu-item>
                    <a-menu-item
                      v-else
                      key="archive"
                      @click="handleArchiveTable(selectedTable.tableKey)"
                    >
                      <InboxOutlined />
                      {{ t("dynamicTable.archiveTable") }}
                    </a-menu-item>
                    <a-menu-divider />
                    <a-menu-item key="delete">
                      <a-popconfirm
                        :title="t('dynamicTable.deleteTableConfirm')"
                        :ok-text="t('common.confirm')"
                        :cancel-text="t('common.cancel')"
                        @confirm="handleDeleteTable(selectedTable.tableKey)"
                      >
                        <span style="color: #ff4d4f">
                          <DeleteOutlined />
                          {{ t("dynamicTable.deleteTable") }}
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

          <div class="panel-content">
            <a-spin :spinning="detailLoading">
              <div class="stats-grid">
                <div class="stat-card">
                  <div class="stat-label">{{ t('dynamicTable.fieldsCount') }}</div>
                  <div class="stat-value">{{ selectedTableDetail?.fieldCount ?? '-' }}</div>
                </div>
                <div class="stat-card">
                  <div class="stat-label">{{ t('dynamicTable.indexesCount') }}</div>
                  <div class="stat-value">{{ selectedTableDetail?.indexCount ?? '-' }}</div>
                </div>
                <div class="stat-card">
                  <div class="stat-label">{{ t('dynamicTable.relationsCount') }}</div>
                  <div class="stat-value">{{ selectedTableDetail?.relationCount ?? '-' }}</div>
                </div>
                <div class="stat-card">
                  <div class="stat-label">{{ t('dynamicTable.dbType') }}</div>
                  <div class="stat-value stat-value--text">{{ selectedTableDetail?.dbType ?? '-' }}</div>
                </div>
                <div class="stat-card">
                  <div class="stat-label">{{ t('dynamicTable.approvalBinding') }}</div>
                  <div class="stat-value stat-value--text">
                    <a-badge :status="selectedApprovalBound ? 'success' : 'default'" />
                    {{ selectedApprovalBound ? t('dynamicTable.approvalBound') : t('dynamicTable.approvalUnbound') }}
                  </div>
                </div>
                <div class="stat-card">
                  <div class="stat-label">{{ t('dynamicTable.referenceCount') }}</div>
                  <div class="stat-value">{{ selectedTableDetail?.referenceCount ?? '-' }}</div>
                </div>
              </div>

              <div class="detail-section">
                <div class="section-title">{{ t('dynamicTable.fieldListOverview') }}</div>
                <a-table
                  v-if="selectedTableDetail"
                  :data-source="selectedTableDetail.previewFields"
                  :columns="fieldColumns"
                  size="small"
                  :pagination="{ pageSize: 10 }"
                  row-key="name"
                  class="field-table"
                >
                  <template #bodyCell="{ column, record }">
                    <template v-if="column.key === 'name'">
                      <span class="field-name">
                        <KeyOutlined
                          v-if="record.isPrimaryKey"
                          style="color: #faad14; margin-right: 4px"
                          :title="t('dynamicTable.isPrimaryKey')"
                        />
                        {{ record.name }}
                      </span>
                    </template>
                    <template v-else-if="column.key === 'allowNull'">
                      <a-tag :color="record.allowNull ? 'default' : 'red'">
                        {{ record.allowNull ? t("dynamicTable.yes") : t("dynamicTable.no") }}
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
    :title="t('dynamicTable.createTableModalTitle')"
    :confirm-loading="creating"
    width="520px"
    @ok="handleCreateTable"
  >
    <a-form ref="createFormRef" layout="vertical" :model="createForm" :rules="createFormRules">
      <a-form-item :label="t('dynamicTable.tableNameLabel')" name="displayName">
        <a-input v-model:value="createForm.displayName" :placeholder="t('dynamicTable.tableNamePlaceholder')" @change="onDisplayNameChange" />
      </a-form-item>
      <a-form-item :label="t('dynamicTable.tableKeyLabel')" name="tableKey">
        <a-input v-model:value="createForm.tableKey" :placeholder="t('dynamicTable.tableKeyPlaceholder')" />
      </a-form-item>
      <a-form-item :label="t('dynamicTable.descriptionLabel')" name="description">
        <a-textarea v-model:value="createForm.description" :rows="2" :placeholder="t('dynamicTable.tableDescPlaceholder')" />
      </a-form-item>
      <a-form-item :label="t('dynamicTable.templateLabel')" name="template">
        <a-radio-group v-model:value="createForm.template" button-style="solid">
          <a-radio-button value="basic">{{ t('dynamicTable.templateBasic') }}</a-radio-button>
          <a-radio-button value="approval">{{ t('dynamicTable.templateApproval') }}</a-radio-button>
          <a-radio-button value="dict">{{ t('dynamicTable.templateDict') }}</a-radio-button>
        </a-radio-group>
      </a-form-item>
      <a-form-item name="includeSystemFields">
        <a-checkbox v-model:checked="createForm.includeSystemFields">
          {{ t('dynamicTable.systemFieldsLabel') }}
        </a-checkbox>
        <div class="form-item-help">{{ t('dynamicTable.systemFieldsHelp') }}</div>
      </a-form-item>
      <a-form-item name="includeExtraSystemFields">
        <a-checkbox v-model:checked="createForm.includeExtraSystemFields" :disabled="!createForm.includeSystemFields">
          {{ t('dynamicTable.extraSystemFieldsLabel') }}
        </a-checkbox>
        <div class="form-item-help">{{ t('dynamicTable.extraSystemFieldsHelp') }}</div>
      </a-form-item>
    </a-form>
  </a-modal>

  <a-modal
    v-model:open="blockerModalOpen"
    :title="t('dynamicTable.deleteBlockedTitle')"
    :footer="null"
    width="680px"
  >
    <div style="margin-bottom: 12px;">{{ t("dynamicTable.deleteBlockedHint") }}</div>
    <a-table
      :data-source="blockerRows"
      :columns="blockerColumns"
      :pagination="false"
      size="small"
      row-key="id"
    />
    <div v-if="deleteWarnings.length > 0" style="margin-top: 12px;">
      <a-alert type="warning" show-icon :message="deleteWarnings.join('; ')" />
    </div>
  </a-modal>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, reactive, ref, watch } from "vue";
import { useI18n } from "vue-i18n";
import { useRouter } from "vue-router";
import { message } from "ant-design-vue";
import {
  ReloadOutlined,
  PlusOutlined,
  SearchOutlined,
  TableOutlined,
  EditOutlined,
  DatabaseOutlined,
  MoreOutlined,
  KeyOutlined,
  InboxOutlined,
  RollbackOutlined,
  DeleteOutlined
} from "@ant-design/icons-vue";
import type { Rule } from "ant-design-vue/es/form";
import { useAppContext } from "@/composables/useAppContext";
import {
  archiveDynamicTable,
  createDynamicTable,
  deleteDynamicTable,
  getAppScopedDynamicTables,
  getDynamicTableDeleteCheck,
  getDynamicTableSummary,
  restoreDynamicTable
} from "@/services/api-dynamic-tables";
import type {
  AppScopedDynamicTableListItem,
  DeleteCheckBlocker,
  DynamicFieldDefinition,
  DynamicTableSummary
} from "@/types/dynamic-tables";

const { t } = useI18n();
const router = useRouter();
const { appKey, appId } = useAppContext();

const isMounted = ref(true);
onUnmounted(() => { isMounted.value = false; });

const tableLoading = ref(false);
const detailLoading = ref(false);
const sidebarKeyword = ref("");
const selectedTableKey = ref("");
const tableDirectory = ref<AppScopedDynamicTableListItem[]>([]);
const selectedTableDetail = ref<DynamicTableSummary | null>(null);
let refreshRequestId = 0;
const blockerModalOpen = ref(false);
const blockerRows = ref<DeleteCheckBlocker[]>([]);
const deleteWarnings = ref<string[]>([]);

const statusLabel = (status: string): string => {
  const map: Record<string, string> = {
    Draft: t("dynamicTable.statusDraft"),
    Active: t("dynamicTable.statusActive"),
    HasUnpublishedChanges: t("dynamicTable.statusHasUnpublishedChanges"),
    Archived: t("dynamicTable.statusArchived"),
    Disabled: t("dynamicTable.statusDisabled")
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
  { title: t("dynamicTable.fieldName"), dataIndex: "name", key: "name" },
  { title: t("dynamicTable.fieldDisplayName"), dataIndex: "displayName", key: "displayName" },
  { title: t("dynamicTable.fieldType"), dataIndex: "fieldType", key: "fieldType" },
  { title: t("dynamicTable.allowNull"), dataIndex: "allowNull", key: "allowNull", width: 100 }
]);

const blockerColumns = computed(() => [
  { title: t("dynamicTable.blockerType"), dataIndex: "type", key: "type", width: 120 },
  { title: t("dynamicTable.blockerName"), dataIndex: "name", key: "name" },
  { title: t("dynamicTable.blockerPath"), dataIndex: "path", key: "path" }
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
    { required: true, message: t("dynamicTable.fieldValueRequired") },
    { pattern: /^[A-Za-z][A-Za-z0-9_]{1,63}$/, message: t("dynamicTable.tableKeyRule") }
  ],
  displayName: [{ required: true, message: t("dynamicTable.fieldValueRequired") }]
};

const loadTableDirectory = async () => {
  const currentAppId = appId.value;
  if (!currentAppId) {
    tableDirectory.value = [];
    selectedTableKey.value = "";
    selectedTableDetail.value = null;
    return;
  }
  tableLoading.value = true;
  try {
    const result = await getAppScopedDynamicTables(currentAppId);
    if (!isMounted.value) return;
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
    message.error((error as Error).message || t("dynamicTable.loadTablesFailed"));
  } finally {
    if (isMounted.value) tableLoading.value = false;
  }
};

const loadSelectedTableDetail = async (tableKeySnapshot: string = selectedTableKey.value) => {
  if (!tableKeySnapshot) {
    selectedTableDetail.value = null;
    return;
  }
  detailLoading.value = true;
  try {
    const detail = await getDynamicTableSummary(tableKeySnapshot);
    if (!isMounted.value || selectedTableKey.value !== tableKeySnapshot) return;
    selectedTableDetail.value = detail;
  } catch (error) {
    if (!isMounted.value) return;
    selectedTableDetail.value = null;
    message.error((error as Error).message || t("dynamicTable.loadTableDetailFailed"));
  } finally {
    if (isMounted.value) detailLoading.value = false;
  }
};

const refreshAll = async () => {
  const requestId = ++refreshRequestId;
  await loadTableDirectory();
  if (requestId !== refreshRequestId) return;
  const tableKeySnapshot = selectedTableKey.value;
  if (!tableKeySnapshot) {
    selectedTableDetail.value = null;
    return;
  }
  queueMicrotask(() => {
    if (requestId !== refreshRequestId || selectedTableKey.value !== tableKeySnapshot) return;
    void loadSelectedTableDetail(tableKeySnapshot);
  });
};

const selectTable = (tableKey: string) => {
  selectedTableKey.value = tableKey;
  void loadSelectedTableDetail();
};

const goToDesign = (tableKey: string) => {
  void router.push(`/apps/${appKey.value}/data/${encodeURIComponent(tableKey)}/design`);
};

const goToRecords = (tableKey: string) => {
  void router.push(`/apps/${appKey.value}/data/${encodeURIComponent(tableKey)}`);
};

const openCreateTableModal = () => {
  createForm.tableKey = "";
  createForm.displayName = "";
  createForm.description = "";
  createForm.template = "basic";
  createForm.includeSystemFields = true;
  createForm.includeExtraSystemFields = false;
  tableKeyAutoGenerated.value = false;
  createModalOpen.value = true;
};

const handleCreateTable = async () => {
  const currentAppId = appId.value;
  if (!currentAppId) return;
  try {
    await createFormRef.value?.validate();
  } catch {
    return;
  }
  creating.value = true;
  try {
    const tableKey = createForm.tableKey.trim();
    const fields: DynamicFieldDefinition[] = [
      {
        name: "id",
        displayName: t("dynamicTable.defaultFieldId"),
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
        displayName: t("dynamicTable.defaultFieldName"),
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
          displayName: t("dynamicTable.defaultFieldCode"),
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
          displayName: t("dynamicTable.defaultFieldLabel"),
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
          displayName: t("dynamicTable.defaultFieldSortOrder"),
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
          displayName: t("dynamicTable.defaultFieldCreatedAt"),
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
          displayName: t("dynamicTable.defaultFieldUpdatedAt"),
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
          displayName: t("dynamicTable.defaultFieldCreatedBy"),
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
          displayName: t("dynamicTable.defaultFieldUpdatedBy"),
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
          displayName: t("dynamicTable.defaultFieldIsDeleted"),
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
      appId: currentAppId,
      tableKey,
      displayName: createForm.displayName.trim(),
      description: createForm.description.trim() || null,
      dbType: "Sqlite",
      fields,
      indexes: []
    });
    message.success(t("dynamicTable.createTableSuccess"));
    createModalOpen.value = false;
    await loadTableDirectory();
    selectedTableKey.value = tableKey;
    goToDesign(tableKey);
  } catch (error) {
    message.error((error as Error).message || t("dynamicTable.createTableFailed"));
  } finally {
    creating.value = false;
  }
};

const handleDeleteTable = async (tableKey: string) => {
  try {
    const check = await getDynamicTableDeleteCheck(tableKey);
    if (!check.canDelete) {
      blockerRows.value = check.blockers;
      deleteWarnings.value = check.warnings;
      blockerModalOpen.value = true;
      message.warning(t("dynamicTable.deleteBlockedTip"));
      return;
    }
    await deleteDynamicTable(tableKey);
    message.success(t("dynamicTable.deleteSuccess"));
    await loadTableDirectory();
    await loadSelectedTableDetail();
  } catch (error) {
    message.error((error as Error).message || t("dynamicTable.deleteFailed"));
  }
};

const handleArchiveTable = async (tableKey: string) => {
  try {
    await archiveDynamicTable(tableKey);
    message.success(t("dynamicTable.archiveTableSuccess"));
    await loadTableDirectory();
    await loadSelectedTableDetail();
  } catch (error) {
    message.error((error as Error).message || t("dynamicTable.archiveTableFailed"));
  }
};

const handleRestoreTable = async (tableKey: string) => {
  try {
    await restoreDynamicTable(tableKey);
    message.success(t("dynamicTable.restoreTableSuccess"));
    await loadTableDirectory();
    await loadSelectedTableDetail();
  } catch (error) {
    message.error((error as Error).message || t("dynamicTable.restoreTableFailed"));
  }
};

onMounted(() => { void refreshAll(); });

watch(appId, (next, previous) => {
  if (next === previous) return;
  selectedTableKey.value = "";
  selectedTableDetail.value = null;
  void refreshAll();
});

watch(selectedTableKey, (next) => {
  if (next) void loadSelectedTableDetail(next);
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
