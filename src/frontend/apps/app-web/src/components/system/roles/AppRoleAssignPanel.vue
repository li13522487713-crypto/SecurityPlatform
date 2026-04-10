<template>
  <div class="role-assign-panel">
    <div class="panel-header">
      <div class="panel-title">
        {{ t("systemRoles.assignPanelTitle", { roleName, roleCode }) }}
      </div>
      <a-space>
        <a-button type="primary" :loading="submitting" @click="submitAssign">{{ t("systemRoles.saveAssign") }}</a-button>
      </a-space>
    </div>

    <div class="panel-content">
      <a-spin :spinning="loading">
        <a-tabs v-model:active-key="activeTab">
          <!-- Basic Info Tab -->
          <a-tab-pane key="basic-info" :tab="t('systemRoles.basicInfoTab')">
            <a-alert
              :message="t('systemRoles.basicInfoTabHint')"
              type="info"
              show-icon
              style="margin-bottom: 12px"
            />
            <a-form layout="vertical">
              <a-form-item :label="t('appsRoles.labelCode')">
                <a-input v-model:value="basicForm.code" disabled />
              </a-form-item>
              <a-form-item :label="t('appsRoles.labelName')">
                <a-input v-model:value="basicForm.name" :disabled="!canAssignPermissions" />
              </a-form-item>
              <a-form-item :label="t('appsRoles.labelDesc')">
                <a-textarea v-model:value="basicForm.description" :rows="3" :disabled="!canAssignPermissions" />
              </a-form-item>
            </a-form>
          </a-tab-pane>

          <!-- API Permissions Tab -->
          <a-tab-pane v-if="canAssignPermissions" key="permissions" :tab="t('systemRoles.permissionTab')">
            <a-alert
              :message="t('systemRoles.permissionTabHint')"
              type="info"
              show-icon
              style="margin-bottom: 12px"
            />
            <a-input-search
              v-model:value="permissionSearchKeyword"
              :placeholder="t('systemRoles.permissionSearchPlaceholder')"
              allow-clear
              style="margin-bottom: 8px"
              @search="handlePermissionSearch"
              @change="handlePermissionSearchClear"
            />
            <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px;">
              <a-space>
                <a-button size="small" @click="selectAllPermissions">{{ t("systemRoles.selectAll") }}</a-button>
                <a-button size="small" @click="clearAllPermissions">{{ t("systemRoles.clearAll") }}</a-button>
                <a-tag v-if="permissionSelectedCount > 0" color="blue">
                  {{ t("systemRoles.selectedCount", { count: permissionSelectedCount }) }}
                </a-tag>
              </a-space>
              <a-switch
                v-model:checked="permissionsCheckStrictly"
                :checked-children="t('systemRoles.independentSelection')"
                :un-checked-children="t('systemRoles.parentChildLinkage')"
              />
            </div>
            <a-tree
              v-model:checked-keys="permissionCheckedKeys"
              checkable
              :check-strictly="permissionsCheckStrictly"
              :tree-data="permissionTreeData"
              :title-render="renderPermissionTitle"
              :selectable="false"
              default-expand-all
              style="max-height: 400px; overflow-y: auto; border: 1px solid #f0f0f0; border-radius: 6px; padding: 12px; background: #fafafa;"
            />
          </a-tab-pane>

          <!-- Page Permissions Tab -->
          <a-tab-pane v-if="canAssignPermissions" key="pages" :tab="t('systemRoles.pagePermissionTab')">
            <a-alert
              :message="t('systemRoles.pagePermissionTabHint')"
              type="info"
              show-icon
              style="margin-bottom: 12px"
            />
            <div style="display: flex; justify-content: flex-start; align-items: center; margin-bottom: 8px;">
              <a-space>
                <a-button size="small" @click="selectAllPages">{{ t("systemRoles.selectAll") }}</a-button>
                <a-button size="small" @click="clearAllPages">{{ t("systemRoles.clearAll") }}</a-button>
                <a-tag v-if="pageCheckedKeys.length > 0" color="blue">
                  {{ t("systemRoles.selectedCount", { count: pageCheckedKeys.length }) }}
                </a-tag>
              </a-space>
            </div>
            <a-checkbox-group v-model:value="pageCheckedKeys" style="display: block;">
              <div v-for="page in availablePages" :key="page.id" style="padding: 6px 0;">
                <a-checkbox :value="page.id">
                  {{ page.name }}
                  <span v-if="page.routePath" style="font-size: 11px; color: var(--color-text-tertiary);">
                    ({{ page.routePath }})
                  </span>
                </a-checkbox>
              </div>
            </a-checkbox-group>
            <a-empty v-if="!availablePages.length" :description="t('systemRoles.noAvailablePages')" />
          </a-tab-pane>

          <!-- Field Permissions Tab -->
          <a-tab-pane v-if="canAssignPermissions" key="field-permissions" :tab="t('systemRoles.fieldPermissionTab')">
            <a-alert
              :message="t('systemRoles.fieldPermissionTabHint')"
              type="info"
              show-icon
              style="margin-bottom: 12px"
            />
            <a-select
              v-model:value="fieldPermissionTableKey"
              style="width: 100%; margin-bottom: 12px"
              :placeholder="t('systemRoles.dynamicTableSelectPlaceholder')"
              :options="dynamicTableOptions"
              :loading="dynamicTableLoading"
              :filter-option="false"
              show-search
              @search="handleDynamicTableSearch"
              @change="handleFieldPermissionTableChange"
              @focus="() => loadDynamicTableOptions()"
            />
            <a-empty
              v-if="!fieldPermissionTableKey"
              :description="t('systemRoles.fieldPermNoTableSelected')"
              style="margin: 32px 0"
            />
            <div v-if="fieldPermissionTableKey" class="table-container">
              <a-table
                :data-source="fieldPermissionRows"
                :loading="fieldPermissionLoading"
                :pagination="false"
                row-key="fieldName"
                size="small"
                :scroll="{ y: 'calc(100vh - 350px)' }"
              >
                <a-table-column key="label" :title="t('systemRoles.fieldColumnLabel')" data-index="label" />
                <a-table-column key="canView" data-index="canView" align="center" width="100">
                  <template #title>
                    <div style="display: flex; flex-direction: column; align-items: center; gap: 4px;">
                      <span>{{ t("systemRoles.fieldColumnCanView") }}</span>
                      <a-switch size="small" :checked="isAllViewChecked" @change="toggleAllView" />
                    </div>
                  </template>
                  <template #default="{ record }">
                    <a-switch
                      :checked="record.canView"
                      size="small"
                      @change="(value: boolean) => handleFieldViewChange(record.fieldName, value)"
                    />
                  </template>
                </a-table-column>
                <a-table-column key="canEdit" data-index="canEdit" align="center" width="100">
                  <template #title>
                    <div style="display: flex; flex-direction: column; align-items: center; gap: 4px;">
                      <span>{{ t("systemRoles.fieldColumnCanEdit") }}</span>
                      <a-switch size="small" :checked="isAllEditChecked" @change="toggleAllEdit" />
                    </div>
                  </template>
                  <template #default="{ record }">
                    <a-switch
                      :checked="record.canEdit"
                      size="small"
                      :disabled="!record.canView"
                      @change="(value: boolean) => handleFieldEditChange(record.fieldName, value)"
                    />
                  </template>
                </a-table-column>
              </a-table>
            </div>
          </a-tab-pane>

          <!-- Data Scope Tab -->
          <a-tab-pane v-if="canManageDataScope" key="data-scope" :tab="t('systemRoles.dataScopeTab')">
            <a-alert
              :message="t('systemRoles.dataScopeTabHint')"
              type="info"
              show-icon
              style="margin-bottom: 16px"
            />
            <a-radio-group v-model:value="assignModel.dataScope" button-style="solid" class="display-block-radio">
              <a-radio-button
                v-for="opt in dataScopeOptions"
                :key="opt.value"
                :value="opt.value"
                style="display: block; width: 100%; text-align: left; margin-bottom: 8px; border-radius: 6px; border-left: 1px solid #d9d9d9;"
              >
                <div class="scope-radio-content">
                  <div class="scope-label">{{ opt.label }}</div>
                  <div class="scope-desc">{{ opt.description }}</div>
                </div>
              </a-radio-button>
            </a-radio-group>

            <div v-if="assignModel.dataScope === 2" style="margin-top: 12px;">
              <a-tree-select
                v-model:value="assignModel.deptIds"
                tree-checkable
                tree-default-expand-all
                :tree-data="departmentTreeData"
                allow-clear
                show-search
                tree-node-filter-prop="title"
                style="width: 100%"
                :placeholder="t('systemRoles.departmentSelectPlaceholder')"
                :dropdown-style="{ maxHeight: '400px', overflow: 'auto' }"
                :show-checked-strategy="'SHOW_ALL'"
              />
            </div>
            <a-alert
              style="margin-top: 12px"
              type="success"
              show-icon
              :message="dataScopePreviewTitle"
              :description="dataScopePreviewDescription"
            />
          </a-tab-pane>
        </a-tabs>
      </a-spin>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, h, reactive, ref, watch, onBeforeUnmount, type VNode } from "vue";
import { message } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import { debounce, handleTree } from "@atlas/shared-core";
import type { DataNode } from "ant-design-vue/es/tree";
import {
  getAppPermissions,
  getRoleDetail,
  updateRolePermissions,
  getAppRoleDataScope,
  updateAppRoleDataScope,
  getAvailableAppPages,
  getRolePageIds,
  updateRolePages,
  getAvailableDynamicTables,
  getRoleFieldPermissions,
  updateRoleFieldPermissions,
  getAppDynamicTableFields,
  type AppPageListItem,
  type AppRoleFieldPermissionGroupDto,
  getDepartmentsAll
} from "@/services/api-org-management";

const { t } = useI18n();

const props = defineProps<{
  appId: string;
  roleId: string;
  roleCode: string;
  roleName: string;
  canAssignPermissions: boolean;
  canManageDataScope: boolean;
}>();

const emit = defineEmits<{
  (e: "success"): void;
}>();

const isMounted = ref(true);
onBeforeUnmount(() => { isMounted.value = false; });

const loading = ref(false);
const submitting = ref(false);
const activeTab = ref("basic-info");

const basicForm = reactive({ code: "", name: "", description: "" });

const dataScopeOptions = [
  { value: 0, label: t("systemRoles.scopeAllLabel"), description: t("systemRoles.scopeAllDesc") },
  { value: 2, label: t("systemRoles.scopeCustomDeptLabel"), description: t("systemRoles.scopeCustomDeptDesc") },
  { value: 3, label: t("systemRoles.scopeCurrentDeptLabel"), description: t("systemRoles.scopeCurrentDeptDesc") },
  { value: 4, label: t("systemRoles.scopeCurrentDeptAndBelowLabel"), description: t("systemRoles.scopeCurrentDeptAndBelowDesc") },
  { value: 5, label: t("systemRoles.scopeOnlySelfLabel"), description: t("systemRoles.scopeOnlySelfDesc") },
];

const assignModel = reactive({
  permissionCodes: [] as string[],
  dataScope: 0,
  deptIds: [] as string[]
});

// Permission tree
const permissionTreeData = ref<DataNode[]>([]);
const permissionCheckedKeys = ref<(string | number)[] | { checked: (string | number)[]; halfChecked: (string | number)[] }>([]);
const permissionsCheckStrictly = ref(true);
const permissionSearchKeyword = ref("");

interface ExtendedTreeNode extends DataNode {
  nodeDescription?: string;
}

const renderPermissionTitle = (node: DataNode): VNode => {
  const ext = node as ExtendedTreeNode;
  const titleText = ext.title as string;
  if (ext.nodeDescription) {
    return h("span", { title: ext.nodeDescription, style: "cursor: default;" }, [
      h("span", {}, titleText),
      h("span", { style: "display: block; font-size: 11px; color: var(--color-text-tertiary, #8c8c8c); line-height: 1.4; margin-top: 1px;" }, ext.nodeDescription)
    ]);
  }
  return h("span", {}, titleText);
};

const permissionSelectedCount = computed(() => {
  const keys = Array.isArray(permissionCheckedKeys.value)
    ? permissionCheckedKeys.value
    : (permissionCheckedKeys.value?.checked ?? []);
  return keys.filter((k) => typeof k === "string" && !k.startsWith("module_")).length;
});

const collectTreeKeys = (nodes: DataNode[]): (string | number)[] => {
  let keys: (string | number)[] = [];
  for (const node of nodes) {
    keys.push(node.key as string | number);
    if (node.children?.length) keys = keys.concat(collectTreeKeys(node.children));
  }
  return keys;
};

const selectAllPermissions = () => { permissionCheckedKeys.value = collectTreeKeys(permissionTreeData.value); };
const clearAllPermissions = () => { permissionCheckedKeys.value = []; };

// Page permissions
const availablePages = ref<AppPageListItem[]>([]);
const pageCheckedKeys = ref<number[]>([]);

const selectAllPages = () => { pageCheckedKeys.value = availablePages.value.map((p) => p.id as unknown as number); };
const clearAllPages = () => { pageCheckedKeys.value = []; };

// Department tree for data scope
const departmentTreeData = ref<DataNode[]>([]);

// Field permissions
const dynamicTableOptions = ref<Array<{ label: string; value: string }>>([]);
const dynamicTableLoading = ref(false);
const fieldPermissionLoading = ref(false);
const fieldPermissionTableKey = ref<string>();
const fieldPermissionRows = ref<Array<{ fieldName: string; label: string; canView: boolean; canEdit: boolean }>>([]);
const existingFieldGroups = ref<AppRoleFieldPermissionGroupDto[]>([]);

const isAllViewChecked = computed(() => {
  if (!fieldPermissionRows.value.length) return false;
  return fieldPermissionRows.value.every((r) => r.canView);
});

const isAllEditChecked = computed(() => {
  if (!fieldPermissionRows.value.length) return false;
  return fieldPermissionRows.value.every((r) => r.canEdit);
});

const toggleAllView = (checked: boolean) => {
  fieldPermissionRows.value.forEach((r) => { r.canView = checked; if (!checked) r.canEdit = false; });
};

const toggleAllEdit = (checked: boolean) => {
  fieldPermissionRows.value.forEach((r) => { r.canEdit = checked; if (checked) r.canView = true; });
};

const handleFieldViewChange = (fieldName: string, value: boolean) => {
  const target = fieldPermissionRows.value.find((r) => r.fieldName === fieldName);
  if (!target) return;
  target.canView = value;
  if (!value) target.canEdit = false;
};

const handleFieldEditChange = (fieldName: string, value: boolean) => {
  const target = fieldPermissionRows.value.find((r) => r.fieldName === fieldName);
  if (!target) return;
  target.canEdit = value;
  if (value) target.canView = true;
};

const dataScopePreviewTitle = computed(() => t("systemRoles.dataScopePreviewTitle"));
const dataScopePreviewDescription = computed(() => {
  if (assignModel.dataScope === 2) {
    return t("systemRoles.dataScopePreviewCustomDept", { count: assignModel.deptIds.length });
  }
  const opt = dataScopeOptions.find((o) => o.value === assignModel.dataScope) ?? dataScopeOptions[0];
  return opt.description;
});

// Data loading

const loadPermissionOptions = async (keyword?: string) => {
  if (!isMounted.value) return;
  try {
    const items = await getAppPermissions(props.appId);
    if (!isMounted.value) return;
    const rootNodes: Record<string, DataNode> = {};
    const kw = keyword?.trim().toLowerCase();
    const filtered = kw ? items.filter((p) => p.name.toLowerCase().includes(kw) || p.code.toLowerCase().includes(kw)) : items;
    filtered.forEach((item) => {
      const parts = item.code.split(":");
      const moduleCode = parts[0] || "default";
      if (!rootNodes[moduleCode]) {
        rootNodes[moduleCode] = { key: `module_${moduleCode}`, title: moduleCode.toUpperCase(), children: [] };
      }
      (rootNodes[moduleCode].children as ExtendedTreeNode[]).push({
        key: item.code,
        title: `${item.name} (${item.code})`,
        nodeDescription: item.description || undefined
      });
    });
    permissionTreeData.value = Object.values(rootNodes);
  } catch {
    message.warning(t("roleAssign.loadPermissionsFailed"));
  }
};

const loadAvailablePages = async () => {
  if (!isMounted.value) return;
  try {
    availablePages.value = await getAvailableAppPages(props.appId);
  } catch { /* ignore */ }
};

const loadRolePageIds = async () => {
  if (!isMounted.value || !props.roleId) return;
  try {
    pageCheckedKeys.value = await getRolePageIds(props.appId, props.roleId);
  } catch { /* ignore */ }
};

const loadDepartmentOptions = async () => {
  if (!isMounted.value || !props.appId) return;
  try {
    const items = await getDepartmentsAll(props.appId);
    if (!isMounted.value) return;
    const formatted = items.map((item) => ({
      ...item,
      key: item.id,
      value: item.id,
      title: item.name,
      id: item.id,
      parentId: item.parentId || "0"
    }));
    departmentTreeData.value = handleTree(formatted, "id", "parentId", "children");
  } catch { /* ignore */ }
};

const loadDynamicTableOptions = async (search?: string) => {
  if (!isMounted.value) return;
  dynamicTableLoading.value = true;
  try {
    const result = await getAvailableDynamicTables(props.appId, search?.trim() || undefined);
    if (!isMounted.value) return;
    dynamicTableOptions.value = result.map((item) => ({
      label: `${item.displayName} (${item.tableKey})`,
      value: item.tableKey
    }));
  } catch {
    message.warning(t("roleAssign.loadDynamicTablesFailed"));
  } finally {
    if (isMounted.value) dynamicTableLoading.value = false;
  }
};

const loadFieldPermissions = async (tableKey: string) => {
  if (!props.roleId || !isMounted.value) return;
  fieldPermissionLoading.value = true;
  try {
    const [fields, groups] = await Promise.all([
      getAppDynamicTableFields(tableKey),
      getRoleFieldPermissions(props.appId, props.roleId)
    ]);
    if (!isMounted.value) return;
    existingFieldGroups.value = groups;
    const currentGroup = groups.find((g) => g.tableKey === tableKey);
    fieldPermissionRows.value = fields.map((field) => {
      const current = currentGroup?.fields.find((f) => f.fieldName === field.name);
      return {
        fieldName: field.name,
        label: field.displayName || field.name,
        canView: current?.canView ?? false,
        canEdit: current?.canEdit ?? false
      };
    });
  } catch {
    message.error(t("systemRoles.loadFieldDefinitionsFailed"));
  } finally {
    if (isMounted.value) fieldPermissionLoading.value = false;
  }
};

const handlePermissionSearch = debounce((value: string) => void loadPermissionOptions(value));
const handlePermissionSearchClear = (e: Event) => {
  const target = e.target as HTMLInputElement;
  if (!target.value) void loadPermissionOptions();
};
const handleDynamicTableSearch = debounce((value: string) => void loadDynamicTableOptions(value));
const handleFieldPermissionTableChange = (value: string) => {
  if (!value) { fieldPermissionRows.value = []; return; }
  void loadFieldPermissions(value);
};

const fetchRoleDetail = async () => {
  if (!props.roleId || !props.appId || !isMounted.value) return;

  loading.value = true;
  fieldPermissionTableKey.value = undefined;
  fieldPermissionRows.value = [];

  try {
    await Promise.all([
      props.canAssignPermissions ? loadPermissionOptions() : Promise.resolve(),
      props.canAssignPermissions ? loadAvailablePages() : Promise.resolve(),
      props.canAssignPermissions ? loadDynamicTableOptions() : Promise.resolve(),
      props.canManageDataScope ? loadDepartmentOptions() : Promise.resolve()
    ]);

    if (!isMounted.value) return;

    const detail = await getRoleDetail(props.appId, props.roleId);
    if (!isMounted.value) return;

    basicForm.code = props.roleCode;
    basicForm.name = props.roleName;
    basicForm.description = detail.description ?? "";
    assignModel.permissionCodes = detail.permissionCodes?.slice() ?? [];

    permissionCheckedKeys.value = assignModel.permissionCodes.slice();

    await loadRolePageIds();

    if (props.canManageDataScope) {
      try {
        const scopeData = await getAppRoleDataScope(props.appId, props.roleId);
        assignModel.dataScope = scopeData.dataScope === 1 ? 0 : (scopeData.dataScope ?? 0);
        assignModel.deptIds = (scopeData.deptIds ?? []).map(String);
      } catch { /* ignore, default to scope 0 */ }
    }
  } catch (error) {
    if (isMounted.value) {
      message.error((error as Error).message || t("systemRoles.loadRoleDetailFailed"));
    }
  } finally {
    if (isMounted.value) loading.value = false;
  }
};

const submitAssign = async () => {
  if (!props.roleId || !props.appId || !isMounted.value) return;

  submitting.value = true;
  try {
    const tasks: Promise<unknown>[] = [];

    if (props.canAssignPermissions) {
      const codes = Array.isArray(permissionCheckedKeys.value)
        ? permissionCheckedKeys.value
        : (permissionCheckedKeys.value?.checked ?? []);
      const permCodes = (codes as string[]).filter((k) => typeof k === "string" && !k.startsWith("module_"));
      tasks.push(updateRolePermissions(props.appId, props.roleId, permCodes));
      tasks.push(updateRolePages(props.appId, props.roleId, pageCheckedKeys.value));
    }

    if (props.canManageDataScope) {
      tasks.push(
        updateAppRoleDataScope(props.appId, props.roleId, {
          dataScope: assignModel.dataScope,
          deptIds: assignModel.dataScope === 2 ? (assignModel.deptIds as unknown as number[]) : undefined
        })
      );
    }

    if (fieldPermissionTableKey.value) {
      const updatedGroups = existingFieldGroups.value
        .filter((g) => g.tableKey !== fieldPermissionTableKey.value)
        .concat([{
          tableKey: fieldPermissionTableKey.value,
          fields: fieldPermissionRows.value
            .filter((r) => r.canView || r.canEdit)
            .map((r) => ({ fieldName: r.fieldName, canView: r.canView, canEdit: r.canEdit }))
        }]);
      tasks.push(updateRoleFieldPermissions(props.appId, props.roleId, updatedGroups));
    }

    await Promise.all(tasks);
    if (!isMounted.value) return;
    message.success(`${t("systemRoles.assignSaveSuccess")} [role=${props.roleCode}]`);
    emit("success");
  } catch (error) {
    if (isMounted.value) {
      message.error((error as Error).message || t("systemRoles.assignSaveFailed"));
    }
  } finally {
    if (isMounted.value) submitting.value = false;
  }
};

watch(() => props.roleId, (newId) => { if (newId) void fetchRoleDetail(); }, { immediate: true });
watch(() => props.roleCode, (v) => { basicForm.code = v ?? ""; });
watch(() => props.roleName, (v) => { basicForm.name = v ?? ""; });
watch(() => assignModel.dataScope, (scope) => { if (scope !== 2) assignModel.deptIds = []; });
</script>

<style scoped>
.role-assign-panel {
  display: flex;
  flex-direction: column;
  height: 100%;
}

.panel-header {
  flex-shrink: 0;
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 16px 20px;
  border-bottom: 1px solid var(--color-border);
  background-color: var(--color-bg-container);
}

.panel-title {
  font-size: 16px;
  font-weight: 600;
  color: var(--color-text-primary);
  display: flex;
  align-items: center;
  gap: 8px;
}

.panel-content {
  flex: 1;
  padding: 0 20px 20px;
  overflow-y: auto;
}

.table-container {
  border: 1px solid var(--color-border);
  border-radius: 6px;
  overflow: hidden;
}

:deep(.ant-tabs-nav) {
  margin-bottom: 16px !important;
}

:deep(.display-block-radio .ant-radio-button-wrapper) {
  height: auto;
  padding: 12px 16px;
}

:deep(.display-block-radio .ant-radio-button-wrapper-checked) {
  background-color: var(--color-primary-bg) !important;
}

.scope-radio-content {
  display: flex;
  flex-direction: column;
  gap: 4px;
  line-height: 1.4;
}

.scope-label {
  font-weight: 600;
  font-size: 14px;
}

.scope-desc {
  font-size: 12px;
  color: var(--color-text-secondary);
}
</style>
