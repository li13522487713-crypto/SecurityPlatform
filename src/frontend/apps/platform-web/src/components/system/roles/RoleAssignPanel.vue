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
              <a-alert
                type="success"
                show-icon
                :message="t('systemRoles.navigationProjectionHint')"
              />
              <a-alert
                style="margin-top: 10px"
                type="warning"
                show-icon
                :message="t('systemRoles.auditTraceHint')"
              />
            </a-form>
          </a-tab-pane>

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

          <a-tab-pane v-if="canAssignMenus" key="menus" :tab="t('systemRoles.menuTab')">
            <a-alert
              :message="t('systemRoles.menuTabHint')"
              type="info"
              show-icon
              style="margin-bottom: 12px"
            />
            <a-input-search
              v-model:value="menuSearchKeyword"
              :placeholder="t('systemRoles.menuSearchPlaceholder')"
              allow-clear
              style="margin-bottom: 8px"
              @search="handleMenuSearch"
              @change="handleMenuSearchClear"
            />
            <div style="display: flex; justify-content: space-between; align-items: center; margin-bottom: 8px;">
              <a-space>
                <a-button size="small" @click="selectAllMenus">{{ t("systemRoles.selectAll") }}</a-button>
                <a-button size="small" @click="clearAllMenus">{{ t("systemRoles.clearAll") }}</a-button>
                <a-tag v-if="menuSelectedCount > 0" color="blue">
                  {{ t("systemRoles.selectedCount", { count: menuSelectedCount }) }}
                </a-tag>
              </a-space>
              <a-switch
                v-model:checked="menusCheckStrictly"
                :checked-children="t('systemRoles.independentSelection')"
                :un-checked-children="t('systemRoles.parentChildLinkage')"
              />
            </div>
            <a-tree
              v-model:checked-keys="menuCheckedKeys"
              checkable
              :check-strictly="menusCheckStrictly"
              :tree-data="menuTreeData"
              :title-render="renderMenuTitle"
              :selectable="false"
              default-expand-all
              style="max-height: 400px; overflow-y: auto; border: 1px solid #f0f0f0; border-radius: 6px; padding: 12px; background: #fafafa;"
            />
          </a-tab-pane>

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

          <a-tab-pane v-if="canManageDataScope" key="data-scope" :tab="t('systemRoles.dataScopeTab')">
            <a-alert
              :message="t('systemRoles.dataScopeTabHint')"
              type="info"
              show-icon
              style="margin-bottom: 16px"
            />
            <a-radio-group v-model:value="assignModel.dataScope" button-style="solid" class="display-block-radio">
              <a-radio-button
                v-for="dataScopeOpt in dataScopeOptions"
                :key="dataScopeOpt.value"
                :value="dataScopeOpt.value"
                style="display: block; width: 100%; text-align: left; margin-bottom: 8px; border-radius: 6px; border-left: 1px solid #d9d9d9;"
              >
                <div class="scope-radio-content">
                  <div class="scope-label">{{ dataScopeOpt.label }}</div>
                  <div class="scope-desc">{{ dataScopeOpt.description }}</div>
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
            <div
              v-if="assignModel.dataScope === 2 && selectedDeptNames.length > 0"
              style="margin-top: 8px; padding: 8px 12px; background: var(--color-bg-layout); border-radius: 6px; font-size: 12px; color: var(--color-text-secondary); line-height: 1.8;"
            >
              <span v-for="(name, i) in selectedDeptNames.slice(0, 8)" :key="i">
                <a-tag size="small">{{ name }}</a-tag>
              </span>
              <span v-if="selectedDeptNames.length > 8" style="margin-left: 4px;">
                {{ t("systemRoles.moreDepartmentsSuffix", { count: selectedDeptNames.length - 8 }) }}
              </span>
            </div>
            <a-alert
              v-if="assignModel.dataScope === 6 && !projectScopeEnabled"
              style="margin-top: 8px"
              type="warning"
              show-icon
              :message="t('systemRoles.projectScopeConsistencyWarning')"
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
import {
  getRoleDetail,
  updateRolePermissions,
  updateRoleMenus,
  setRoleDataScope,
  getPermissionsPaged,
  getMenusAll,
  getDepartmentsAll
} from "@/services/api-users";
import {
  getAppScopedDynamicTables,
  getCurrentProjectAppId,
  getDynamicTableFields,
  getDynamicFieldPermissions,
  setDynamicFieldPermissions
} from "@/services/api-dynamic-tables";
import { debounce, handleTree, getProjectScopeEnabled } from "@atlas/shared-core";
import type { DataNode } from "ant-design-vue/es/tree";

const { t } = useI18n();

const props = defineProps<{
  roleId: string | null;
  roleCode: string;
  roleName: string;
  canAssignPermissions: boolean;
  canAssignMenus: boolean;
  canManageDataScope: boolean;
}>();

const emit = defineEmits<{
  (e: "success"): void;
}>();

const isMounted = ref(true);
onBeforeUnmount(() => {
  isMounted.value = false;
});

const loading = ref(false);
const submitting = ref(false);
const activeTab = ref("basic-info");
const projectScopeEnabled = getProjectScopeEnabled();
const basicForm = reactive({
  code: props.roleCode ?? "",
  name: props.roleName ?? "",
  description: ""
});
const basicFormSnapshot = ref({
  name: props.roleName ?? "",
  description: ""
});
const basicInfoChanged = computed(() => {
  return (
    basicForm.name.trim() !== basicFormSnapshot.value.name.trim() ||
    basicForm.description.trim() !== basicFormSnapshot.value.description.trim()
  );
});

const dataScopeOptions = [
  { value: 0, label: t("systemRoles.scopeAllLabel"), description: t("systemRoles.scopeAllDesc") },
  { value: 2, label: t("systemRoles.scopeCustomDeptLabel"), description: t("systemRoles.scopeCustomDeptDesc") },
  { value: 3, label: t("systemRoles.scopeCurrentDeptLabel"), description: t("systemRoles.scopeCurrentDeptDesc") },
  { value: 4, label: t("systemRoles.scopeCurrentDeptAndBelowLabel"), description: t("systemRoles.scopeCurrentDeptAndBelowDesc") },
  { value: 5, label: t("systemRoles.scopeOnlySelfLabel"), description: t("systemRoles.scopeOnlySelfDesc") },
  { value: 6, label: t("systemRoles.scopeProjectLabel"), description: t("systemRoles.scopeProjectDesc") }
];

const assignModel = reactive({
  permissionIds: [] as number[],
  menuIds: [] as string[],
  dataScope: 0 as number,
  deptIds: [] as string[]
});

const permissionTreeData = ref<DataNode[]>([]);
const permissionCheckedKeys = ref<
  (number | string)[] | { checked: (number | string)[]; halfChecked: (number | string)[] }
>([]);
const permissionsCheckStrictly = ref(true);

const menuTreeData = ref<DataNode[]>([]);
const menuCheckedKeys = ref<
  (number | string)[] | { checked: (number | string)[]; halfChecked: (number | string)[] }
>([]);
const menusCheckStrictly = ref(true);

const departmentTreeData = ref<DataNode[]>([]);

const permissionSearchKeyword = ref("");
const menuSearchKeyword = ref("");

interface ExtendedTreeNode extends DataNode {
  nodeDescription?: string;
  nodePath?: string;
}

const renderPermissionTitle = (node: DataNode): VNode => {
  const ext = node as ExtendedTreeNode;
  const titleText = ext.title as string;
  if (ext.nodeDescription) {
    return h("span", { title: ext.nodeDescription, style: "cursor: default;" }, [
      h("span", {}, titleText),
      h(
        "span",
        {
          style:
            "display: block; font-size: 11px; color: var(--color-text-tertiary, #8c8c8c); line-height: 1.4; margin-top: 1px;"
        },
        ext.nodeDescription
      )
    ]);
  }
  return h("span", {}, titleText);
};

const renderMenuTitle = (node: DataNode): VNode => {
  const ext = node as ExtendedTreeNode;
  const titleText = ext.title as string;
  if (ext.nodePath) {
    return h("span", { style: "display: flex; align-items: center; gap: 6px;" }, [
      h("span", {}, titleText),
      h("span", { style: "font-size: 11px; color: var(--color-text-tertiary, #8c8c8c);" }, ext.nodePath)
    ]);
  }
  return h("span", {}, titleText);
};

const permissionSelectedCount = computed(() => {
  const keys = Array.isArray(permissionCheckedKeys.value)
    ? permissionCheckedKeys.value
    : (permissionCheckedKeys.value?.checked ?? []);
  return keys.filter((k): k is number => typeof k === "number").length;
});

const menuSelectedCount = computed(() => {
  const keys = Array.isArray(menuCheckedKeys.value)
    ? menuCheckedKeys.value
    : (menuCheckedKeys.value?.checked ?? []);
  return keys.filter((k): k is string => typeof k === "string").length;
});

const selectedDeptNames = computed(() => {
  if (assignModel.dataScope !== 2 || assignModel.deptIds.length === 0) return [];
  const findNames = (nodes: DataNode[]): string[] => {
    const names: string[] = [];
    for (const node of nodes) {
      if (assignModel.deptIds.includes(String(node.key))) {
        names.push(node.title as string);
      }
      if (node.children) names.push(...findNames(node.children as DataNode[]));
    }
    return names;
  };
  return findNames(departmentTreeData.value);
});

const dynamicTableOptions = ref<Array<{ label: string; value: string }>>([]);
const dynamicTableLoading = ref(false);
const fieldPermissionLoading = ref(false);
const fieldPermissionTableKey = ref<string>();
const existingFieldPermissions = ref<
  Array<{
    fieldName: string;
    roleCode: string;
    canView: boolean;
    canEdit: boolean;
  }>
>([]);
const fieldPermissionRows = ref<
  Array<{
    fieldName: string;
    label: string;
    canView: boolean;
    canEdit: boolean;
  }>
>([]);

const isAllViewChecked = computed(() => {
  if (fieldPermissionRows.value.length === 0) return false;
  return fieldPermissionRows.value.every((row) => row.canView);
});

const isAllEditChecked = computed(() => {
  if (fieldPermissionRows.value.length === 0) return false;
  return fieldPermissionRows.value.every((row) => row.canEdit);
});

const toggleAllView = (checked: boolean) => {
  fieldPermissionRows.value.forEach((row) => {
    row.canView = checked;
    if (!checked) row.canEdit = false;
  });
};

const toggleAllEdit = (checked: boolean) => {
  fieldPermissionRows.value.forEach((row) => {
    row.canEdit = checked;
    if (checked) row.canView = true;
  });
};

const filterNumericKeys = (keys: (number | string)[]) =>
  keys.filter((k): k is number => typeof k === "number" && !Number.isNaN(k));
const filterStringKeys = (keys: (number | string)[]) =>
  keys.filter((k): k is string => typeof k === "string" && k.trim().length > 0);

const collectTreeKeys = (nodes: DataNode[]): (number | string)[] => {
  let keys: (number | string)[] = [];
  for (const node of nodes) {
    keys.push(node.key as number | string);
    if (node.children && node.children.length > 0) {
      keys = keys.concat(collectTreeKeys(node.children));
    }
  }
  return keys;
};

const selectAllPermissions = () => {
  permissionCheckedKeys.value = collectTreeKeys(permissionTreeData.value);
};

const clearAllPermissions = () => {
  permissionCheckedKeys.value = [];
};

const selectAllMenus = () => {
  menuCheckedKeys.value = collectTreeKeys(menuTreeData.value);
};

const clearAllMenus = () => {
  menuCheckedKeys.value = [];
};

watch(permissionCheckedKeys, (val) => {
  const keys = Array.isArray(val) ? val : (val?.checked ?? []);
  assignModel.permissionIds = filterNumericKeys(keys);
});

watch(menuCheckedKeys, (val) => {
  if (Array.isArray(val)) {
    assignModel.menuIds = filterStringKeys(val);
  } else if (val && val.checked) {
    assignModel.menuIds = filterStringKeys(val.checked);
  }
});

watch(
  () => assignModel.permissionIds,
  (newVal) => {
    if (
      Array.isArray(newVal) &&
      (!Array.isArray(permissionCheckedKeys.value) ||
        newVal.join(",") !== (permissionCheckedKeys.value as number[]).join(","))
    ) {
      permissionCheckedKeys.value = [...newVal];
    }
  }
);

watch(
  () => assignModel.menuIds,
  (newVal) => {
    if (
      Array.isArray(newVal) &&
      (!Array.isArray(menuCheckedKeys.value) || newVal.join(",") !== (menuCheckedKeys.value as string[]).join(","))
    ) {
      menuCheckedKeys.value = [...newVal];
    }
  }
);

const loadPermissionOptions = async (keyword?: string) => {
  if (!isMounted.value) return;
  try {
    const result = await getPermissionsPaged({
      pageIndex: 1,
      pageSize: 200,
      keyword: keyword?.trim() || undefined
    });
    if (!isMounted.value) return;
    const rootNodes: Record<string, DataNode> = {};
    result.items.forEach((item: { id: string; code: string; name: string; description?: string }) => {
      const parts = item.code.split(":");
      const moduleCode = parts[0] || "默认";
      if (!rootNodes[moduleCode]) {
        rootNodes[moduleCode] = {
          key: `module_${moduleCode}`,
          title: moduleCode.toUpperCase(),
          children: []
        };
      }
      const nodeKey = Number(item.id);
      (rootNodes[moduleCode].children as ExtendedTreeNode[]).push({
        key: nodeKey,
        title: `${item.name} (${item.code})`,
        nodeDescription: item.description || undefined
      });
    });
    permissionTreeData.value = Object.values(rootNodes);
  } catch {
    message.warning(t("roleAssign.loadPermissionsFailed"));
  }
};

const loadMenuOptions = async (keyword?: string) => {
  if (!isMounted.value) return;
  try {
    const items = await getMenusAll();
    if (!isMounted.value) return;
    const formatted = items.map((item) => ({
      ...item,
      key: item.id,
      value: item.id,
      title: item.name,
      nodePath: item.path || undefined,
      id: item.id,
      parentId: item.parentId || "0"
    }));
    const keywordTrimmed = keyword?.trim().toLowerCase();
    const filtered = keywordTrimmed
      ? formatted.filter((f) => (f.title as string).toLowerCase().includes(keywordTrimmed))
      : formatted;
    menuTreeData.value = handleTree(filtered, "id", "parentId", "children");
  } catch (error) {
    const reason = error instanceof Error ? error.message : "";
    message.warning(reason || t("roleAssign.loadMenusFailed"));
  }
};

const handlePermissionSearch = debounce((value: string) => void loadPermissionOptions(value));
const handleMenuSearch = debounce((value: string) => void loadMenuOptions(value));

const handlePermissionSearchClear = (e: Event) => {
  const target = e.target as HTMLInputElement;
  if (!target.value) void loadPermissionOptions();
};

const handleMenuSearchClear = (e: Event) => {
  const target = e.target as HTMLInputElement;
  if (!target.value) void loadMenuOptions();
};

const loadDepartmentOptions = async (keyword?: string) => {
  if (!isMounted.value) return;
  try {
    const items = await getDepartmentsAll();
    if (!isMounted.value) return;
    const formatted = items.map((item) => ({
      ...item,
      key: item.id,
      value: item.id,
      title: item.name,
      id: item.id,
      parentId: item.parentId || "0"
    }));
    const keywordTrimmed = keyword?.trim().toLowerCase();
    const filtered = keywordTrimmed
      ? formatted.filter((f) => (f.title as string).toLowerCase().includes(keywordTrimmed))
      : formatted;
    departmentTreeData.value = handleTree(filtered, "id", "parentId", "children");
  } catch (error) {
    const reason = error instanceof Error ? error.message : "";
    message.warning(reason || t("roleAssign.loadDepartmentsFailed"));
  }
};

const loadDynamicTableOptions = async (search?: string) => {
  if (!isMounted.value) return;
  const appId = getCurrentProjectAppId();
  if (!appId) {
    dynamicTableOptions.value = [];
    return;
  }
  dynamicTableLoading.value = true;
  try {
    const result = await getAppScopedDynamicTables(appId, search?.trim() || undefined);
    if (!isMounted.value) return;
    dynamicTableOptions.value = result.map((item) => ({
      label: `${item.displayName} (${item.tableKey})`,
      value: item.tableKey
    }));
  } catch {
    message.warning(t("roleAssign.loadDynamicTablesFailed"));
  } finally {
    if (isMounted.value) {
      dynamicTableLoading.value = false;
    }
  }
};

const handleDynamicTableSearch = debounce((value: string) => void loadDynamicTableOptions(value));

const loadFieldPermissions = async (tableKey: string) => {
  if (!props.roleCode || !isMounted.value) return;

  fieldPermissionLoading.value = true;
  try {
    const [fields, permissions] = await Promise.all([
      getDynamicTableFields(tableKey),
      getDynamicFieldPermissions(tableKey)
    ]);
    if (!isMounted.value) return;
    existingFieldPermissions.value = permissions;
    fieldPermissionRows.value = fields.map((field) => {
      const current = permissions.find(
        (item) => item.roleCode === props.roleCode && item.fieldName === field.name
      );
      return {
        fieldName: field.name,
        label: field.displayName || field.name,
        canView: current?.canView ?? false,
        canEdit: current?.canEdit ?? false
      };
    });
  } catch (error) {
    if (isMounted.value) {
      message.error((error as Error).message || t("systemRoles.loadFieldDefinitionsFailed"));
    }
  } finally {
    if (isMounted.value) {
      fieldPermissionLoading.value = false;
    }
  }
};

const handleFieldPermissionTableChange = (value: string) => {
  if (!value) {
    fieldPermissionRows.value = [];
    existingFieldPermissions.value = [];
    return;
  }
  void loadFieldPermissions(value);
};

const handleFieldViewChange = (fieldName: string, value: boolean) => {
  const target = fieldPermissionRows.value.find((item) => item.fieldName === fieldName);
  if (!target) return;
  target.canView = value;
  if (!value) target.canEdit = false;
};

const handleFieldEditChange = (fieldName: string, value: boolean) => {
  const target = fieldPermissionRows.value.find((item) => item.fieldName === fieldName);
  if (!target) return;
  target.canEdit = value;
  if (value) target.canView = true;
};

const selectedDataScopeOption = computed(() => {
  return dataScopeOptions.find((item) => item.value === assignModel.dataScope) ?? dataScopeOptions[0];
});

const dataScopePreviewTitle = computed(() => t("systemRoles.dataScopePreviewTitle"));
const dataScopePreviewDescription = computed(() => {
  if (assignModel.dataScope === 2) {
    return t("systemRoles.dataScopePreviewCustomDept", { count: assignModel.deptIds.length });
  }
  return selectedDataScopeOption.value.description;
});

const fetchRoleDetail = async () => {
  if (!props.roleId || !isMounted.value) return;

  loading.value = true;
  fieldPermissionTableKey.value = undefined;
  fieldPermissionRows.value = [];
  existingFieldPermissions.value = [];

  try {
    await Promise.all([
      props.canAssignPermissions ? loadPermissionOptions() : Promise.resolve(),
      props.canAssignMenus ? loadMenuOptions() : Promise.resolve(),
      props.canAssignPermissions ? loadDynamicTableOptions() : Promise.resolve(),
      props.canManageDataScope ? loadDepartmentOptions() : Promise.resolve()
    ]);

    if (!isMounted.value) return;
    const detail = await getRoleDetail(props.roleId);
    if (!isMounted.value) return;
    assignModel.permissionIds = detail.permissionIds?.slice() ?? [];
    assignModel.menuIds = (detail.menuIds ?? []).map((value) => String(value));
    basicForm.code = props.roleCode;
    basicForm.name = props.roleName;
    basicForm.description = detail.description ?? "";
    basicFormSnapshot.value = {
      name: basicForm.name,
      description: basicForm.description
    };
    assignModel.dataScope = detail.dataScope === 1 ? 0 : (detail.dataScope ?? 0);
    assignModel.deptIds = (detail.deptIds ?? []).map((value) => String(value));
  } catch (error) {
    if (isMounted.value) {
      message.error((error as Error).message || t("systemRoles.loadRoleDetailFailed"));
    }
  } finally {
    if (isMounted.value) {
      loading.value = false;
    }
  }
};

const submitAssign = async () => {
  if (!props.roleId || !isMounted.value) return;

  submitting.value = true;
  try {
    const tasks: Promise<unknown>[] = [];

    if (props.canAssignPermissions) {
      tasks.push(updateRolePermissions(props.roleId, { permissionIds: assignModel.permissionIds }));
    }

    if (props.canAssignMenus) {
      tasks.push(updateRoleMenus(props.roleId, { menuIds: assignModel.menuIds as unknown as number[] }));
    }

    if (props.canManageDataScope) {
      tasks.push(
        setRoleDataScope(
          props.roleId,
          assignModel.dataScope,
          assignModel.dataScope === 2 ? (assignModel.deptIds as unknown as number[]) : undefined
        )
      );
    }

    if (fieldPermissionTableKey.value && props.roleCode) {
      const merged = existingFieldPermissions.value
        .filter((item) => item.roleCode !== props.roleCode)
        .concat(
          fieldPermissionRows.value
            .filter((item) => item.canView || item.canEdit)
            .map((item) => ({
              fieldName: item.fieldName,
              roleCode: props.roleCode,
              canView: item.canView,
              canEdit: item.canEdit
            }))
        );
      tasks.push(setDynamicFieldPermissions(fieldPermissionTableKey.value, { permissions: merged }));
    }

    await Promise.all(tasks);
    if (!isMounted.value) return;
    basicFormSnapshot.value = {
      name: basicForm.name,
      description: basicForm.description
    };
    message.success(`${t("systemRoles.assignSaveSuccess")} [role=${props.roleCode}]`);
    emit("success");
  } catch (error) {
    if (isMounted.value) {
      message.error((error as Error).message || t("systemRoles.assignSaveFailed"));
    }
  } finally {
    if (isMounted.value) {
      submitting.value = false;
    }
  }
};

watch(
  () => props.roleId,
  (newId) => {
    if (newId) {
      void fetchRoleDetail();
    }
  },
  { immediate: true }
);

watch(
  () => props.roleCode,
  (newCode) => {
    basicForm.code = newCode ?? "";
  }
);

watch(
  () => props.roleName,
  (newName) => {
    if (!basicInfoChanged.value) {
      basicForm.name = newName ?? "";
      basicFormSnapshot.value.name = newName ?? "";
    }
  }
);

watch(
  () => assignModel.dataScope,
  (scope) => {
    if (scope !== 2) {
      assignModel.deptIds = [];
    }
  }
);
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
