<template>
  <div class="tenants-page">
    <a-card :bordered="false" class="mb-4">
      <a-form layout="inline" @finish="handleSearch">
        <a-form-item label="关键词">
          <a-input v-model:value="searchParams.keyword" placeholder="租户名称/编码" allow-clear />
        </a-form-item>
        <a-form-item label="状态">
          <a-select v-model:value="searchParams.isActive" placeholder="全部" allow-clear style="width: 120px">
            <a-select-option :value="true">启用</a-select-option>
            <a-select-option :value="false">停用</a-select-option>
          </a-select>
        </a-form-item>
        <a-form-item>
          <a-button type="primary" html-type="submit">查询</a-button>
          <a-button style="margin-left: 8px" @click="resetSearch">重置</a-button>
        </a-form-item>
      </a-form>
    </a-card>

    <a-card :bordered="false">
      <template #extra>
        <a-button type="primary" @click="handleCreate" v-permission="'system:tenant:create'">
          <template #icon><PlusOutlined /></template>
          新建租户
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
              @change="(checked) => handleToggleStatus(record, checked)"
            />
          </template>
          <template v-else-if="column.dataIndex === 'createdAt'">
            {{ formatDate(record.createdAt) }}
          </template>
          <template v-else-if="column.key === 'action'">
            <a-space>
              <a-button type="link" size="small" @click="handleEdit(record)" v-permission="'system:tenant:update'">
                编辑
              </a-button>
              <a-popconfirm
                title="确定要删除该租户吗？删除后将无法恢复相关的所有数据！"
                @confirm="handleDelete(record)"
                okText="确定"
                cancelText="取消"
                :disabled="record.id === 1 || record.id === 10000"
              >
                <a-button type="link" size="small" danger :disabled="record.id === 1 || record.id === 10000" v-permission="'system:tenant:delete'">
                  删除
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
      @ok="handleModalOk"
      @cancel="handleModalCancel"
      :confirm-loading="modalConfirmLoading"
    >
      <a-form
        ref="formRef"
        :model="formState"
        :rules="rules"
        :label-col="{ span: 6 }"
        :wrapper-col="{ span: 16 }"
      >
        <a-form-item label="租户名称" name="name">
          <a-input v-model:value="formState.name" placeholder="请输入租户名称" />
        </a-form-item>
        <a-form-item label="租户编码" name="code">
          <a-input v-model:value="formState.code" placeholder="请输入租户编码 (唯一标识)" :disabled="isEdit" />
        </a-form-item>
        <a-form-item label="描述" name="description">
          <a-textarea v-model:value="formState.description" :rows="3" placeholder="请输入租户描述" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from 'vue';
import { message } from 'ant-design-vue';
import { PlusOutlined } from '@ant-design/icons-vue';
import type { FormInstance } from 'ant-design-vue';
import dayjs from 'dayjs';
import * as tenantApi from '@/services/api-tenants';
import type { TenantQueryRequest, TenantCreateRequest, TenantUpdateRequest, TenantDto } from '@/services/api-tenants';

// -- 搜索与列表状态 --
const searchParams = reactive<TenantQueryRequest>({
  keyword: undefined,
  isActive: undefined,
  pageNumber: 1,
  pageSize: 10
});

const loading = ref(false);
const tableData = ref<TenantDto[]>([]);
const pagination = reactive({
  current: 1,
  pageSize: 10,
  total: 0,
  showSizeChanger: true,
  showTotal: (total: number) => `共 ${total} 条`
});

const columns = [
  { title: '租户名称', dataIndex: 'name', key: 'name' },
  { title: '租户编码', dataIndex: 'code', key: 'code' },
  { title: '描述', dataIndex: 'description', key: 'description', ellipsis: true },
  { title: '状态', dataIndex: 'isActive', key: 'isActive', width: 100 },
  { title: '创建时间', dataIndex: 'createdAt', key: 'createdAt', width: 180 },
  { title: '操作', key: 'action', width: 150, fixed: 'right' }
];

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
  name: [{ required: true, message: '请输入租户名称', trigger: 'blur' }],
  code: [{ required: true, message: '请输入租户唯一编码', trigger: 'blur' }]
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
      pagination.total = res.totalCount || 0;
    }
  } catch (error: any) {
    message.error(error.message || '获取租户列表失败');
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

const handleTableChange = (pag: any) => {
  pagination.current = pag.current;
  pagination.pageSize = pag.pageSize;
  fetchData();
};

const formatDate = (val: Date | string | null | undefined) => {
  if (!val) return '-';
  return dayjs(val).format('YYYY-MM-DD HH:mm:ss');
};

const handleToggleStatus = async (record: TenantDto, checked: boolean | string | number) => {
  const isChecked = Boolean(checked);
  try {
    await tenantApi.toggleTenantStatus(record.id, isChecked);
    message.success(`${isChecked ? '启用' : '停用'}成功`);
    record.isActive = isChecked;
  } catch (error: any) {
    message.error(error.message || '切换状态失败');
  }
};

const handleDelete = async (record: TenantDto) => {
  try {
    await tenantApi.deleteTenant(record.id);
    message.success('删除成功');
    if (tableData.value.length === 1 && pagination.current > 1) {
      pagination.current -= 1;
    }
    fetchData();
  } catch (error: any) {
    message.error(error.message || '删除失败');
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
  modalTitle.value = '新建租户';
  resetForm();
  modalVisible.value = true;
};

const modalTitle = ref('新建租户');

const handleEdit = (record: TenantDto) => {
  isEdit.value = true;
  modalTitle.value = '编辑租户';
  resetForm();
  currentId.value = record.id;
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
        id: currentId.value,
        name: formState.name,
        code: formState.code,
        description: formState.description || undefined
      };
      await tenantApi.updateTenant(currentId.value.toString(), payload);
      message.success('编辑租户成功');
    } else {
      const payload: TenantCreateRequest = {
        name: formState.name,
        code: formState.code,
        description: formState.description || undefined
      };
      await tenantApi.createTenant(payload);
      message.success('新建租户成功');
    }
    modalVisible.value = false;
    fetchData();
  } catch (error: any) {
    message.error(error.message || '操作失败');
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
