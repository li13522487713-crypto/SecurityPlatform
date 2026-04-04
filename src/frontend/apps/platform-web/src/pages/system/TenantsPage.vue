<template>
  <CrudPageLayout
    v-model:keyword="searchParams.keyword"
    :title="t('route.tenants')"
    :search-placeholder="t('systemTenants.keywordPlaceholder')"
    :drawer-open="modalVisible"
    :drawer-title="modalTitle"
    :drawer-width="520"
    :submit-loading="modalConfirmLoading"
    @update:drawer-open="modalVisible = $event"
    @search="handleSearch"
    @reset="resetSearch"
    @close-form="handleModalCancel"
    @submit="handleModalOk"
  >
    <template #search-filters>
      <a-form-item :label="t('systemTenants.statusLabel')">
        <a-select v-model:value="searchParams.isActive" :placeholder="t('common.all')" allow-clear style="width: 120px">
          <a-select-option :value="true">{{ t("common.statusEnabled") }}</a-select-option>
          <a-select-option :value="false">{{ t("common.statusDisabled") }}</a-select-option>
        </a-select>
      </a-form-item>
    </template>

    <template #toolbar-actions>
      <a-button v-if="hasPermission(profile, 'system:tenant:create')" type="primary" @click="handleCreate">
        <template #icon><PlusOutlined /></template>
        {{ t("systemTenants.createTenant") }}
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
          <template v-if="column.dataIndex === 'isActive'">
            <a-switch
              :checked="record.isActive"
              :disabled="isCoreTenant(record.id)"
              @change="handleToggleStatus(record, Boolean($event))"
            />
          </template>
          <template v-else-if="column.dataIndex === 'createdAt'">
            {{ formatDate(record.createdAt) }}
          </template>
          <template v-else-if="column.key === 'action'">
            <a-space>
              <a-button
                v-if="hasPermission(profile, 'system:tenant:update')"
                type="link"
                size="small"
                @click="handleEdit(record)"
              >
                {{ t("common.edit") }}
              </a-button>
              <a-popconfirm
                :title="t('systemTenants.deleteConfirm')"
                :ok-text="t('systemTenants.confirm')"
                :cancel-text="t('common.cancel')"
                :disabled="isCoreTenant(record.id)"
                @confirm="handleDelete(record)"
              >
                <a-button
                  v-if="hasPermission(profile, 'system:tenant:delete')"
                  type="link"
                  size="small"
                  danger
                  :disabled="isCoreTenant(record.id)"
                >
                  {{ t("common.delete") }}
                </a-button>
              </a-popconfirm>
            </a-space>
          </template>
        </template>
      </a-table>
    </template>

    <template #form>
      <a-form
        ref="formRef"
        :model="formState"
        :rules="rules"
        layout="vertical"
      >
        <a-form-item :label="t('systemTenants.tenantName')" name="name">
          <a-input v-model:value="formState.name" :placeholder="t('systemTenants.tenantNamePlaceholder')" />
        </a-form-item>
        <a-form-item :label="t('systemTenants.tenantCode')" name="code">
          <a-input v-model:value="formState.code" :placeholder="t('systemTenants.tenantCodePlaceholder')" :disabled="isEdit" />
        </a-form-item>
        <a-form-item :label="t('systemTenants.description')" name="description">
          <a-textarea v-model:value="formState.description" :rows="4" :placeholder="t('systemTenants.descriptionPlaceholder')" />
        </a-form-item>
      </a-form>
    </template>
  </CrudPageLayout>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, reactive, ref } from "vue";
import { message } from "ant-design-vue";
import { PlusOutlined } from "@ant-design/icons-vue";
import type { FormInstance } from "ant-design-vue";
import type { TablePaginationConfig } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import { getAuthProfile, hasPermission } from "@atlas/shared-core";
import { CrudPageLayout } from "@atlas/shared-ui";
import * as tenantApi from "@/services/api-tenants";
import type { TenantQueryRequest, TenantCreateRequest, TenantUpdateRequest, TenantDto } from "@/services/api-tenants";

const { t } = useI18n();
const profile = getAuthProfile();

function isCoreTenant(id: number) {
  return id === 1 || id === 10000;
}

const searchParams = reactive<TenantQueryRequest>({
  keyword: undefined,
  isActive: undefined,
  pageIndex: 1,
  pageSize: 10
});

const loading = ref(false);
const tableData = ref<TenantDto[]>([]);
const pagination = reactive({
  current: 1,
  pageSize: 10,
  total: 0,
  showSizeChanger: true,
  showTotal: (total: number) => t("crud.totalItems", { total })
});

const columns = computed(() => ([
  { title: t("systemTenants.colTenantName"), dataIndex: "name", key: "name" },
  { title: t("systemTenants.colTenantCode"), dataIndex: "code", key: "code" },
  { title: t("systemTenants.colDescription"), dataIndex: "description", key: "description", ellipsis: true },
  { title: t("systemTenants.colStatus"), dataIndex: "isActive", key: "isActive", width: 100 },
  { title: t("systemTenants.colCreatedAt"), dataIndex: "createdAt", key: "createdAt", width: 180 },
  { title: t("systemTenants.colActions"), key: "action", width: 150, fixed: "right" as const }
]));

const modalVisible = ref(false);
const modalConfirmLoading = ref(false);
const isEdit = ref(false);
const formRef = ref<FormInstance>();
const currentId = ref<number | null>(null);

const formState = reactive({
  name: "",
  code: "",
  description: ""
});

const rules = {
  name: [{ required: true, message: t("systemTenants.nameRequired"), trigger: "blur" }],
  code: [{ required: true, message: t("systemTenants.codeRequired"), trigger: "blur" }]
};

const isMounted = ref(false);

onMounted(() => {
  isMounted.value = true;
  void fetchData();
});

onUnmounted(() => {
  isMounted.value = false;
});

const fetchData = async () => {
  loading.value = true;
  try {
    searchParams.pageIndex = pagination.current;
    searchParams.pageSize = pagination.pageSize;
    const res = await tenantApi.getTenantsPaged(searchParams);
    if (!isMounted.value) return;
    tableData.value = res.items || [];
    pagination.total = Number(res.total) || 0;
  } catch (error: unknown) {
    if (!isMounted.value) return;
    message.error((error as Error).message || t("systemTenants.fetchFailed"));
  } finally {
    if (isMounted.value) {
      loading.value = false;
    }
  }
};

const handleSearch = () => {
  pagination.current = 1;
  void fetchData();
};

const resetSearch = () => {
  searchParams.keyword = undefined;
  searchParams.isActive = undefined;
  pagination.current = 1;
  void fetchData();
};

const handleTableChange = (pag: TablePaginationConfig) => {
  pagination.current = pag.current ?? 1;
  pagination.pageSize = pag.pageSize ?? 10;
  void fetchData();
};

function formatDate(val: Date | string | null | undefined) {
  if (!val) return "-";
  const date = new Date(val);
  if (Number.isNaN(date.getTime())) return "-";
  return date.toLocaleString();
}

const handleToggleStatus = async (record: TenantDto, checked: boolean) => {
  try {
    await tenantApi.toggleTenantStatus(record.id, checked);
    message.success(checked ? t("systemTenants.enableSuccess") : t("systemTenants.disableSuccess"));
    record.isActive = checked;
  } catch (error: unknown) {
    message.error((error as Error).message || t("systemTenants.toggleFailed"));
  }
};

const handleDelete = async (record: TenantDto) => {
  try {
    await tenantApi.deleteTenant(record.id);
    message.success(t("crud.deleteSuccess"));
    if (tableData.value.length === 1 && pagination.current > 1) {
      pagination.current -= 1;
    }
    void fetchData();
  } catch (error: unknown) {
    message.error((error as Error).message || t("crud.deleteFailed"));
  }
};

const resetForm = () => {
  formState.name = "";
  formState.code = "";
  formState.description = "";
  currentId.value = null;
  formRef.value?.clearValidate();
};

const modalTitle = ref(t("systemTenants.modalCreateTitle"));

const handleCreate = () => {
  isEdit.value = false;
  modalTitle.value = t("systemTenants.modalCreateTitle");
  resetForm();
  modalVisible.value = true;
};

const handleEdit = (record: TenantDto) => {
  isEdit.value = true;
  modalTitle.value = t("systemTenants.modalEditTitle");
  resetForm();
  currentId.value = record.id;
  formState.name = record.name;
  formState.code = record.code;
  formState.description = record.description ?? "";
  modalVisible.value = true;
};

const handleModalOk = async () => {
  try {
    await formRef.value?.validate();
  } catch {
    return;
  }

  modalConfirmLoading.value = true;
  try {
    if (isEdit.value && currentId.value !== null) {
      const payload: TenantUpdateRequest = {
        id: currentId.value,
        name: formState.name,
        code: formState.code,
        description: formState.description || undefined
      };
      await tenantApi.updateTenant(String(currentId.value), payload);
      message.success(t("systemTenants.updateSuccess"));
    } else {
      const payload: TenantCreateRequest = {
        name: formState.name,
        code: formState.code,
        description: formState.description || undefined
      };
      await tenantApi.createTenant(payload);
      message.success(t("systemTenants.createSuccess"));
    }
    modalVisible.value = false;
    void fetchData();
  } catch (error: unknown) {
    message.error((error as Error).message || t("systemTenants.operationFailed"));
  } finally {
    modalConfirmLoading.value = false;
  }
};

const handleModalCancel = () => {
  modalVisible.value = false;
};
</script>
