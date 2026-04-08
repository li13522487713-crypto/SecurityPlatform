<template>
  <div class="app-departments-page-root">
    <CrudPageLayout
      data-testid="app-departments-page"
      :title="t('systemDepartments.pageTitle')"
      :subtitle="t('systemDepartments.pageSubtitle')"
      :drawer-open="formVisible"
      :drawer-title="formMode === 'create' ? t('systemDepartments.drawerCreateTitle') : t('systemDepartments.drawerEditTitle')"
      :drawer-width="520"
      @update:drawer-open="formVisible = $event"
      @close-form="closeForm"
      @submit="submitForm"
    >
      <template v-if="canCreate" #card-extra>
        <a-button type="primary" data-testid="app-departments-create" @click="openCreate">
          {{ t("systemDepartments.addDepartment") }}
        </a-button>
      </template>

      <template #table>
        <a-card class="app-departments-inner-card" :bordered="true">
          <div class="app-departments-inner-toolbar">
            <a-input
              v-model:value="tableKeyword"
              :placeholder="t('systemDepartments.searchPlaceholder')"
              allow-clear
              class="app-departments-search-input"
              data-testid="app-departments-table-search"
            />
            <a-button type="default" class="app-departments-expand-btn" data-testid="app-departments-toggle-expand" @click="toggleExpandAll">
              {{ expandToggleLabel }}
            </a-button>
          </div>
          <a-table
            data-testid="app-departments-table"
            :columns="tableColumns"
            :data-source="displayTree"
            :loading="loading"
            :pagination="false"
            :locale="{ emptyText: t('systemDepartments.emptyText') }"
            row-key="id"
            children-column-name="children"
            :expandable="tableExpandable"
          >
            <template #bodyCell="{ column, record }">
              <template v-if="column.key === 'name'">
                <a-space :size="8" align="center">
                  <FolderOutlined class="app-departments-folder-icon" />
                  <span class="app-departments-name-text">{{ record.name }}</span>
                </a-space>
              </template>
              <template v-else-if="column.key === 'code'">
                <a-tag color="default" class="app-departments-code-tag">{{ record.code }}</a-tag>
              </template>
              <template v-else-if="column.key === 'manager'">
                <span class="app-departments-placeholder-cell">{{ placeholderDash }}</span>
              </template>
              <template v-else-if="column.key === 'memberCount'">
                <span class="app-departments-placeholder-cell">{{ placeholderDash }}</span>
              </template>
              <template v-else-if="column.key === 'actions'">
                <a-space>
                  <a-button v-if="canUpdate" type="link" :data-testid="`app-departments-edit-${record.id}`" @click="openEdit(record)">
                    {{ t("common.edit") }}
                  </a-button>
                  <a-popconfirm
                    v-if="canDelete"
                    :title="t('systemDepartments.deleteConfirm')"
                    :ok-text="t('common.delete')"
                    :cancel-text="t('common.cancel')"
                    @confirm="handleDeleteDept(record.id)"
                  >
                    <a-button type="link" danger :data-testid="`app-departments-delete-${record.id}`">{{ t("common.delete") }}</a-button>
                  </a-popconfirm>
                </a-space>
              </template>
            </template>
          </a-table>
        </a-card>
      </template>

      <template #form>
        <a-form ref="formRef" :model="formModel" :rules="formRules" layout="vertical">
          <a-form-item :label="t('systemDepartments.departmentName')" name="name">
            <a-input v-model:value="formModel.name" data-testid="app-departments-form-name" />
          </a-form-item>
          <a-form-item :label="t('systemDepartments.departmentCode')" name="code">
            <a-input v-model:value="formModel.code" data-testid="app-departments-form-code" />
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
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, reactive, ref, watch } from "vue";
import type { FormInstance } from "ant-design-vue";
import { message } from "ant-design-vue";
import { FolderOutlined } from "@ant-design/icons-vue";
import { useI18n } from "vue-i18n";
import { CrudPageLayout } from "@atlas/shared-ui";
import { debounce, type SelectOption } from "@atlas/shared-core";
import { usePermission } from "@/composables/usePermission";
import { APP_PERMISSIONS } from "@/constants/permissions";
import { useAppContext } from "@/composables/useAppContext";
import {
  getDepartmentsAll,
  getDepartmentsPaged,
  createDepartment,
  updateDepartment,
  deleteDepartment,
} from "@/services/api-org-management";
import type { AppDepartmentListItem } from "@/types/organization";

type FormMode = "create" | "edit";

interface DeptTreeNode extends AppDepartmentListItem {
  children?: DeptTreeNode[];
}

const { t } = useI18n();
const { appId } = useAppContext();
const { hasPermission } = usePermission();
const isMounted = ref(false);

const placeholderDash = "-";

const canCreate = hasPermission(APP_PERMISSIONS.APP_MEMBERS_UPDATE);
const canUpdate = hasPermission(APP_PERMISSIONS.APP_MEMBERS_UPDATE);
const canDelete = hasPermission(APP_PERMISSIONS.APP_MEMBERS_UPDATE);

const tableColumns = computed(() => [
  { title: t("systemDepartments.colDepartmentName"), dataIndex: "name", key: "name" },
  { title: t("systemDepartments.colDepartmentCode"), dataIndex: "code", key: "code" },
  { title: t("systemDepartments.colManager"), key: "manager" },
  { title: t("systemDepartments.colMemberCount"), key: "memberCount" },
  { title: t("systemDepartments.colActions"), key: "actions", width: 160 },
]);

const loading = ref(false);
const tableKeyword = ref("");
const allDepartments = ref<AppDepartmentListItem[]>([]);
const expandedRowKeys = ref<string[]>([]);

const formVisible = ref(false);
const formMode = ref<FormMode>("create");
const formRef = ref<FormInstance>();
const formModel = reactive({
  name: "",
  code: "",
  parentId: undefined as string | undefined,
  sortOrder: 0,
});

const formRules = {
  name: [{ required: true, message: t("systemDepartments.nameRequired") }],
  code: [{ required: true, message: t("systemDepartments.codeRequired") }],
};

const parentOptions = ref<SelectOption[]>([]);
const parentLoading = ref(false);
const selectedId = ref<string | null>(null);

function buildDepartmentTree(items: AppDepartmentListItem[]): DeptTreeNode[] {
  const nodeMap = new Map<string, DeptTreeNode>();
  items.forEach((item) => {
    nodeMap.set(item.id, { ...item });
  });

  const roots: DeptTreeNode[] = [];
  items.forEach((item) => {
    const node = nodeMap.get(item.id);
    if (!node) return;
    // 历史数据中根节点可能使用 parentId=self 表示，需按根节点处理，避免整棵树丢失。
    if (item.parentId && item.parentId !== item.id) {
      const parent = nodeMap.get(item.parentId);
      if (parent) {
        parent.children = parent.children ?? [];
        parent.children.push(node);
        return;
      }
    }
    roots.push(node);
  });

  const sortNodes = (nodes: DeptTreeNode[]) => {
    nodes.sort((a, b) => {
      if (a.sortOrder !== b.sortOrder) {
        return a.sortOrder - b.sortOrder;
      }
      return a.name.localeCompare(b.name, "zh-Hans-CN");
    });
    nodes.forEach((n) => {
      if (n.children?.length) {
        sortNodes(n.children);
      } else {
        delete n.children;
      }
    });
  };

  sortNodes(roots);
  return roots;
}

function filterDeptTree(nodes: DeptTreeNode[], keywordValue: string): DeptTreeNode[] {
  const trimmed = keywordValue.trim().toLowerCase();
  if (!trimmed) {
    return nodes;
  }
  const result: DeptTreeNode[] = [];
  for (const node of nodes) {
    const childFiltered = node.children?.length ? filterDeptTree(node.children, keywordValue) : [];
    const nameMatch = node.name.toLowerCase().includes(trimmed);
    const codeMatch = node.code.toLowerCase().includes(trimmed);
    if (nameMatch || codeMatch || childFiltered.length > 0) {
      const next: DeptTreeNode = { ...node };
      if (childFiltered.length > 0) {
        next.children = childFiltered;
      } else {
        delete next.children;
      }
      result.push(next);
    }
  }
  return result;
}

const fullTree = computed(() => buildDepartmentTree(allDepartments.value));
const displayTree = computed(() => filterDeptTree(fullTree.value, tableKeyword.value));

function collectExpandableIds(nodes: DeptTreeNode[]): string[] {
  const acc: string[] = [];
  for (const n of nodes) {
    if (n.children && n.children.length > 0) {
      acc.push(n.id);
      acc.push(...collectExpandableIds(n.children));
    }
  }
  return acc;
}

const expandableIds = computed(() => collectExpandableIds(displayTree.value));

const isAllExpanded = computed(() => {
  const keys = expandableIds.value;
  if (keys.length === 0) {
    return false;
  }
  const open = new Set(expandedRowKeys.value);
  return keys.every((id) => open.has(id));
});

const expandToggleLabel = computed(() =>
  isAllExpanded.value ? t("systemDepartments.collapseAll") : t("systemDepartments.expandAll")
);

const tableExpandable = computed(() => ({
  expandedRowKeys: expandedRowKeys.value,
  onExpandedRowsChange: (keys: readonly (string | number)[]) => {
    expandedRowKeys.value = keys.map(String);
  },
}));

function toggleExpandAll() {
  const keys = expandableIds.value;
  if (keys.length === 0) {
    return;
  }
  if (isAllExpanded.value) {
    expandedRowKeys.value = [];
  } else {
    expandedRowKeys.value = [...keys];
  }
}

function pruneExpandedToTree(tree: DeptTreeNode[]) {
  const valid = new Set<string>();
  const walk = (nodes: DeptTreeNode[]) => {
    for (const n of nodes) {
      valid.add(n.id);
      if (n.children?.length) {
        walk(n.children);
      }
    }
  };
  walk(tree);
  expandedRowKeys.value = expandedRowKeys.value.filter((id) => valid.has(id));
}

watch(displayTree, (tree) => {
  pruneExpandedToTree(tree);
});

const loadParentOptions = async (kw?: string) => {
  const id = appId.value;
  if (!id) return;

  parentLoading.value = true;
  try {
    const result = await getDepartmentsPaged(id, {
      pageIndex: 1,
      pageSize: 20,
      keyword: kw?.trim() || undefined,
    });
    if (!isMounted.value) return;
    parentOptions.value = result.items.map((item) => ({
      label: item.name,
      value: item.id,
    }));
  } catch (error) {
    if (!isMounted.value) return;
    message.error(error instanceof Error ? error.message : t("systemDepartments.loadDepartmentsFailed"));
  } finally {
    if (isMounted.value) {
      parentLoading.value = false;
    }
  }
};

const ensureParentOption = (parentId?: string) => {
  if (!parentId) return;
  const exists = parentOptions.value.some((item) => String(item.value) === parentId);
  if (!exists) {
    const dept = allDepartments.value.find((d) => d.id === parentId);
    const label = dept?.name ?? parentId;
    parentOptions.value = [{ label, value: parentId }, ...parentOptions.value];
  }
};

const handleParentSearch = debounce((value: string) => {
  void loadParentOptions(value);
});

const openCreate = () => {
  formMode.value = "create";
  selectedId.value = null;
  formModel.name = "";
  formModel.code = "";
  formModel.parentId = undefined;
  formModel.sortOrder = 0;
  void loadParentOptions();
  formVisible.value = true;
};

const openEdit = async (record: AppDepartmentListItem) => {
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

const loadAllDepartments = async () => {
  const id = appId.value;
  if (!id) return;

  loading.value = true;
  try {
    const list = await getDepartmentsAll(id);
    if (!isMounted.value) return;
    allDepartments.value = list;
    pruneExpandedToTree(displayTree.value);
  } catch (error) {
    if (!isMounted.value) return;
    message.error(error instanceof Error ? error.message : t("systemDepartments.loadDepartmentTreeFailed"));
  } finally {
    if (isMounted.value) {
      loading.value = false;
    }
  }
};

const submitForm = async () => {
  const id = appId.value;
  if (!id) return;

  const valid = await formRef.value?.validate().catch(() => false);
  if (!valid) return;

  try {
    if (formMode.value === "create") {
      await createDepartment(id, {
        name: formModel.name,
        code: formModel.code,
        parentId: formModel.parentId ?? undefined,
        sortOrder: formModel.sortOrder,
      });
      message.success(t("crud.createSuccess"));
    } else if (selectedId.value) {
      await updateDepartment(id, selectedId.value, {
        name: formModel.name,
        code: formModel.code,
        parentId: formModel.parentId ?? undefined,
        sortOrder: formModel.sortOrder,
      });
      message.success(t("crud.updateSuccess"));
    }
    formVisible.value = false;
    await loadAllDepartments();
    void loadParentOptions();
  } catch (error) {
    message.error(error instanceof Error ? error.message : t("crud.submitFailed"));
  }
};

const handleDeleteDept = async (deptId: string) => {
  const id = appId.value;
  if (!id) return;

  try {
    await deleteDepartment(id, deptId);
    message.success(t("crud.deleteSuccess"));
    await loadAllDepartments();
    void loadParentOptions();
  } catch (error) {
    message.error(error instanceof Error ? error.message : t("crud.deleteFailed"));
  }
};

onMounted(() => {
  isMounted.value = true;
  void loadParentOptions();
  void loadAllDepartments();
});

onUnmounted(() => {
  isMounted.value = false;
});
</script>

<style scoped>
.app-departments-page-root :deep(.crud-search-bar) {
  display: none;
}

.app-departments-page-root :deep(.crud-toolbar) {
  display: none;
}

.app-departments-inner-card {
  background: var(--ant-color-bg-container, #fff);
}

.app-departments-inner-toolbar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 12px;
  margin-bottom: 16px;
}

.app-departments-search-input {
  flex: 1;
  min-width: 200px;
  max-width: 360px;
}

.app-departments-expand-btn {
  border-radius: 6px;
}

.app-departments-folder-icon {
  color: var(--ant-color-primary, #722ed1);
  font-size: 16px;
}

.app-departments-name-text {
  font-weight: 600;
}

.app-departments-code-tag {
  margin: 0;
  font-family: ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas, "Liberation Mono", "Courier New", monospace;
  border-radius: 4px;
}

.app-departments-placeholder-cell {
  color: var(--ant-color-text-secondary, rgba(0, 0, 0, 0.45));
}
</style>
