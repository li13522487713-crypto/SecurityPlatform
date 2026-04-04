<template>
  <CrudPageLayout
    v-model:keyword="keyword"
    :title="t('systemDepartments.pageTitle')"
    :search-placeholder="t('systemDepartments.searchPlaceholder')"
    :drawer-open="formVisible"
    :drawer-title="formMode === 'create' ? t('systemDepartments.drawerCreateTitle') : t('systemDepartments.drawerEditTitle')"
    :drawer-width="520"
    @update:drawer-open="formVisible = $event"
    @search="handleSearch"
    @reset="handleReset"
    @close-form="closeForm"
    @submit="submitForm"
  >
    <template #toolbar-actions>
      <a-button v-if="canCreate" type="primary" @click="openCreate">{{ t("systemDepartments.addDepartment") }}</a-button>
    </template>

    <template #toolbar-right>
      <TableViewToolbar :controller="tableViewController" />
    </template>

    <template #table>
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
    </template>

    <template #form>
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
    </template>
  </CrudPageLayout>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, reactive, ref, watch } from "vue";
import type { TablePaginationConfig, FormInstance } from "ant-design-vue";
import type { Rule } from "ant-design-vue/es/form";
import { message } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import { CrudPageLayout, TableViewToolbar } from "@atlas/shared-ui";
import {
  useTableView,
  debounce,
  getAuthProfile,
  hasPermission,
  type FormMode,
  type SelectOption,
  type DepartmentCreateRequest,
  type DepartmentListItem,
  type DepartmentUpdateRequest
} from "@atlas/shared-core";
import {
  createDepartment,
  deleteDepartment,
  getDepartmentsAll,
  getDepartmentsPaged,
  updateDepartment
} from "@/services/api-users";
import { tableViewApi } from "@/services/api-table-views";

const { t } = useI18n();
const isMounted = ref(false);
const baseColumns = computed(() => [
  { title: t("systemDepartments.colDepartmentName"), dataIndex: "name", key: "name" },
  { title: t("systemDepartments.colDepartmentCode"), dataIndex: "code", key: "code" },
  { title: t("systemDepartments.colParentDepartment"), key: "parent" },
  { title: t("systemDepartments.colSortOrder"), dataIndex: "sortOrder", key: "sortOrder" },
  { title: t("systemDepartments.colActions"), key: "actions", view: { canHide: false } }
]);

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
      const parent = nodeMap.get(String(item.parentId));
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

const filterTree = (nodes: TreeNode[], keywordValue: string, parentMatched = false): TreeNode[] => {
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
const selectedTreeKeys = computed(() => (selectedParentId.value !== null ? [String(selectedParentId.value)] : []));
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
    const sel = String(selectedParentId.value);
    if (!isKeyInTree(newTree, sel)) {
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
    if (!isMounted.value) return;
    dataSource.value = result.items;
    pagination.total = Number(result.total) || 0;
  } catch (error) {
    if (!isMounted.value) return;
    message.error((error as Error).message || t("crud.queryFailed"));
  } finally {
    if (isMounted.value) {
      loading.value = false;
    }
  }
};

const refreshDepartmentData = async () => {
  await loadAllDepartments();
  await fetchData();
};

const { controller: tableViewController, tableColumns, tableSize } = useTableView<DepartmentListItem>({
  tableKey: "system.departments",
  columns: baseColumns,
  pagination,
  onRefresh: refreshDepartmentData,
  api: tableViewApi,
  translate: (key: string, params?: Record<string, unknown>) =>
    String(t(key, (params ?? {}) as Record<string, string | number | boolean>))
});

const loadParentOptions = async (kw?: string) => {
  parentLoading.value = true;
  try {
    const result = await getDepartmentsPaged({
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
    if (!isMounted.value) return;
    message.error((error as Error).message || t("systemDepartments.loadDepartmentsFailed"));
  } finally {
    if (isMounted.value) {
      parentLoading.value = false;
    }
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
    void fetchData();
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
    if (!isMounted.value) return;
    allDepartments.value = list;
    if (selectedParentId.value === null && list.length > 0) {
      const root = list.find((item) => !item.parentId);
      selectedParentId.value = root ? Number(root.id) : null;
    }
    pagination.total = filteredDepartments.value.length;
  } catch (error) {
    if (!isMounted.value) return;
    message.error((error as Error).message || t("systemDepartments.loadDepartmentTreeFailed"));
  } finally {
    if (isMounted.value) {
      treeLoading.value = false;
    }
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
  const source =
    currentParent === null
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
    void fetchData();
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
    void fetchData();
    void loadParentOptions();
  } catch (error) {
    message.error((error as Error).message || t("crud.submitFailed"));
  }
};

const handleDeleteDept = async (id: string) => {
  try {
    await deleteDepartment(id);
    message.success(t("crud.deleteSuccess"));
    await loadAllDepartments();
    void fetchData();
    void loadParentOptions();
  } catch (error) {
    message.error((error as Error).message || t("crud.deleteFailed"));
  }
};

onMounted(() => {
  isMounted.value = true;
  void loadParentOptions();
  void loadAllDepartments();
  void fetchData();
});

onUnmounted(() => {
  isMounted.value = false;
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
