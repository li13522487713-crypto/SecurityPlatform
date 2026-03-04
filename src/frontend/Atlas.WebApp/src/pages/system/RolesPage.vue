<template>
  <CrudPageLayout
    v-model:keyword="keyword"
    title="角色管理"
    search-placeholder="搜索角色名称/编码"
    :drawer-open="formVisible"
    :drawer-title="formMode === 'create' ? '新增角色' : '编辑角色'"
    :drawer-width="520"
    @update:drawer-open="formVisible = $event"
    @search="handleSearch"
    @reset="handleReset"
    @close-form="closeForm"
    @submit="submitForm"
  >
    <template #toolbar-actions>
      <a-button v-if="canCreate" type="primary" @click="openCreate">新增角色</a-button>
    </template>
    <template #toolbar-right>
      <TableViewToolbar :controller="tableViewController" />
      <a-popconfirm
        v-if="canDelete"
        title="确认批量删除所选角色？"
        ok-text="删除"
        cancel-text="取消"
        @confirm="handleBatchDelete"
      >
        <a-button danger :disabled="!selectedRowKeys.length">批量删除</a-button>
      </a-popconfirm>
    </template>

    <template #filter>
      <a-select
        v-model:value="systemFilter"
        :options="systemOptions"
        style="width: 160px"
        @change="handleSearch"
      />
    </template>

    <template #table>
      <a-table
        :columns="tableColumns"
        :data-source="dataSource"
        :pagination="pagination"
        :loading="loading"
        :row-selection="rowSelection"
        :size="tableSize"
        row-key="id"
        @change="onTableChange"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'isSystem'">
            <a-tag v-if="record.isSystem" color="blue">系统</a-tag>
            <span v-else>-</span>
          </template>
          <template v-if="column.key === 'actions'">
            <a-space>
              <a-button v-if="canUpdate" type="link" @click="openEdit(record)">编辑</a-button>
              <a-button
                v-if="canAssignPermissions || canAssignMenus"
                type="link"
                @click="openAssign(record)"
              >
                权限
              </a-button>
              <a-popconfirm
                v-if="canDelete"
                title="确认删除该角色？"
                ok-text="删除"
                cancel-text="取消"
                @confirm="handleDelete(record.id)"
              >
                <a-button type="link" danger>删除</a-button>
              </a-popconfirm>
            </a-space>
          </template>
        </template>
      </a-table>
    </template>

    <template #form>
      <a-form ref="formRef" :model="formModel" :rules="formRules" layout="vertical">
        <a-form-item label="角色名称" name="name">
          <a-input v-model:value="formModel.name" />
        </a-form-item>
        <a-form-item label="角色编码" name="code">
          <a-input v-model:value="formModel.code" :disabled="formMode === 'edit'" />
        </a-form-item>
        <a-form-item label="描述" name="description">
          <a-input v-model:value="formModel.description" />
        </a-form-item>
      </a-form>
    </template>

    <template #extra-drawers>
      <a-drawer
        v-model:open="assignVisible"
        title="角色权限配置"
        placement="right"
        :width="640"
        destroy-on-close
        @close="closeAssign"
      >
        <a-tabs v-model:active-key="assignTab">
          <a-tab-pane v-if="canAssignPermissions" key="permissions" tab="权限分配">
            <a-select
              v-model:value="assignModel.permissionIds"
              mode="multiple"
              style="width: 100%"
              placeholder="选择权限"
              :options="permissionOptions"
              :loading="permissionLoading"
              :filter-option="false"
              show-search
              @search="handlePermissionSearch"
              @focus="() => loadPermissionOptions()"
            />
          </a-tab-pane>
          <a-tab-pane v-if="canAssignMenus" key="menus" tab="菜单分配">
            <a-select
              v-model:value="assignModel.menuIds"
              mode="multiple"
              style="width: 100%"
              placeholder="选择菜单"
              :options="menuOptions"
              :loading="menuLoading"
              :filter-option="false"
              show-search
              @search="handleMenuSearch"
              @focus="() => loadMenuOptions()"
            />
          </a-tab-pane>
          <a-tab-pane v-if="canAssignPermissions" key="field-permissions" tab="字段权限">
            <a-alert
              message="字段权限用于控制角色对动态表字段的可见/可编辑能力。"
              type="info"
              show-icon
              style="margin-bottom: 12px"
            />
            <a-select
              v-model:value="fieldPermissionTableKey"
              style="width: 100%; margin-bottom: 12px"
              placeholder="选择动态表（默认展示20条，可搜索）"
              :options="dynamicTableOptions"
              :loading="dynamicTableLoading"
              :filter-option="false"
              show-search
              @search="handleDynamicTableSearch"
              @change="handleFieldPermissionTableChange"
              @focus="() => loadDynamicTableOptions()"
            />
            <a-table
              :data-source="fieldPermissionRows"
              :loading="fieldPermissionLoading"
              :pagination="false"
              row-key="fieldName"
              size="small"
            >
              <a-table-column key="label" title="字段" data-index="label" />
              <a-table-column key="canView" title="可见" width="100">
                <template #default="{ record }">
                  <a-switch
                    :checked="record.canView"
                    size="small"
                    @change="(value: boolean) => handleFieldViewChange(record.fieldName, value)"
                  />
                </template>
              </a-table-column>
              <a-table-column key="canEdit" title="可编辑" width="100">
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
          </a-tab-pane>
          <a-tab-pane key="data-scope" tab="数据权限">
            <a-alert
              message="数据权限控制该角色可查看的数据范围（等保2.0 最小化授权原则）"
              type="info"
              show-icon
              style="margin-bottom: 16px"
            />
            <a-radio-group v-model:value="assignModel.dataScope" button-style="solid">
              <a-radio-button
                v-for="scope in dataScopeOptions"
                :key="scope.value"
                :value="scope.value"
              >
                {{ scope.label }}
              </a-radio-button>
            </a-radio-group>
            <div style="margin-top: 12px; color: #888; font-size: 12px;">
              <p v-for="scope in dataScopeOptions" :key="`desc-${scope.value}`">
                • <b>{{ scope.label }}</b>：{{ scope.description }}
              </p>
            </div>
          </a-tab-pane>
        </a-tabs>
        <template #footer>
          <a-space>
            <a-button @click="closeAssign">取消</a-button>
            <a-button type="primary" @click="submitAssign">保存</a-button>
          </a-space>
        </template>
      </a-drawer>
    </template>
  </CrudPageLayout>
</template>

<script setup lang="ts">
import { computed, reactive, ref } from "vue";
import type { FormInstance } from "ant-design-vue";
import { message } from "ant-design-vue";
import CrudPageLayout from "@/components/crud/CrudPageLayout.vue";
import TableViewToolbar from "@/components/table/table-view-toolbar.vue";
import { useCrudPage } from "@/composables/useCrudPage";
import {
  createRole,
  deleteRole,
  getRoleDetail,
  getRolesPaged,
  updateRole,
  updateRolePermissions,
  updateRoleMenus,
  setRoleDataScope,
  getPermissionsPaged,
  getMenusPaged
} from "@/services/api";
import {
  getDynamicTablesPaged,
  getDynamicTableFields,
  getDynamicFieldPermissions,
  setDynamicFieldPermissions
} from "@/services/dynamic-tables";
import type { RoleListItem, RoleDetail, RoleCreateRequest, RoleUpdateRequest } from "@/types/api";
import { debounce, type SelectOption } from "@/utils/common";

const systemFilter = ref<"all" | "system" | "custom">("all");
const systemOptions = [
  { label: "全部角色", value: "all" },
  { label: "系统内置", value: "system" },
  { label: "自定义", value: "custom" }
];

const selectedRowKeys = ref<string[]>([]);

const rowSelection = computed(() => {
  if (!crud.canDelete) return undefined;
  return {
    selectedRowKeys: selectedRowKeys.value,
    onChange: (keys: (string | number)[]) => {
      selectedRowKeys.value = keys.map((key) => key.toString());
    }
  };
});

const formRef = ref<FormInstance>();

const crud = useCrudPage<RoleListItem, RoleDetail, RoleCreateRequest, RoleUpdateRequest>({
  tableKey: "system.roles",
  columns: [
    { title: "角色名称", dataIndex: "name", key: "name" },
    { title: "角色编码", dataIndex: "code", key: "code" },
    { title: "描述", dataIndex: "description", key: "description" },
    { title: "系统内置", dataIndex: "isSystem", key: "isSystem" },
    { title: "操作", key: "actions", view: { canHide: false } }
  ],
  permissions: {
    create: "roles:create",
    update: "roles:update",
    delete: "roles:delete",
    assignPermissions: "roles:assign-permissions",
    assignMenus: "roles:assign-menus"
  },
  api: {
    list: getRolesPaged,
    detail: getRoleDetail,
    create: createRole,
    update: updateRole,
    delete: deleteRole
  },
  formRef,
  defaultFormModel: () => ({
    name: "",
    code: "",
    description: ""
  }),
  formRules: {
    name: [{ required: true, message: "请输入角色名称" }],
    code: [{ required: true, message: "请输入角色编码" }]
  },
  buildListParams: (base) => ({
    ...base,
    isSystem: systemFilter.value === "all" ? undefined : systemFilter.value === "system"
  }),
  buildCreatePayload: (model) => ({
    name: model.name,
    code: model.code,
    description: model.description || undefined
  }),
  buildUpdatePayload: (model) => ({
    name: model.name,
    description: model.description || undefined
  }),
  mapRecordToForm: (record, model) => {
    model.name = record.name;
    model.code = record.code;
    model.description = record.description ?? "";
  }
});

const {
  dataSource, loading, keyword, pagination,
  formVisible, formMode, formModel, formRules,
  tableViewController, tableColumns, tableSize,
  canCreate, canUpdate, canDelete,
  onTableChange, openCreate, openEdit, closeForm, submitForm, handleDelete
} = crud;

const canAssignPermissions = crud.hasPermissionFor("assignPermissions");
const canAssignMenus = crud.hasPermissionFor("assignMenus");
const dataScopeOptions = [
  { value: 1, label: "全部数据", description: "可查看当前租户内全部数据（默认）" },
  { value: 2, label: "自定义部门", description: "仅可查看指定部门数据（后续按部门配置）" },
  { value: 3, label: "本部门", description: "仅可查看本人所在部门的数据" },
  { value: 4, label: "本部门及下级", description: "可查看本部门及所有下级部门的数据" },
  { value: 5, label: "仅本人", description: "仅可查看本人创建或归属的数据" },
  { value: 6, label: "项目维度", description: "仅可查看当前项目范围内的数据" }
];

// --- Assignment Drawer ---
const assignVisible = ref(false);
const assignTab = ref("permissions");
const assignRoleId = ref<string | null>(null);
const assignRoleCode = ref<string>("");
const assignModel = reactive({
  permissionIds: [] as number[],
  menuIds: [] as number[],
  dataScope: 1 as number
});

const permissionOptions = ref<SelectOption[]>([]);
const menuOptions = ref<SelectOption[]>([]);
const permissionLoading = ref(false);
const menuLoading = ref(false);
const dynamicTableOptions = ref<Array<{ label: string; value: string }>>([]);
const dynamicTableLoading = ref(false);
const fieldPermissionLoading = ref(false);
const fieldPermissionTableKey = ref<string>();
const existingFieldPermissions = ref<Array<{
  fieldName: string;
  roleCode: string;
  canView: boolean;
  canEdit: boolean;
}>>([]);
const fieldPermissionRows = ref<Array<{
  fieldName: string;
  label: string;
  canView: boolean;
  canEdit: boolean;
}>>([]);

const loadPermissionOptions = async (keyword?: string) => {
  permissionLoading.value = true;
  try {
    const result = await getPermissionsPaged({
      pageIndex: 1,
      pageSize: 50,
      keyword: keyword?.trim() || undefined
    });
    permissionOptions.value = result.items.map((item) => ({
      label: `${item.name} (${item.code})`,
      value: Number(item.id)
    }));
  } catch (error) {
    message.error((error as Error).message || "加载权限失败");
  } finally {
    permissionLoading.value = false;
  }
};

const loadMenuOptions = async (keyword?: string) => {
  menuLoading.value = true;
  try {
    const result = await getMenusPaged({
      pageIndex: 1,
      pageSize: 50,
      keyword: keyword?.trim() || undefined
    });
    menuOptions.value = result.items.map((item) => ({
      label: `${item.name} (${item.path})`,
      value: Number(item.id)
    }));
  } catch (error) {
    message.error((error as Error).message || "加载菜单失败");
  } finally {
    menuLoading.value = false;
  }
};

const handlePermissionSearch = debounce((value: string) => {
  void loadPermissionOptions(value);
});

const handleMenuSearch = debounce((value: string) => {
  void loadMenuOptions(value);
});

const loadDynamicTableOptions = async (search?: string) => {
  dynamicTableLoading.value = true;
  try {
    const result = await getDynamicTablesPaged({
      pageIndex: 1,
      pageSize: 20,
      keyword: search?.trim() || undefined
    });
    dynamicTableOptions.value = result.items.map((item) => ({
      label: `${item.displayName} (${item.tableKey})`,
      value: item.tableKey
    }));
  } catch (error) {
    message.error((error as Error).message || "加载动态表失败");
  } finally {
    dynamicTableLoading.value = false;
  }
};

const handleDynamicTableSearch = debounce((value: string) => {
  void loadDynamicTableOptions(value);
});

const loadFieldPermissions = async (tableKey: string) => {
  if (!assignRoleCode.value) {
    return;
  }

  fieldPermissionLoading.value = true;
  try {
    const [fields, permissions] = await Promise.all([
      getDynamicTableFields(tableKey),
      getDynamicFieldPermissions(tableKey)
    ]);
    existingFieldPermissions.value = permissions;
    fieldPermissionRows.value = fields.map((field) => {
      const current = permissions.find((item) =>
        item.roleCode === assignRoleCode.value && item.fieldName === field.name
      );
      return {
        fieldName: field.name,
        label: field.displayName || field.name,
        canView: current?.canView ?? false,
        canEdit: current?.canEdit ?? false
      };
    });
  } catch (error) {
    message.error((error as Error).message || "加载字段权限失败");
  } finally {
    fieldPermissionLoading.value = false;
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
  if (!value) {
    target.canEdit = false;
  }
};

const handleFieldEditChange = (fieldName: string, value: boolean) => {
  const target = fieldPermissionRows.value.find((item) => item.fieldName === fieldName);
  if (!target) return;
  target.canEdit = value;
  if (value) {
    target.canView = true;
  }
};

const openAssign = async (record: RoleListItem) => {
  assignRoleId.value = record.id;
  assignRoleCode.value = record.code;
  assignTab.value = canAssignPermissions ? "permissions" : "menus";
  assignVisible.value = true;

  await Promise.all([
    canAssignPermissions ? loadPermissionOptions() : Promise.resolve(),
    canAssignMenus ? loadMenuOptions() : Promise.resolve(),
    canAssignPermissions ? loadDynamicTableOptions() : Promise.resolve()
  ]);

  try {
    const detail = await getRoleDetail(record.id);
    assignModel.permissionIds = detail.permissionIds?.slice() ?? [];
    assignModel.menuIds = detail.menuIds?.slice() ?? [];
    assignModel.dataScope = detail.dataScope ?? 1;
  } catch (error) {
    message.error((error as Error).message || "加载角色详情失败");
  }
};

const closeAssign = () => {
  assignVisible.value = false;
  assignRoleCode.value = "";
  fieldPermissionTableKey.value = undefined;
  fieldPermissionRows.value = [];
  existingFieldPermissions.value = [];
};

const submitAssign = async () => {
  if (!assignRoleId.value) return;
  try {
    const tasks: Promise<unknown>[] = [];
    if (canAssignPermissions) {
      tasks.push(updateRolePermissions(assignRoleId.value, { permissionIds: assignModel.permissionIds }));
    }
    if (canAssignMenus) {
      tasks.push(updateRoleMenus(assignRoleId.value, { menuIds: assignModel.menuIds }));
    }
    // 保存数据权限
    tasks.push(setRoleDataScope(assignRoleId.value, assignModel.dataScope));
    if (fieldPermissionTableKey.value && assignRoleCode.value) {
      const merged = existingFieldPermissions.value
        .filter((item) => item.roleCode !== assignRoleCode.value)
        .concat(
          fieldPermissionRows.value
            .filter((item) => item.canView || item.canEdit)
            .map((item) => ({
              fieldName: item.fieldName,
              roleCode: assignRoleCode.value,
              canView: item.canView,
              canEdit: item.canEdit
            }))
        );
      tasks.push(setDynamicFieldPermissions(fieldPermissionTableKey.value, { permissions: merged }));
    }
    await Promise.all(tasks);
    message.success("权限配置已更新");
    assignVisible.value = false;
  } catch (error) {
    message.error((error as Error).message || "更新权限配置失败");
  }
};

// --- Search/Reset ---
const handleSearch = () => {
  selectedRowKeys.value = [];
  crud.handleSearch();
};

const handleReset = () => {
  systemFilter.value = "all";
  selectedRowKeys.value = [];
  crud.keyword.value = "";
  crud.handleSearch();
};

const handleBatchDelete = async () => {
  if (!selectedRowKeys.value.length) {
    message.warning("请先选择角色");
    return;
  }
  try {
    await Promise.all(selectedRowKeys.value.map((id) => deleteRole(id)));
    message.success(`已删除 ${selectedRowKeys.value.length} 个角色`);
    selectedRowKeys.value = [];
    crud.fetchData();
  } catch (error) {
    message.error((error as Error).message || "批量删除失败");
  }
};
</script>
