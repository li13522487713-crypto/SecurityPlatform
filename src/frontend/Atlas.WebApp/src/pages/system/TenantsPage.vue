<template>
  <div class="tenants-page">
    <a-card :bordered="false" class="mb-4">
      <a-form layout="inline" @finish="handleSearch">
        <a-form-item :label="t('systemTenants.keywordLabel')">
          <a-input v-model:value="searchParams.keyword" :placeholder="t('systemTenants.keywordPlaceholder')" allow-clear />
        </a-form-item>
        <a-form-item :label="t('systemTenants.statusLabel')">
          <a-select v-model:value="searchParams.isActive" :placeholder="t('common.all')" allow-clear style="width: 120px">
            <a-select-option :value="true">{{ t("common.statusEnabled") }}</a-select-option>
            <a-select-option :value="false">{{ t("common.statusDisabled") }}</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item>
          <a-button type="primary" html-type="submit">{{ t("common.search") }}</a-button>
          <a-button style="margin-left: 8px" @click="resetSearch">{{ t("common.reset") }}</a-button>
        </a-form-item>
      </a-form>
    </a-card>

    <a-card :bordered="false">
      <template #extra>
        <a-button type="primary" v-permission="'system:tenant:create'" @click="handleCreate">
          <template #icon><PlusOutlined /></template>
          {{ t("systemTenants.createTenant") }}
        </a-button>
      </template>

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
              :disabled="record.id === 1 || record.id === 10000"
              @change="handleToggleStatus(record, Boolean($event))"
            />
          </template>
          <template v-else-if="column.dataIndex === 'createdAt'">
            {{ formatDate(record.createdAt) }}
          </template>
          <template v-else-if="column.key === 'action'">
            <a-space>
              <a-button type="link" size="small" v-permission="'system:tenant:update'" @click="handleEdit(record)">
                {{ t("common.edit") }}
              </a-button>
              <a-popconfirm
                :title="t('systemTenants.deleteConfirm')"
                :ok-text="t('systemTenants.confirm')"
                :cancel-text="t('common.cancel')"
                :disabled="record.id === 1 || record.id === 10000"
                @confirm="handleDelete(record)"
              >
                <a-button type="link" size="small" danger v-permission="'system:tenant:delete'" :disabled="record.id === 1 || record.id === 10000">
                  {{ t("common.delete") }}
                </a-button>
              </a-popconfirm>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-card>

    <!-- 增改表单弹窗 -->
    <a-modal
      v-model:open="modalVisible"
      :title="modalTitle"
      :confirm-loading="modalConfirmLoading"
      @ok="handleModalOk"
      @cancel="handleModalCancel"
    >
      <a-form
        ref="formRef"
        :model="formState"
        :rules="rules"
        :label-col="{ span: 6 }"
        :wrapper-col="{ span: 16 }"
      >
        <a-form-item :label="t('systemTenants.tenantName')" name="name">
          <a-input v-model:value="formState.name" :placeholder="t('systemTenants.tenantNamePlaceholder')" />
        </a-form-item>
        <a-form-item :label="t('systemTenants.tenantCode')" name="code">
          <a-input v-model:value="formState.code" :placeholder="t('systemTenants.tenantCodePlaceholder')" :disabled="isEdit" />
        </a-form-item>
        <a-form-item :label="t('systemTenants.description')" name="description">
          <a-textarea v-model:value="formState.description" :rows="3" :placeholder="t('systemTenants.descriptionPlaceholder')" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from "vue";
import { message } from 'ant-design-vue';
import { PlusOutlined } from '@ant-design/icons-vue';
import type { FormInstance } from 'ant-design-vue';
import type { TablePaginationConfig } from 'ant-design-vue';
import { useI18n } from "vue-i18n";
import dayjs from 'dayjs';
import * as tenantApi from '@/services/api-tenants';
import type { TenantQueryRequest, TenantCreateRequest, TenantUpdateRequest, TenantDto } from '@/services/api-tenants';

const { t } = useI18n();

// -- 搜索与列表状态 --
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
  { title: t("systemTenants.colActions"), key: "action", width: 150, fixed: "right" }
]));

// -- 弹窗表单状态 --
const modalVisible = ref(false);
const modalConfirmLoading = ref(false);
const isEdit = ref(false);
const formRef = ref<FormInstance>();
const currentId = ref<number | null>(null);

const formState = reactive({
  name: '',
  code: '',
  description: ''
});

const rules = {
  name: [{ required: true, message: t("systemTenants.nameRequired"), trigger: "blur" }],
  code: [{ required: true, message: t("systemTenants.codeRequired"), trigger: "blur" }]
};

// -- 生命周期与方法 --
onMounted(() => {
  fetchData();
});

const fetchData = async () => {
  loading.value = true;
  try {
    searchParams.pageIndex = pagination.current;
    searchParams.pageSize = pagination.pageSize;
    const res = await tenantApi.getTenantsPaged(searchParams);
    if (res) {
      tableData.value = res.items || [];
      pagination.total = res.total || 0;
    }
  } catch (error: unknown) {
    message.error((error as Error).message || t("systemTenants.fetchFailed"));
  } finally {
    loading.value = false;
  }
};

const handleSearch = () => {
  pagination.current = 1;
  fetchData();
};

const resetSearch = () => {
  searchParams.keyword = undefined;
  searchParams.isActive = undefined;
  pagination.current = 1;
  fetchData();
};

const handleTableChange = (pag: TablePaginationConfig) => {
  pagination.current = pag.current ?? 1;
  pagination.pageSize = pag.pageSize ?? 10;
  fetchData();
};

const formatDate = (val: Date | string | null | undefined) => {
  if (!val) return "-";
  return dayjs(val).format('YYYY-MM-DD HH:mm:ss');
};

const handleToggleStatus = async (record: TenantDto, checked: boolean) => {
  const isChecked = checked;
  try {
    await tenantApi.toggleTenantStatus(record.id, isChecked);
    message.success(isChecked ? t("systemTenants.enableSuccess") : t("systemTenants.disableSuccess"));
    record.isActive = isChecked;
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
    fetchData();
  } catch (error: unknown) {
    message.error((error as Error).message || t("crud.deleteFailed"));
  }
};

const resetForm = () => {
  formState.name = '';
  formState.code = '';
  formState.description = '';
  currentId.value = null;
  if (formRef.value) {
    formRef.value.clearValidate();
  }
};

const handleCreate = () => {
  isEdit.value = false;
  modalTitle.value = t("systemTenants.modalCreateTitle");
  resetForm();
  modalVisible.value = true;
};

const modalTitle = ref(t("systemTenants.modalCreateTitle"));

const handleEdit = (record: TenantDto) => {
  isEdit.value = true;
  modalTitle.value = t("systemTenants.modalEditTitle");
  resetForm();
  currentId.value = Number(record.id);
  formState.name = record.name;
  formState.code = record.code;
  formState.description = record.description || '';
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
    if (isEdit.value && currentId.value) {
      const payload: TenantUpdateRequest = {
        id: currentId.value.toString(),
        name: formState.name,
        code: formState.code,
        description: formState.description || undefined
      };
      await tenantApi.updateTenant(currentId.value.toString(), payload);
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
    fetchData();
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

<style scoped>
.mb-4 {
  margin-bottom: 16px;
}
</style>
