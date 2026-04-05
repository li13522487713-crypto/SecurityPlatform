<template>
  <CrudPageLayout
    v-model:keyword="searchParams.keyword"
    :title="t('systemPositions.pageTitle')"
    :search-placeholder="t('systemPositions.searchPlaceholder')"
    :drawer-open="drawerVisible"
    :drawer-title="isEdit ? t('systemPositions.drawerEditTitle') : t('systemPositions.drawerCreateTitle')"
    :drawer-width="520"
    :submit-loading="submitting"
    @update:drawer-open="drawerVisible = $event"
    @search="handleSearch"
    @reset="resetSearch"
    @close-form="closeDrawer"
    @submit="handleSubmit"
  >
    <template #toolbar-actions>
      <a-button v-if="hasPermission(profile, 'positions:create')" type="primary" @click="openCreate">
        <template #icon><PlusOutlined /></template>
        {{ t("systemPositions.addPosition") }}
      </a-button>
    </template>

    <template #table>
      <a-table
        :columns="columns"
        :data-source="tableData"
        :loading="loading"
        :pagination="pagination"
        row-key="id"
        @change="handleTableChange"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'isActive'">
            <a-tag :color="record.isActive ? 'green' : 'red'">
              {{ record.isActive ? t("common.statusEnabled") : t("common.statusDisabled") }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'isSystem'">
            <a-tag :color="record.isSystem ? 'blue' : 'default'">
              {{ record.isSystem ? t("systemPositions.systemBuiltIn") : t("systemPositions.customType") }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'action'">
            <a-space>
              <a-button v-if="hasPermission(profile, 'positions:update')" type="link" size="small" @click="openEdit(record)">
                {{ t("common.edit") }}
              </a-button>
              <a-popconfirm
                v-if="hasPermission(profile, 'positions:delete') && !record.isSystem"
                :title="t('systemPositions.deleteConfirm')"
                @confirm="handleDelete(record.id)"
              >
                <a-button type="link" size="small" danger>{{ t("common.delete") }}</a-button>
              </a-popconfirm>
            </a-space>
          </template>
        </template>
      </a-table>
    </template>

    <template #form>
      <a-form ref="formRef" :model="formState" :rules="rules" layout="vertical">
        <a-form-item :label="t('systemPositions.positionName')" name="name">
          <a-input v-model:value="formState.name" />
        </a-form-item>
        <a-form-item :label="t('systemPositions.code')" name="code">
          <a-input v-model:value="formState.code" :disabled="isEdit" />
        </a-form-item>
        <a-form-item :label="t('systemPositions.description')" name="description">
          <a-textarea v-model:value="formState.description" :rows="3" />
        </a-form-item>
        <a-form-item :label="t('systemPositions.status')" name="isActive">
          <a-switch v-model:checked="formState.isActive" />
        </a-form-item>
        <a-form-item :label="t('systemPositions.sortOrder')" name="sortOrder">
          <a-input-number v-model:value="formState.sortOrder" :min="0" style="width: 100%" />
        </a-form-item>
      </a-form>
    </template>
  </CrudPageLayout>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from "vue";
import { message } from "ant-design-vue";
import { PlusOutlined } from "@ant-design/icons-vue";
import type { FormInstance, TablePaginationConfig } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import type { PositionListItem, PositionDetail } from "@atlas/shared-core";
import { getAuthProfile, hasPermission } from "@atlas/shared-core";
import { CrudPageLayout } from "@atlas/shared-ui";
import {
  getPositionsPaged,
  getPositionDetail,
  createPosition,
  updatePosition,
  deletePosition
} from "@/services/api-users";

const { t } = useI18n();
const profile = getAuthProfile();

const searchParams = reactive<{ keyword?: string; pageIndex: number; pageSize: number }>({
  keyword: undefined,
  pageIndex: 1,
  pageSize: 10
});

const loading = ref(false);
const tableData = ref<PositionListItem[]>([]);
const pagination = reactive({
  current: 1,
  pageSize: 10,
  total: 0,
  showSizeChanger: true,
  showTotal: (total: number) => t("crud.totalItems", { total })
});

const columns = computed(() => [
  { title: t("systemPositions.colPositionName"), dataIndex: "name", key: "name" },
  { title: t("systemPositions.colCode"), dataIndex: "code", key: "code" },
  { title: t("systemPositions.colDescription"), dataIndex: "description", key: "description", ellipsis: true },
  { title: t("systemPositions.colStatus"), dataIndex: "isActive", key: "isActive", width: 100 },
  { title: t("systemPositions.colType"), dataIndex: "isSystem", key: "isSystem", width: 100 },
  { title: t("systemPositions.colSortOrder"), dataIndex: "sortOrder", key: "sortOrder", width: 80 },
  { title: t("systemPositions.colActions"), key: "action", width: 150, fixed: "right" as const }
]);

const drawerVisible = ref(false);
const submitting = ref(false);
const isEdit = ref(false);
const formRef = ref<FormInstance>();
const currentId = ref<string | null>(null);

const formState = reactive({
  name: "",
  code: "",
  description: "",
  isActive: true,
  sortOrder: 0
});

const rules = {
  name: [{ required: true, message: t("systemPositions.nameRequired"), trigger: "blur" as const }],
  code: [{ required: true, message: t("systemPositions.codeRequired"), trigger: "blur" as const }]
};

onMounted(() => {
  void fetchData();
});

async function fetchData() {
  loading.value = true;
  try {
    const result = await getPositionsPaged({
      keyword: searchParams.keyword,
      pageIndex: searchParams.pageIndex,
      pageSize: searchParams.pageSize
    });
    tableData.value = result.items;
    pagination.total = result.total;
    pagination.current = searchParams.pageIndex;
    pagination.pageSize = searchParams.pageSize;
  } catch {
    message.error(t("crud.queryFailed"));
  } finally {
    loading.value = false;
  }
}

function handleTableChange(pag: TablePaginationConfig) {
  searchParams.pageIndex = pag.current ?? 1;
  searchParams.pageSize = pag.pageSize ?? 10;
  void fetchData();
}

function handleSearch() {
  searchParams.pageIndex = 1;
  void fetchData();
}

function resetSearch() {
  searchParams.keyword = undefined;
  searchParams.pageIndex = 1;
  void fetchData();
}

function resetForm() {
  formState.name = "";
  formState.code = "";
  formState.description = "";
  formState.isActive = true;
  formState.sortOrder = 0;
}

function openCreate() {
  isEdit.value = false;
  currentId.value = null;
  resetForm();
  drawerVisible.value = true;
}

async function openEdit(record: PositionListItem) {
  isEdit.value = true;
  currentId.value = record.id;
  try {
    const detail: PositionDetail = await getPositionDetail(record.id);
    formState.name = detail.name;
    formState.code = detail.code;
    formState.description = detail.description ?? "";
    formState.isActive = detail.isActive;
    formState.sortOrder = detail.sortOrder;
    drawerVisible.value = true;
  } catch {
    message.error(t("crud.queryFailed"));
  }
}

function closeDrawer() {
  drawerVisible.value = false;
  formRef.value?.resetFields();
}

async function handleSubmit() {
  try {
    await formRef.value?.validate();
  } catch {
    return;
  }

  submitting.value = true;
  try {
    if (isEdit.value && currentId.value) {
      await updatePosition(currentId.value, {
        name: formState.name,
        description: formState.description || undefined,
        isActive: formState.isActive,
        sortOrder: formState.sortOrder
      });
      message.success(t("crud.updateSuccess"));
    } else {
      await createPosition({
        name: formState.name,
        code: formState.code,
        description: formState.description || undefined,
        isActive: formState.isActive,
        sortOrder: formState.sortOrder
      });
      message.success(t("crud.createSuccess"));
    }
    closeDrawer();
    void fetchData();
  } catch (e: unknown) {
    message.error(e instanceof Error ? e.message : t("crud.operationFailed"));
  } finally {
    submitting.value = false;
  }
}

async function handleDelete(id: string) {
  try {
    await deletePosition(id);
    message.success(t("crud.deleteSuccess"));
    void fetchData();
  } catch (e: unknown) {
    message.error(e instanceof Error ? e.message : t("crud.deleteFailed"));
  }
}
</script>
