<template>
  <a-card title="菜单管理" class="page-card">
    <div class="crud-toolbar">
      <a-space wrap>
        <a-input
          v-model:value="keyword"
          placeholder="搜索菜单名称/路径"
          allow-clear
          @press-enter="handleSearch"
        />
        <a-button @click="handleSearch">查询</a-button>
        <a-button @click="handleReset">重置</a-button>
        <a-button v-if="canCreate" type="primary" @click="openCreate">新增菜单</a-button>
      </a-space>
      <a-space wrap>
        <TableViewToolbar :controller="tableViewController" />
        <a-button v-if="canUpdate" :disabled="!selectedRowKeys.length" @click="batchSetHidden(true)">
          批量隐藏
        </a-button>
        <a-button v-if="canUpdate" :disabled="!selectedRowKeys.length" @click="batchSetHidden(false)">
          批量显示
        </a-button>
      </a-space>
    </div>

    <div class="crud-filter-bar">
      <a-space wrap>
        <span class="crud-filter-label">高级筛选</span>
        <a-select
          v-model:value="hiddenFilter"
          :options="hiddenOptions"
          style="width: 160px"
          @change="handleSearch"
        />
      </a-space>
    </div>

    <a-row v-if="showTreeLayout" :gutter="16">
      <a-col :span="6">
        <a-card size="small" title="菜单树">
          <a-input
            v-model:value="treeKeyword"
            placeholder="搜索菜单"
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
              <a-tag v-if="record.isHidden" color="orange">隐藏</a-tag>
              <span v-else>-</span>
            </template>
            <template v-else-if="column.key === 'actions'">
              <a-space>
                <a-button v-if="canUpdate" type="link" @click="openEdit(record)">编辑</a-button>
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
          <a-tag v-if="record.isHidden" color="orange">隐藏</a-tag>
          <span v-else>-</span>
        </template>
        <template v-else-if="column.key === 'actions'">
          <a-space>
            <a-button v-if="canUpdate" type="link" @click="openEdit(record)">编辑</a-button>
          </a-space>
        </template>
      </template>
    </a-table>

    <a-drawer
      v-model:open="formVisible"
      :title="formMode === 'create' ? '新增菜单' : '编辑菜单'"
      placement="right"
      :width="560"
      @close="closeForm"
      destroy-on-close
    >
      <a-form ref="formRef" :model="formModel" :rules="formRules" layout="vertical">
        <a-form-item label="菜单名称" name="name">
          <a-input v-model:value="formModel.name" />
        </a-form-item>
        <a-form-item label="菜单路径" name="path">
          <a-input v-model:value="formModel.path" />
        </a-form-item>
        <a-form-item label="上级菜单" name="parentId">
          <a-select
            v-model:value="formModel.parentId"
            :options="parentOptions"
            allow-clear
            placeholder="无"
            show-search
            :filter-option="false"
            :loading="parentLoading"
            @search="handleParentSearch"
            @focus="() => loadParentOptions()"
          />
        </a-form-item>
        <a-form-item label="排序" name="sortOrder">
          <a-input-number v-model:value="formModel.sortOrder" :min="0" style="width: 100%" />
        </a-form-item>
        <a-form-item label="组件" name="component">
          <a-input v-model:value="formModel.component" />
        </a-form-item>
        <a-form-item label="图标" name="icon">
          <a-input v-model:value="formModel.icon" />
        </a-form-item>
        <a-form-item label="权限编码" name="permissionCode">
          <a-input v-model:value="formModel.permissionCode" />
        </a-form-item>
        <a-form-item label="隐藏菜单" name="isHidden">
          <a-switch v-model:checked="formModel.isHidden" />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-space>
          <a-button @click="closeForm">取消</a-button>
          <a-button type="primary" @click="submitForm">保存</a-button>
        </a-space>
      </template>
    </a-drawer>
  </a-card>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from "vue";
import type { TablePaginationConfig, FormInstance } from "ant-design-vue";
import type { Rule } from "ant-design-vue/es/form";
import { message } from "ant-design-vue";
import TableViewToolbar from "@/components/table/table-view-toolbar.vue";
import { useTableView } from "@/composables/useTableView";
import { createMenu, getMenusAll, getMenusPaged, updateMenu } from "@/services/api";
import type { MenuCreateRequest, MenuListItem, MenuUpdateRequest } from "@/types/api";
import { getAuthProfile, hasPermission } from "@/utils/auth";
import { debounce, type FormMode, type SelectOption } from "@/utils/common";

const baseColumns = [
  { title: "菜单名称", dataIndex: "name", key: "name" },
  { title: "路径", dataIndex: "path", key: "path" },
  { title: "上级菜单", key: "parent" },
  { title: "排序", dataIndex: "sortOrder", key: "sortOrder" },
  { title: "组件", dataIndex: "component", key: "component" },
  { title: "图标", dataIndex: "icon", key: "icon" },
  { title: "权限编码", dataIndex: "permissionCode", key: "permissionCode" },
  { title: "隐藏", key: "hidden" },
  { title: "操作", key: "actions", view: { canHide: false } }
];

const dataSource = ref<MenuListItem[]>([]);
const loading = ref(false);
const keyword = ref("");
const hiddenFilter = ref<"all" | "hidden" | "visible">("all");
const hiddenOptions = [
  { label: "全部", value: "all" },
  { label: "仅隐藏", value: "hidden" },
  { label: "仅显示", value: "visible" }
];
const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 10,
  total: 0,
  showTotal: (total) => `共 ${total} 条`
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
  component: "",
  icon: "",
  permissionCode: "",
  isHidden: false
});

const formRules: Record<string, Rule[]> = {
  name: [{ required: true, message: "请输入菜单名称" }],
  path: [{ required: true, message: "请输入菜单路径" }]
};

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
    const result = await getMenusPaged({
      pageIndex: pagination.current ?? 1,
      pageSize: pagination.pageSize ?? 10,
      keyword: keyword.value || undefined,
      isHidden
    });
    dataSource.value = result.items;
    pagination.total = result.total;
    selectedRowKeys.value = [];
    selectedRows.value = [];
  } catch (error) {
    message.error((error as Error).message || "查询失败");
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
    const result = await getMenusPaged({
      pageIndex: 1,
      pageSize: 20,
      keyword: kw?.trim() || undefined
    });
    parentOptions.value = result.items.map((item) => ({
      label: item.name,
      value: Number(item.id)
    }));
  } catch (error) {
    message.error((error as Error).message || "加载菜单失败");
  } finally {
    parentLoading.value = false;
  }
};

const ensureParentOption = (parentId?: number) => {
  if (!parentId) return;
  const exists = parentOptions.value.some((item) => item.value === parentId);
  if (!exists) {
    parentOptions.value = [{ label: `菜单ID ${parentId}`, value: parentId }, ...parentOptions.value];
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
    const list = await getMenusAll();
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
    message.error((error as Error).message || "加载菜单树失败");
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
    message.warning("请先选择菜单");
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
          component: item.component ?? undefined,
          icon: item.icon ?? undefined,
          permissionCode: item.permissionCode ?? undefined,
          isHidden
        })
      )
    );
    message.success(`已更新 ${selectedRows.value.length} 个菜单`);
    selectedRowKeys.value = [];
    selectedRows.value = [];
    await loadAllMenus();
    fetchData();
  } catch (error) {
    message.error((error as Error).message || "批量更新失败");
  }
};

const resetForm = () => {
  formModel.name = "";
  formModel.path = "";
  formModel.parentId = undefined;
  formModel.sortOrder = 0;
  formModel.component = "";
  formModel.icon = "";
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
  formModel.component = record.component ?? "";
  formModel.icon = record.icon ?? "";
  formModel.permissionCode = record.permissionCode ?? "";
  formModel.isHidden = record.isHidden;
  await loadParentOptions();
  ensureParentOption(formModel.parentId);
  formVisible.value = true;
};

const closeForm = () => {
  formVisible.value = false;
};

const submitForm = async () => {
  const valid = await formRef.value?.validate().catch(() => false);
  if (!valid) return;

  try {
    if (formMode.value === "create") {
      await createMenu({
        name: formModel.name,
        path: formModel.path,
        parentId: formModel.parentId ?? undefined,
        sortOrder: formModel.sortOrder,
        component: formModel.component || undefined,
        icon: formModel.icon || undefined,
        permissionCode: formModel.permissionCode || undefined,
        isHidden: formModel.isHidden
      });
      message.success("创建成功");
    } else if (selectedId.value) {
      await updateMenu(selectedId.value, {
        name: formModel.name,
        path: formModel.path,
        parentId: formModel.parentId ?? undefined,
        sortOrder: formModel.sortOrder,
        component: formModel.component || undefined,
        icon: formModel.icon || undefined,
        permissionCode: formModel.permissionCode || undefined,
        isHidden: formModel.isHidden
      });
      message.success("更新成功");
    }
    formVisible.value = false;
    await loadAllMenus();
    fetchData();
    loadParentOptions();
  } catch (error) {
    message.error((error as Error).message || "提交失败");
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
</style>
