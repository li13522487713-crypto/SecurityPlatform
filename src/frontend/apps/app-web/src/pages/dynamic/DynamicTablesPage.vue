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
import { useDynamicTablesWorkbench } from "@/composables/useDynamicTablesWorkbench";

const {
  t,
  tableLoading,
  detailLoading,
  sidebarKeyword,
  selectedTableKey,
  tableDirectory,
  selectedTableDetail,
  blockerModalOpen,
  blockerRows,
  deleteWarnings,
  statusLabel,
  statusTagColor,
  filteredTables,
  fieldColumns,
  blockerColumns,
  createModalOpen,
  creating,
  createFormRef,
  createForm,
  onDisplayNameChange,
  selectedTable,
  selectedApprovalBound,
  createFormRules,
  refreshAll,
  selectTable,
  goToDesign,
  goToRecords,
  openCreateTableModal,
  handleCreateTable,
  handleDeleteTable,
  handleArchiveTable,
  handleRestoreTable
} = useDynamicTablesWorkbench();
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
