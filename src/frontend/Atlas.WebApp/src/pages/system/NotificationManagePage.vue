<template>
  <CrudPageLayout
    v-model:keyword="keyword"
    title="公告管理"
    search-placeholder="搜索公告标题"
    :drawer-open="formVisible"
    :drawer-title="formMode === 'create' ? '新增公告' : '编辑公告'"
    :drawer-width="600"
    :submit-loading="submitting"
    :submit-disabled="submitting"
    @update:drawer-open="formVisible = $event"
    @search="handleSearch"
    @reset="handleReset"
    @close-form="closeForm"
    @submit="submitForm"
  >
    <template #toolbar-actions>
      <a-button type="primary" @click="openCreate">发布公告</a-button>
    </template>

    <template #table>
      <a-table
        :columns="tableColumns"
        :data-source="dataSource"
        :pagination="pagination"
        :loading="loading"
        row-key="id"
        @change="onTableChange"
      >
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'isActive'">
            <a-tag :color="record.isActive ? 'success' : 'default'">
              {{ record.isActive ? '已发布' : '已撤回' }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'noticeType'">
            <a-tag :color="record.noticeType === '1' ? 'blue' : 'orange'">
              {{ record.noticeType === '1' ? '通知' : '公告' }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'actions'">
            <a-space>
              <a-button type="link" @click="openEdit(record)">编辑</a-button>
              <a-popconfirm
                v-if="record.isActive"
                title="确认撤回此公告？撤回后用户将无法看到。"
                @confirm="handleRevoke(record.id)"
              >
                <a-button type="link" danger>撤回</a-button>
              </a-popconfirm>
              <a-popconfirm
                title="确认彻底删除此公告？"
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
      <a-form :model="formData" :rules="formRules" ref="formRef" layout="vertical">
        <a-form-item label="类型" name="noticeType">
          <a-radio-group v-model:value="formData.noticeType">
            <a-radio value="1">通知</a-radio>
            <a-radio value="2">公告</a-radio>
          </a-radio-group>
        </a-form-item>
        <a-form-item label="标题" name="title">
          <a-input v-model:value="formData.title" placeholder="请输入标题" :maxlength="100" />
        </a-form-item>
        <a-form-item label="内容" name="content">
          <a-textarea v-model:value="formData.content" placeholder="请输入内容" :rows="8" />
        </a-form-item>
        <a-form-item label="状态" name="isActive">
          <a-switch v-model:checked="formData.isActive" checked-children="发布" un-checked-children="草稿" />
        </a-form-item>
      </a-form>
    </template>
  </CrudPageLayout>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted } from "vue";
import { message } from "ant-design-vue";
import type { TablePaginationConfig } from "ant-design-vue";
import type { FormInstance } from "ant-design-vue";
import CrudPageLayout from "@/components/crud/CrudPageLayout.vue";
import { requestApi } from "@/services/api-core";
import { toQuery } from "@/services/api-core";
import type { ApiResponse, PagedResult } from "@/types/api";

interface NotificationDto {
  id: string;
  noticeType: string;
  title: string;
  content: string;
  isActive: boolean;
  createdAt: string;
}

const keyword = ref("");
const dataSource = ref<NotificationDto[]>([]);
const loading = ref(false);
const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 20,
  total: 0,
});

const formVisible = ref(false);
const formMode = ref<"create" | "edit">("create");
const submitting = ref(false);
const formRef = ref<FormInstance>();
const currentId = ref("");

const formData = reactive({
  title: "",
  noticeType: "1",
  content: "",
  isActive: true,
});

const formRules = {
  title: [{ required: true, message: "请输入标题" }],
  noticeType: [{ required: true, message: "请选择类型" }],
  content: [{ required: true, message: "请输入内容" }]
};

const tableColumns = [
  { title: "标题", dataIndex: "title", key: "title", width: 300, ellipsis: true },
  { title: "类型", key: "noticeType", width: 100 },
  { title: "状态", key: "isActive", width: 100 },
  { title: "发布时间", dataIndex: "createdAt", key: "createdAt", width: 180 },
  { title: "操作", key: "actions", width: 200 }
];

async function loadData() {
  loading.value = true;
  try {
    const q = toQuery({
      pageIndex: pagination.current ?? 1,
      pageSize: pagination.pageSize ?? 20
    });
    const titleFilter = keyword.value ? `&title=${encodeURIComponent(keyword.value)}` : "";
    const res = await requestApi<ApiResponse<PagedResult<NotificationDto>>>(`/notifications/manage?${q}${titleFilter}`);
    if (res.data) {
      dataSource.value = res.data.items;
      pagination.total = res.data.total;
    }
  } catch (err: unknown) {
    message.error((err instanceof Error ? err.message : "加载失败"));
  } finally {
    loading.value = false;
  }
}

function handleSearch() {
  pagination.current = 1;
  loadData();
}

function handleReset() {
  keyword.value = "";
  pagination.current = 1;
  loadData();
}

function onTableChange(pag: TablePaginationConfig) {
  pagination.current = pag.current ?? 1;
  loadData();
}

function openCreate() {
  formMode.value = "create";
  currentId.value = "";
  formData.title = "";
  formData.content = "";
  formData.noticeType = "1";
  formData.isActive = true;
  formVisible.value = true;
}

function openEdit(record: NotificationDto) {
  formMode.value = "edit";
  currentId.value = record.id;
  formData.title = record.title;
  formData.content = record.content;
  formData.noticeType = record.noticeType;
  formData.isActive = record.isActive;
  formVisible.value = true;
}

function closeForm() {
  formVisible.value = false;
  formRef.value?.resetFields();
}

async function submitForm() {
  try {
    await formRef.value?.validate();
  } catch {
    return;
  }
  
  submitting.value = true;
  try {
    const method = formMode.value === "create" ? "POST" : "PUT";
    const url = formMode.value === "create" ? "/notifications/manage" : `/notifications/manage/${currentId.value}`;
    const res = await requestApi<ApiResponse<any>>(url, {
      method,
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(formData)
    });
    if (res.success) {
      message.success("保存成功");
      closeForm();
      loadData();
    }
  } catch (err: unknown) {
    message.error((err instanceof Error ? err.message : "保存失败"));
  } finally {
    submitting.value = false;
  }
}

async function handleRevoke(id: string) {
  try {
    await requestApi(`/notifications/manage/${id}/revoke`, { method: "PUT" });
    message.success("撤回成功");
    loadData();
  } catch (err: unknown) {
    message.error((err instanceof Error ? err.message : "撤回失败"));
  }
}

async function handleDelete(id: string) {
  try {
    await requestApi(`/notifications/manage/${id}`, { method: "DELETE" });
    message.success("删除成功");
    loadData();
  } catch (err: unknown) {
    message.error((err instanceof Error ? err.message : "删除失败"));
  }
}

onMounted(() => {
  loadData();
});
</script>
