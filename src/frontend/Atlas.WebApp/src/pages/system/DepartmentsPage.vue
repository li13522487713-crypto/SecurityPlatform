<template>
  <a-card :title="t('systemDepartments.pageTitle')" class="page-card">
    <div class="crud-toolbar">
      <a-space wrap>
        <a-input
          v-model:value="keyword"
          :placeholder="t('systemDepartments.searchPlaceholder')"
          allow-clear
          @press-enter="handleSearch"
        />
        <a-button @click="handleSearch">{{ t("common.search") }}</a-button>
        <a-button @click="handleReset">{{ t("common.reset") }}</a-button>
        <a-button v-if="canCreate" type="primary" @click="openCreate">{{ t("systemDepartments.addDepartment") }}</a-button>
      </a-space>
      <TableViewToolbar :controller="tableViewController" />
    </div>

    <a-row v-if="showTreeLayout" :gutter="16">
      <a-col :span="6">
        <a-card size="small" :title="t('systemDepartments.treeTitle')">
          <a-input
            v-model:value="treeKeyword"
            :placeholder="t('systemDepartments.treeSearchPlaceholder')"
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
          :size="tableSize"
          :locale="{ emptyText: t('systemDepartments.emptyText') }"
          row-key="id"
          @change="onTableChange"
        >
          <template #bodyCell="{ column, record }">
            <template v-if="column.key === 'parent'">
              <span>{{ getParentName(record.parentId) }}</span>
            </template>
            <template v-else-if="column.key === 'actions'">
              <a-space>
                <a-button v-if="canUpdate" type="link" @click="openEdit(record)">{{ t("common.edit") }}</a-button>
                <a-popconfirm
                  v-if="canDelete"
                  :title="t('systemDepartments.deleteConfirm')"
                  :ok-text="t('common.delete')"
                  :cancel-text="t('common.cancel')"
                  @confirm="handleDeleteDept(record.id)"
                >
                  <a-button type="link" danger>{{ t("common.delete") }}</a-button>
                </a-popconfirm>
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
      :size="tableSize"
      :locale="{ emptyText: t('systemDepartments.emptyText') }"
      row-key="id"
      @change="onTableChange"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'parent'">
          <span>{{ getParentName(record.parentId) }}</span>
        </template>
        <template v-else-if="column.key === 'actions'">
          <a-space>
            <a-button v-if="canUpdate" type="link" @click="openEdit(record)">{{ t("common.edit") }}</a-button>
            <a-popconfirm
              v-if="canDelete"
              :title="t('systemDepartments.deleteConfirm')"
              :ok-text="t('common.delete')"
              :cancel-text="t('common.cancel')"
              @confirm="handleDeleteDept(record.id)"
            >
              <a-button type="link" danger>{{ t("common.delete") }}</a-button>
            </a-popconfirm>
          </a-space>
        </template>
      </template>
    </a-table>

    <a-drawer
      v-model:open="formVisible"
      :title="formMode === 'create' ? t('systemDepartments.drawerCreateTitle') : t('systemDepartments.drawerEditTitle')"
      placement="right"
      :width="520"
      destroy-on-close
      @close="closeForm"
    >
      <a-form ref="formRef" :model="formModel" :rules="formRules" layout="vertical">
        <a-form-item :label="t('systemDepartments.departmentName')" name="name">
          <a-input v-model:value="formModel.name" />
        </a-form-item>
        <a-form-item :label="t('systemDepartments.departmentCode')" name="code">
          <a-input v-model:value="formModel.code" />
        </a-form-item>
        <a-form-item :label="t('systemDepartments.parentDepartment')" name="parentId">
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
        <a-form-item :label="t('systemDepartments.sortOrder')" name="sortOrder">
          <a-input-number v-model:value="formModel.sortOrder" :min="0" style="width: 100%" />
        </a-form-item>
      </a-form>
      <template #footer>
        <a-space>
          <a-button @click="closeForm">{{ t("common.cancel") }}</a-button>
          <a-button type="primary" @click="submitForm">{{ t("common.save") }}</a-button>
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
import { useI18n } from "vue-i18n";
import TableViewToolbar from "@/components/table/table-view-toolbar.vue";
import { useTableView } from "@/composables/useTableView";
import { createDepartment, deleteDepartment, getDepartmentsAll, getDepartmentsPaged, updateDepartment } from "@/services/api";
import type { DepartmentCreateRequest, DepartmentListItem, DepartmentUpdateRequest } from "@/types/api";
import { getAuthProfile, hasPermission } from "@/utils/auth";
import { debounce, type FormMode, type SelectOption } from "@/utils/common";

const { t } = useI18n();
const baseColumns = computed(() => ([
  { title: t("systemDepartments.colDepartmentName"), dataIndex: "name", key: "name" },
  { title: t("systemDepartments.colDepartmentCode"), dataIndex: "code", key: "code" },
  { title: t("systemDepartments.colParentDepartment"), key: "parent" },
  { title: t("systemDepartments.colSortOrder"), dataIndex: "sortOrder", key: "sortOrder" },
  { title: t("systemDepartments.colActions"), key: "actions", view: { canHide: false } }
]));

const dataSource = ref<DepartmentListItem[]>([]);
const loading = ref(false);
const keyword = ref("");
const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 10,
  total: 0,
  showTotal: (total) => t("crud.totalItems", { total })
});
const treeKeyword = ref("");
const treeLoading = ref(false);
const allDepartments = ref<DepartmentListItem[]>([]);
const selectedParentId = ref<number | null>(null);

const formVisible = ref(false);
const formMode = ref<FormMode>("create");
const formRef = ref<FormInstance>();
const formModel = reactive<DepartmentCreateRequest & DepartmentUpdateRequest>({
  name: "",
  code: "",
  parentId: undefined,
  sortOrder: 0
});

const formRules: Record<string, Rule[]> = {
  name: [{ required: true, message: t("systemDepartments.nameRequired") }],
  code: [{ required: true, message: t("systemDepartments.codeRequired") }]
};

const parentOptions = ref<SelectOption[]>([]);
const parentLoading = ref(false);
const selectedId = ref<string | null>(null);
const profile = getAuthProfile();
const canCreate = hasPermission(profile, "departments:create");
const canUpdate = hasPermission(profile, "departments:update");
const canDelete = hasPermission(profile, "departments:delete");
const canViewAll = hasPermission(profile, "departments:all");
const showTreeLayout = computed(() => canViewAll);

interface TreeNode {
  key: string;
  title: string;
  children?: TreeNode[];
}

const buildTree = (items: DepartmentListItem[]) => {
  const nodeMap = new Map<string, TreeNode>();
  const rootNodes: TreeNode[] = [];

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
    rootNodes.push(node);
  });

  const sortNodes = (nodes: TreeNode[]) => {
    nodes.sort((a, b) => a.title.localeCompare(b.title, "zh-Hans-CN"));
    nodes.forEach((child) => {
      if (child.children && child.children.length > 0) {
        sortNodes(child.children);
      }
    });
  };

  sortNodes(rootNodes);
  return rootNodes;
};

const filterTree = (nodes: TreeNode[], keywordValue: string, parentMatched: boolean = false): TreeNode[] => {
  if (!keywordValue) return nodes;
  const matcher = keywordValue.trim().toLowerCase();
  if (!matcher) return nodes;
  const result: TreeNode[] = [];
  nodes.forEach((node) => {
    const isMatched = parentMatched || node.title.toLowerCase().includes(matcher);
    const children = node.children ? filterTree(node.children, keywordValue, isMatched) : [];
    if (isMatched || children.length > 0) {
      result.push({ ...node, children });
    }
  });
  return result;
};

const treeData = computed(() => filterTree(buildTree(allDepartments.value), treeKeyword.value));
const selectedTreeKeys = computed(() => (selectedParentId.value ? [selectedParentId.value.toString()] : []));
const expandedTreeKeys = computed(() => {
  if (!treeKeyword.value.trim()) return [];
  return allDepartments.value.map((item) => item.id);
});

const isKeyInTree = (nodes: TreeNode[], key: string): boolean => {
  for (const node of nodes) {
    if (node.key === key) return true;
    if (node.children && isKeyInTree(node.children, key)) return true;
  }
  return false;
};

watch(treeData, (newTree) => {
  if (selectedParentId.value !== null) {
    if (!isKeyInTree(newTree, selectedParentId.value.toString())) {
      selectedParentId.value = newTree.length > 0 ? Number(newTree[0].key) : null;
      pagination.current = 1;
    }
  }
});

const fetchData = async () => {
  if (showTreeLayout.value) return;
  loading.value = true;
  try {
    const result = await getDepartmentsPaged({
      pageIndex: pagination.current ?? 1,
      pageSize: pagination.pageSize ?? 10,
      keyword: keyword.value || undefined
    });
    dataSource.value = result.items;
    pagination.total = result.total;
  } catch (error) {
    message.error((error as Error).message || t("crud.queryFailed"));
  } finally {
    loading.value = false;
  }
};

const { controller: tableViewController, tableColumns, tableSize } = useTableView<DepartmentListItem>({
  tableKey: "system.departments",
  columns: baseColumns,
  pagination,
  onRefresh: fetchData
});

const loadParentOptions = async (kw?: string) => {
  parentLoading.value = true;
  try {
    const result = await getDepartmentsPaged({
      pageIndex: 1,
      pageSize: 20,
      keyword: kw?.trim() || undefined
    });
    parentOptions.value = result.items.map((item) => ({
      label: item.name,
      value: Number(item.id)
    }));
  } catch (error) {
    message.error((error as Error).message || t("systemDepartments.loadDepartmentsFailed"));
  } finally {
    parentLoading.value = false;
  }
};

const ensureParentOption = (parentId?: number) => {
  if (!parentId) return;
  const exists = parentOptions.value.some((item) => item.value === parentId);
  if (!exists) {
    const dept = allDepartments.value.find((d) => Number(d.id) === parentId);
    const label = dept?.name ?? t("systemDepartments.parentDepartmentFallback", { id: parentId });
    parentOptions.value = [{ label, value: parentId }, ...parentOptions.value];
  }
};

const handleParentSearch = debounce((value: string) => {
  void loadParentOptions(value);
});

const handleSearch = () => {
  if (!showTreeLayout.value) {
    pagination.current = 1;
    fetchData();
    return;
  }
  pagination.current = 1;
};

const handleReset = () => {
  keyword.value = "";
  handleSearch();
};

const loadAllDepartments = async () => {
  if (!showTreeLayout.value) return;
  treeLoading.value = true;
  try {
    const list = await getDepartmentsAll();
    allDepartments.value = list;
    if (selectedParentId.value === null && list.length > 0) {
      const root = list.find((item) => !item.parentId);
      selectedParentId.value = root ? Number(root.id) : null;
    }
    pagination.total = filteredDepartments.value.length;
  } catch (error) {
    message.error((error as Error).message || t("systemDepartments.loadDepartmentTreeFailed"));
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

const getParentName = (parentId?: number | null) => {
  if (!parentId) return "-";
  const target = allDepartments.value.find((item) => Number(item.id) === Number(parentId));
  return target?.name ?? "-";
};

const filteredDepartments = computed(() => {
  if (!showTreeLayout.value) return dataSource.value;
  const currentParent = selectedParentId.value;
  const source = currentParent === null
    ? allDepartments.value.filter((item) => !item.parentId)
    : allDepartments.value.filter((item) => Number(item.parentId) === Number(currentParent));

  const trimmed = keyword.value.trim();
  if (!trimmed) return source;
  return source.filter((item) => item.name.includes(trimmed));
});

const tableData = computed(() => {
  if (!showTreeLayout.value) return dataSource.value;
  const current = pagination.current ?? 1;
  const pageSize = pagination.pageSize ?? 10;
  const start = (current - 1) * pageSize;
  return filteredDepartments.value.slice(start, start + pageSize);
});

const onTableChange = (pager: TablePaginationConfig) => {
  pagination.current = pager.current;
  pagination.pageSize = pager.pageSize;
  if (!showTreeLayout.value) {
    fetchData();
  }
};

const resetForm = () => {
  formModel.name = "";
  formModel.code = "";
  formModel.parentId = undefined;
  formModel.sortOrder = 0;
};

const openCreate = () => {
  formMode.value = "create";
  selectedId.value = null;
  resetForm();
  void loadParentOptions();
  formVisible.value = true;
};

const openEdit = async (record: DepartmentListItem) => {
  formMode.value = "edit";
  selectedId.value = record.id;
  formModel.name = record.name;
  formModel.code = record.code;
  formModel.parentId = record.parentId ?? undefined;
  formModel.sortOrder = record.sortOrder;
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
      await createDepartment({
        name: formModel.name,
        code: formModel.code,
        parentId: formModel.parentId ?? undefined,
        sortOrder: formModel.sortOrder
      });
      message.success(t("crud.createSuccess"));
    } else if (selectedId.value) {
      await updateDepartment(selectedId.value, {
        name: formModel.name,
        code: formModel.code,
        parentId: formModel.parentId ?? undefined,
        sortOrder: formModel.sortOrder
      });
      message.success(t("crud.updateSuccess"));
    }
    formVisible.value = false;
    await loadAllDepartments();
    fetchData();
    loadParentOptions();
  } catch (error) {
    message.error((error as Error).message || t("crud.submitFailed"));
  }
};

const handleDeleteDept = async (id: string) => {
  try {
    await deleteDepartment(id);
    message.success(t("crud.deleteSuccess"));
    await loadAllDepartments();
    fetchData();
    loadParentOptions();
  } catch (error) {
    message.error((error as Error).message || t("crud.deleteFailed"));
  }
};

onMounted(() => {
  loadParentOptions();
  loadAllDepartments();
  fetchData();
});

watch(
  () => filteredDepartments.value.length,
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
</style>
