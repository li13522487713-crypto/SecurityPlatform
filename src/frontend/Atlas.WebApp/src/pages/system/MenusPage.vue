```
<template>
  <CrudPageLayout
    :title="t('systemMenus.pageTitle')"
    v-model:keyword="keyword"
    :search-placeholder="t('systemMenus.searchPlaceholder')"
    :drawer-open="formVisible"
    :drawer-title="formMode === 'create' ? t('systemMenus.drawerCreateTitle') : t('systemMenus.drawerEditTitle')"
    :drawer-width="560"
    @update:drawer-open="formVisible = $event"
    @search="handleSearch"
    @reset="handleReset"
    @close-form="closeForm"
    @submit="submitForm"
  >
    <template #search-filters>
      <a-form-item :label="t('systemMenus.advancedFilter')">
        <a-select
          v-model:value="hiddenFilter"
          :options="hiddenOptions"
          style="width: 160px"
          @change="handleSearch"
        />
      </a-form-item>
    </template>

    <template #toolbar-actions>
      <a-button v-if="canCreate" type="primary" @click="openCreate">{{ t("systemMenus.addMenu") }}</a-button>
      <a-button v-if="canUpdate" :disabled="!selectedRowKeys.length" @click="batchSetHidden(true)">
        {{ t("systemMenus.batchHide") }}
      </a-button>
      <a-button v-if="canUpdate" :disabled="!selectedRowKeys.length" @click="batchSetHidden(false)">
        {{ t("systemMenus.batchShow") }}
      </a-button>
    </template>

    <template #toolbar-right>
      <TableViewToolbar :controller="tableViewController" />
    </template>

    <template #table>
      <a-row v-if="showTreeLayout" :gutter="16">
        <a-col :span="6">
          <a-card size="small" :title="t('systemMenus.treeTitle')">
            <a-input
              v-model:value="treeKeyword"
              :placeholder="t('systemMenus.treeSearchPlaceholder')"
              allow-clear
              size="small"
              style="margin-bottom: 12px"
            />
            <a-skeleton :loading="treeLoading" active>
              <a-tree
                :tree-data="treeData"
                :selected-keys="selectedTreeKeys"
                :expanded-keys="expandedTreeKeys"
                :auto-expand-parent="true"
                @select="handleTreeSelect"
              />
            </a-skeleton>
          </a-card>
        </a-col>
        <a-col :span="18">
          <a-table
            :columns="tableColumns"
            :data-source="tableData"
            :pagination="pagination"
            :loading="loading"
            :row-selection="rowSelection"
            :size="tableSize"
            row-key="id"
            @change="onTableChange"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'parent'">
                <span>{{ getParentName(record.parentId) }}</span>
              </template>
              <template v-else-if="column.key === 'hidden'">
                <a-tag v-if="record.isHidden" color="orange">{{ t("systemMenus.hiddenTag") }}</a-tag>
                <span v-else>-</span>
              </template>
              <template v-else-if="column.key === 'actions'">
                <a-space>
                  <a-button v-if="canUpdate" type="link" @click="openEdit(record)">{{ t("common.edit") }}</a-button>
                </a-space>
              </template>
            </template>
          </a-table>
        </a-col>
      </a-row>

      <a-table
        v-else
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
          <template v-if="column.key === 'parent'">
            <span>{{ getParentName(record.parentId) }}</span>
          </template>
          <template v-else-if="column.key === 'hidden'">
            <a-tag v-if="record.isHidden" color="orange">{{ t("systemMenus.hiddenTag") }}</a-tag>
            <span v-else>-</span>
          </template>
          <template v-else-if="column.key === 'actions'">
            <a-space>
              <a-button v-if="canUpdate" type="link" @click="openEdit(record)">{{ t("common.edit") }}</a-button>
            </a-space>
          </template>
        </template>
      </a-table>
    </template>

    <template #form>
      <a-form ref="formRef" :model="formModel" :rules="formRules" layout="vertical">
        <a-form-item :label="t('systemMenus.menuName')" name="name">
          <a-input v-model:value="formModel.name" />
        </a-form-item>
        <a-form-item :label="t('systemMenus.menuPath')" name="path">
          <a-input v-model:value="formModel.path" />
        </a-form-item>
        <a-form-item :label="t('systemMenus.parentMenu')" name="parentId">
          <a-select
            v-model:value="formModel.parentId"
            :options="parentOptions"
            allow-clear
            :placeholder="t('common.none')"
            show-search
            :filter-option="false"
            :loading="parentLoading"
            @search="handleParentSearch"
            @focus="() => loadParentOptions()"
          />
        </a-form-item>
        <a-form-item :label="t('systemMenus.sortOrder')" name="sortOrder">
          <a-input-number v-model:value="formModel.sortOrder" :min="0" style="width: 100%" />
        </a-form-item>
        <a-form-item :label="t('systemMenus.menuType')" name="menuType">
          <a-select
            v-model:value="formModel.menuType"
            :options="menuTypeOptions"
          />
        </a-form-item>
        <a-form-item :label="t('systemMenus.component')" name="component">
          <a-input v-model:value="formModel.component" />
        </a-form-item>
        <a-form-item :label="t('systemMenus.icon')" name="icon">
          <a-input v-model:value="formModel.icon" />
        </a-form-item>
        <a-form-item :label="t('systemMenus.permissionCode')" name="permissionCode">
          <a-input v-model:value="formModel.permissionCode" />
        </a-form-item>
        <a-form-item :label="t('systemMenus.perms')" name="perms">
          <a-input v-model:value="formModel.perms" />
        </a-form-item>
        <a-form-item :label="t('systemMenus.query')" name="query">
          <a-input v-model:value="formModel.query" />
        </a-form-item>
        <a-form-item :label="t('systemMenus.isFrame')" name="isFrame">
          <a-switch v-model:checked="formModel.isFrame" />
        </a-form-item>
        <a-form-item :label="t('systemMenus.isCache')" name="isCache">
          <a-switch v-model:checked="formModel.isCache" />
        </a-form-item>
        <a-form-item :label="t('systemMenus.visibility')" name="visible">
          <a-select v-model:value="formModel.visible" :options="visibilityOptions" />
        </a-form-item>
        <a-form-item :label="t('systemMenus.status')" name="status">
          <a-select v-model:value="formModel.status" :options="statusOptions" />
        </a-form-item>
        <a-form-item :label="t('systemMenus.hideMenu')" name="isHidden">
          <a-switch v-model:checked="formModel.isHidden" />
        </a-form-item>
      </a-form>
    </template>
  </CrudPageLayout>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch, onUnmounted } from "vue";

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import type { TablePaginationConfig, FormInstance } from "ant-design-vue";
import type { Rule } from "ant-design-vue/es/form";
import { message } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import CrudPageLayout from "@/components/crud/CrudPageLayout.vue";
import TableViewToolbar from "@/components/table/table-view-toolbar.vue";
import { useTableView } from "@/composables/useTableView";
import { createMenu, getMenusAll, getMenusPaged, updateMenu } from "@/services/api";
import type { MenuCreateRequest, MenuListItem, MenuUpdateRequest } from "@/types/api";
import { getAuthProfile, hasPermission } from "@/utils/auth";
import { debounce, type FormMode, type SelectOption } from "@/utils/common";

const { t } = useI18n();
const baseColumns = computed(() => ([
  { title: t("systemMenus.colMenuName"), dataIndex: "name", key: "name" },
  { title: t("systemMenus.colPath"), dataIndex: "path", key: "path" },
  { title: t("systemMenus.colParent"), key: "parent" },
  { title: t("systemMenus.colSortOrder"), dataIndex: "sortOrder", key: "sortOrder" },
  { title: t("systemMenus.colComponent"), dataIndex: "component", key: "component" },
  { title: t("systemMenus.colIcon"), dataIndex: "icon", key: "icon" },
  { title: t("systemMenus.colPermissionCode"), dataIndex: "permissionCode", key: "permissionCode" },
  { title: t("systemMenus.colHidden"), key: "hidden" },
  { title: t("systemMenus.colActions"), key: "actions", view: { canHide: false } }
]));

const dataSource = ref<MenuListItem[]>([]);
const loading = ref(false);
const keyword = ref("");
const hiddenFilter = ref<"all" | "hidden" | "visible">("all");
const hiddenOptions = computed(() => ([
  { label: t("systemMenus.hiddenFilterAll"), value: "all" },
  { label: t("systemMenus.hiddenFilterOnlyHidden"), value: "hidden" },
  { label: t("systemMenus.hiddenFilterOnlyVisible"), value: "visible" }
]));
const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 10,
  total: 0,
  showTotal: (total) => t("crud.totalItems", { total })
});
const treeKeyword = ref("");
const treeLoading = ref(false);
const allMenus = ref<MenuListItem[]>([]);
const selectedParentId = ref<number | null>(null);

const formVisible = ref(false);
const formMode = ref<FormMode>("create");
const formRef = ref<FormInstance>();
const formModel = reactive<MenuCreateRequest & MenuUpdateRequest>({
  name: "",
  path: "",
  parentId: undefined,
  sortOrder: 0,
  menuType: "C",
  component: "",
  icon: "",
  perms: "",
  query: "",
  isFrame: false,
  isCache: false,
  visible: "0",
  status: "0",
  permissionCode: "",
  isHidden: false
});

const formRules: Record<string, Rule[]> = {
  name: [{ required: true, message: t("systemMenus.menuNameRequired") }],
  path: [{ required: true, message: t("systemMenus.menuPathRequired") }]
};
const menuTypeOptions = computed(() => ([
  { label: t("systemMenus.menuTypeDirectory"), value: "M" },
  { label: t("systemMenus.menuTypeMenu"), value: "C" },
  { label: t("systemMenus.menuTypeButton"), value: "F" },
  { label: t("systemMenus.menuTypeLink"), value: "L" }
]));
const visibilityOptions = computed(() => ([
  { label: t("systemMenus.visible"), value: "0" },
  { label: t("systemMenus.hidden"), value: "1" }
]));
const statusOptions = computed(() => ([
  { label: t("systemMenus.statusNormal"), value: "0" },
  { label: t("systemMenus.statusDisabled"), value: "1" }
]));

const parentOptions = ref<SelectOption[]>([]);
const parentNameMap = ref<Map<number, string>>(new Map());
const parentLoading = ref(false);
const selectedId = ref<string | null>(null);
const profile = getAuthProfile();
const canCreate = hasPermission(profile, "menus:create");
const canUpdate = hasPermission(profile, "menus:update");
const canViewAll = hasPermission(profile, "menus:all");
const showTreeLayout = computed(() => canViewAll);
const selectedRowKeys = ref<string[]>([]);
const selectedRows = ref<MenuListItem[]>([]);

const rowSelection = computed(() => {
  if (!canUpdate) return undefined;
  return {
    selectedRowKeys: selectedRowKeys.value,
    onChange: (keys: (string | number)[], rows: MenuListItem[]) => {
      selectedRowKeys.value = keys.map((key) => key.toString());
      selectedRows.value = rows;
    }
  };
});

interface TreeNode {
  key: string;
  title: string;
  children?: TreeNode[];
}

const buildTree = (items: MenuListItem[]) => {
  const nodeMap = new Map<string, TreeNode>();
  const roots: TreeNode[] = [];

  items.forEach((item) => {
    nodeMap.set(item.id, { key: item.id, title: item.name, children: [] });
  });

  items.forEach((item) => {
    const node = nodeMap.get(item.id);
    if (!node) return;
    if (item.parentId) {
      const parent = nodeMap.get(item.parentId.toString());
      if (parent) {
        parent.children = parent.children ?? [];
        parent.children.push(node);
        return;
      }
    }
    roots.push(node);
  });

  const sortNodes = (nodes: TreeNode[]) => {
    nodes.sort((a, b) => a.title.localeCompare(b.title, "zh-Hans-CN"));
    nodes.forEach((child) => {
      if (child.children && child.children.length > 0) {
        sortNodes(child.children);
      }
    });
  };

  sortNodes(roots);
  return roots;
};

const filterTree = (nodes: TreeNode[], keywordValue: string): TreeNode[] => {
  if (!keywordValue.trim()) return nodes;
  const matcher = keywordValue.trim();
  const result: TreeNode[] = [];
  nodes.forEach((node) => {
    const children = node.children ? filterTree(node.children, matcher) : [];
    if (node.title.includes(matcher) || children.length > 0) {
      result.push({ ...node, children });
    }
  });
  return result;
};

const treeData = computed(() => filterTree(buildTree(allMenus.value), treeKeyword.value));
const selectedTreeKeys = computed(() => (selectedParentId.value ? [selectedParentId.value.toString()] : []));
const expandedTreeKeys = computed(() => {
  if (!treeKeyword.value.trim()) return [];
  return allMenus.value.map((item) => item.id);
});

const fetchData = async () => {
  if (showTreeLayout.value) return;
  loading.value = true;
  try {
    const isHidden = hiddenFilter.value === "all"
      ? undefined
      : hiddenFilter.value === "hidden";
    const result  = await getMenusPaged({
      pageIndex: pagination.current ?? 1,
      pageSize: pagination.pageSize ?? 10,
      keyword: keyword.value || undefined,
      isHidden
    });

    if (!isMounted.value) return;
    dataSource.value = result.items;
    pagination.total = result.total;
    selectedRowKeys.value = [];
    selectedRows.value = [];
  } catch (error) {
    message.error((error as Error).message || t("systemMenus.queryFailed"));
  } finally {
    loading.value = false;
  }
};

const { controller: tableViewController, tableColumns, tableSize } = useTableView<MenuListItem>({
  tableKey: "system.menus",
  columns: baseColumns,
  pagination,
  onRefresh: fetchData
});

const loadParentOptions = async (kw?: string) => {
  parentLoading.value = true;
  try {
    const result  = await getMenusPaged({
      pageIndex: 1,
      pageSize: 20,
      keyword: kw?.trim() || undefined
    });

    if (!isMounted.value) return;
    parentOptions.value = result.items.map((item) => ({
      label: item.name,
      value: Number(item.id)
    }));
  } catch (error) {
    message.error((error as Error).message || t("systemMenus.loadMenusFailed"));
  } finally {
    parentLoading.value = false;
  }
};

const ensureParentOption = (parentId?: number) => {
  if (!parentId) return;
  const exists = parentOptions.value.some((item) => item.value === parentId);
  if (!exists) {
    parentOptions.value = [{ label: t("systemMenus.menuIdLabel", { id: parentId }), value: parentId }, ...parentOptions.value];
  }
};

const handleParentSearch = debounce((value: string) => {
  void loadParentOptions(value);
});

const handleSearch = () => {
  selectedRowKeys.value = [];
  selectedRows.value = [];
  if (!showTreeLayout.value) {
    pagination.current = 1;
    fetchData();
    return;
  }
  pagination.current = 1;
};

const handleReset = () => {
  keyword.value = "";
  hiddenFilter.value = "all";
  handleSearch();
};

const loadAllMenus = async () => {
  if (!showTreeLayout.value) return;
  treeLoading.value = true;
  try {
    const list  = await getMenusAll();

    if (!isMounted.value) return;
    allMenus.value = list;
    parentNameMap.value = new Map(list.map((item) => [Number(item.id), item.name]));
    if (selectedParentId.value === null && list.length > 0) {
      const root = list.find((item) => !item.parentId);
      selectedParentId.value = root ? Number(root.id) : null;
    }
    pagination.total = filteredMenus.value.length;
    selectedRowKeys.value = [];
    selectedRows.value = [];
  } catch (error) {
    message.error((error as Error).message || t("systemMenus.loadTreeFailed"));
  } finally {
    treeLoading.value = false;
  }
};

const handleTreeSelect = (keys: (string | number)[]) => {
  if (!keys.length) {
    selectedParentId.value = null;
  } else {
    selectedParentId.value = Number(keys[0]);
  }
  pagination.current = 1;
};

const filteredMenus = computed(() => {
  if (!showTreeLayout.value) return dataSource.value;
  const currentParent = selectedParentId.value;
  const source = currentParent === null
    ? allMenus.value.filter((item) => !item.parentId)
    : allMenus.value.filter((item) => Number(item.parentId) === Number(currentParent));
  const hiddenMatched = source.filter((item) => {
    if (hiddenFilter.value === "hidden") return item.isHidden;
    if (hiddenFilter.value === "visible") return !item.isHidden;
    return true;
  });
  const trimmed = keyword.value.trim();
  if (!trimmed) return hiddenMatched;
  return hiddenMatched.filter((item) => item.name.includes(trimmed) || item.path.includes(trimmed));
});

const tableData = computed(() => {
  if (!showTreeLayout.value) return dataSource.value;
  const current = pagination.current ?? 1;
  const pageSize = pagination.pageSize ?? 10;
  const start = (current - 1) * pageSize;
  return filteredMenus.value.slice(start, start + pageSize);
});

const getParentName = (parentId?: number | null) => {
  if (!parentId) return "-";
  return parentNameMap.value.get(Number(parentId)) ?? "-";
};

const onTableChange = (pager: TablePaginationConfig) => {
  pagination.current = pager.current;
  pagination.pageSize = pager.pageSize;
  if (!showTreeLayout.value) {
    fetchData();
  }
};

const batchSetHidden = async (isHidden: boolean) => {
  if (!selectedRows.value.length) {
    message.warning(t("systemMenus.selectMenuWarning"));
    return;
  }
  try {
    await Promise.all(
      selectedRows.value.map((item) =>
        updateMenu(item.id, {
          name: item.name,
          path: item.path,
          parentId: item.parentId ?? undefined,
          sortOrder: item.sortOrder,
          menuType: item.menuType ?? "C",
          component: item.component ?? undefined,
          icon: item.icon ?? undefined,
          perms: item.perms ?? undefined,
          query: item.query ?? undefined,
          isFrame: item.isFrame ?? false,
          isCache: item.isCache ?? false,
          visible: item.visible ?? (isHidden ? "1" : "0"),
          status: item.status ?? "0",
          permissionCode: item.permissionCode ?? undefined,
          isHidden
        })
      )
    );

    if (!isMounted.value) return;
    message.success(t("systemMenus.batchUpdateSuccess", { count: selectedRows.value.length }));
    selectedRowKeys.value = [];
    selectedRows.value = [];
    await loadAllMenus();

    if (!isMounted.value) return;
    fetchData();
  } catch (error) {
    message.error((error as Error).message || t("systemMenus.batchUpdateFailed"));
  }
};

const resetForm = () => {
  formModel.name = "";
  formModel.path = "";
  formModel.parentId = undefined;
  formModel.sortOrder = 0;
  formModel.menuType = "C";
  formModel.component = "";
  formModel.icon = "";
  formModel.perms = "";
  formModel.query = "";
  formModel.isFrame = false;
  formModel.isCache = false;
  formModel.visible = "0";
  formModel.status = "0";
  formModel.permissionCode = "";
  formModel.isHidden = false;
};

const openCreate = () => {
  formMode.value = "create";
  selectedId.value = null;
  resetForm();
  void loadParentOptions();
  formVisible.value = true;
};

const openEdit = async (record: MenuListItem) => {
  formMode.value = "edit";
  selectedId.value = record.id;
  formModel.name = record.name;
  formModel.path = record.path;
  formModel.parentId = record.parentId ?? undefined;
  formModel.sortOrder = record.sortOrder;
  formModel.menuType = record.menuType ?? "C";
  formModel.component = record.component ?? "";
  formModel.icon = record.icon ?? "";
  formModel.perms = record.perms ?? "";
  formModel.query = record.query ?? "";
  formModel.isFrame = record.isFrame ?? false;
  formModel.isCache = record.isCache ?? false;
  formModel.visible = record.visible ?? (record.isHidden ? "1" : "0");
  formModel.status = record.status ?? "0";
  formModel.permissionCode = record.permissionCode ?? "";
  formModel.isHidden = record.isHidden;
  await loadParentOptions();

  if (!isMounted.value) return;
  ensureParentOption(formModel.parentId);
  formVisible.value = true;
};

const closeForm = () => {
  formVisible.value = false;
};

const submitForm = async () => {
  const valid  = await formRef.value?.validate().catch(() => false);

  if (!isMounted.value) return;
  if (!valid) return;

  try {
    if (formMode.value === "create") {
      await createMenu({
        name: formModel.name,
        path: formModel.path,
        parentId: formModel.parentId ?? undefined,
        sortOrder: formModel.sortOrder,
        menuType: formModel.menuType,
        component: formModel.component || undefined,
        icon: formModel.icon || undefined,
        perms: formModel.perms || undefined,
        query: formModel.query || undefined,
        isFrame: formModel.isFrame,
        isCache: formModel.isCache,
        visible: formModel.visible,
        status: formModel.status,
        permissionCode: formModel.permissionCode || undefined,
        isHidden: formModel.isHidden
      });

      if (!isMounted.value) return;
      message.success(t("crud.createSuccess"));
    } else if (selectedId.value) {
      await updateMenu(selectedId.value, {
        name: formModel.name,
        path: formModel.path,
        parentId: formModel.parentId ?? undefined,
        sortOrder: formModel.sortOrder,
        menuType: formModel.menuType,
        component: formModel.component || undefined,
        icon: formModel.icon || undefined,
        perms: formModel.perms || undefined,
        query: formModel.query || undefined,
        isFrame: formModel.isFrame,
        isCache: formModel.isCache,
        visible: formModel.visible,
        status: formModel.status,
        permissionCode: formModel.permissionCode || undefined,
        isHidden: formModel.isHidden
      });

      if (!isMounted.value) return;
      message.success(t("crud.updateSuccess"));
    }
    formVisible.value = false;
    await loadAllMenus();

    if (!isMounted.value) return;
    fetchData();
    loadParentOptions();
  } catch (error) {
    message.error((error as Error).message || t("crud.submitFailed"));
  }
};

onMounted(() => {
  loadParentOptions();
  loadAllMenus();
  fetchData();
});

watch(
  () => filteredMenus.value.length,
  (total) => {
    if (!showTreeLayout.value) return;
    pagination.total = total;
    const maxPage = Math.max(1, Math.ceil(total / (pagination.pageSize ?? 10)));
    if ((pagination.current ?? 1) > maxPage) {
      pagination.current = maxPage;
    }
  }
);
</script>

<style scoped>
/* The styles below are no longer needed as CrudPageLayout manages the layout */
/*
.crud-toolbar {
  margin-bottom: 12px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
}

.crud-filter-bar {
  margin-bottom: 16px;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  flex-wrap: wrap;
}

.crud-filter-label {
  color: var(--color-text-tertiary);
}
*/
</style>
```
