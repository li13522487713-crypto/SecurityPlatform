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
      <a-button type="primary" :disabled="!canCreate" @click="openCreate">发布公告</a-button>
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
            <a-tag :color="record.noticeType === 'System' ? 'blue' : record.noticeType === 'Reminder' ? 'orange' : 'purple'">
              {{ typeLabel(record.noticeType) }}
            </a-tag>
          </template>
          <template v-else-if="column.key === 'priority'">
            <a-tag :color="priorityColor(record.priority)">{{ priorityLabel(record.priority) }}</a-tag>
          </template>
          <template v-else-if="column.key === 'actions'">
            <a-space>
              <a-button type="link" :disabled="!canUpdate" @click="openEdit(record)">编辑</a-button>
              <a-popconfirm
                v-if="record.isActive && canUpdate"
                title="确认撤回此公告？撤回后用户将无法看到。"
                @confirm="handleRevoke(record.id)"
              >
                <a-button type="link" danger>撤回</a-button>
              </a-popconfirm>
              <a-popconfirm
                v-if="canDelete"
                title="确认彻底删除此公告？"
                @confirm="handleDelete(record.id)"
              >
                <a-button type="link" danger :disabled="!canDelete">删除</a-button>
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
            <a-radio value="System">通知</a-radio>
            <a-radio value="Announcement">公告</a-radio>
            <a-radio value="Reminder">提醒</a-radio>
          </a-radio-group>
        </a-form-item>
        <a-form-item label="优先级" name="priority">
          <a-select
            v-model:value="formData.priority"
            :options="[
              { label: '普通', value: 0 },
              { label: '重要', value: 1 },
              { label: '紧急', value: 2 }
            ]"
          />
        </a-form-item>
        <a-form-item label="标题" name="title">
          <a-input v-model:value="formData.title" placeholder="请输入标题" :maxlength="100" />
        </a-form-item>
        <a-form-item label="内容" name="content">
          <a-textarea v-model:value="formData.content" placeholder="请输入内容" :rows="8" />
        </a-form-item>
      </a-form>
    </template>
  </CrudPageLayout>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, onUnmounted } from "vue";

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { message } from "ant-design-vue";
import type { TablePaginationConfig } from "ant-design-vue";
import type { FormInstance } from "ant-design-vue";
import CrudPageLayout from "@/components/crud/CrudPageLayout.vue";
import {
  createNotification,
  deleteNotification,
  getNotificationsManage,
  revokeNotification,
  updateNotification,
  type NotificationDto
} from "@/services/notification";
import { getAuthProfile, hasPermission } from "@/utils/auth";

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
const profile = getAuthProfile();
const canCreate = hasPermission(profile, "notification:create");
const canUpdate = hasPermission(profile, "notification:update");
const canDelete = hasPermission(profile, "notification:delete");

const formData = reactive({
  title: "",
  noticeType: "System",
  priority: 0,
  content: "",
});

const formRules = {
  title: [{ required: true, message: "请输入标题" }],
  noticeType: [{ required: true, message: "请选择类型" }],
  priority: [{ required: true, message: "请选择优先级" }],
  content: [{ required: true, message: "请输入内容" }]
};

const tableColumns = [
  { title: "标题", dataIndex: "title", key: "title", width: 300, ellipsis: true },
  { title: "类型", key: "noticeType", width: 100 },
  { title: "优先级", key: "priority", width: 100 },
  { title: "状态", key: "isActive", width: 100 },
  { title: "发布时间", dataIndex: "createdAt", key: "createdAt", width: 180 },
  { title: "操作", key: "actions", width: 200 }
];

async function loadData() {
  loading.value = true;
  try {
    const result  = await getNotificationsManage({
      pageIndex: pagination.current ?? 1,
      pageSize: pagination.pageSize ?? 20,
      title: keyword.value || undefined
    });

    if (!isMounted.value) return;
    dataSource.value = result.items;
    pagination.total = result.total;
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
  if (!canCreate) {
    return;
  }
  formMode.value = "create";
  currentId.value = "";
  formData.title = "";
  formData.content = "";
  formData.noticeType = "System";
  formData.priority = 0;
  formVisible.value = true;
}

function openEdit(record: NotificationDto) {
  if (!canUpdate) {
    return;
  }
  formMode.value = "edit";
  currentId.value = String(record.id);
  formData.title = record.title;
  formData.content = record.content;
  formData.noticeType = record.noticeType;
  formData.priority = normalizePriority(record.priority);
  formVisible.value = true;
}

function normalizePriority(priority: number | null | undefined): number {
  if (priority === 1 || priority === 2) {
    return priority;
  }
  return 0;
}

function closeForm() {
  formVisible.value = false;
  formRef.value?.resetFields();
}

async function submitForm() {
  try {
    await formRef.value?.validate();

    if (!isMounted.value) return;
  } catch {
    return;
  }
  
  submitting.value = true;
  try {
    if (formMode.value === "create") {
      if (!canCreate) {
        message.error("暂无发布权限");
        return;
      }
      await createNotification({
        title: formData.title,
        content: formData.content,
        noticeType: formData.noticeType,
        priority: formData.priority
      });

      if (!isMounted.value) return;
    } else {
      if (!canUpdate) {
        message.error("暂无编辑权限");
        return;
      }
      await updateNotification(currentId.value, {
        title: formData.title,
        content: formData.content,
        noticeType: formData.noticeType,
        priority: formData.priority
      });

      if (!isMounted.value) return;
    }
    message.success("保存成功");
    closeForm();
    loadData();
  } catch (err: unknown) {
    message.error((err instanceof Error ? err.message : "保存失败"));
  } finally {
    submitting.value = false;
  }
}

async function handleRevoke(id: string) {
  if (!canUpdate) {
    return;
  }
  try {
    await revokeNotification(id);

    if (!isMounted.value) return;
    message.success("撤回成功");
    loadData();
  } catch (err: unknown) {
    message.error((err instanceof Error ? err.message : "撤回失败"));
  }
}

async function handleDelete(id: string) {
  if (!canDelete) {
    return;
  }
  try {
    await deleteNotification(id);

    if (!isMounted.value) return;
    message.success("删除成功");
    loadData();
  } catch (err: unknown) {
    message.error((err instanceof Error ? err.message : "删除失败"));
  }
}

function typeLabel(type: string): string {
  const map: Record<string, string> = {
    Announcement: "公告",
    System: "通知",
    Reminder: "提醒"
  };
  return map[type] ?? type;
}

function priorityLabel(priority: number): string {
  if (priority >= 2) return "紧急";
  if (priority === 1) return "重要";
  return "普通";
}

function priorityColor(priority: number): string {
  if (priority >= 2) return "red";
  if (priority === 1) return "orange";
  return "default";
}

onMounted(() => {
  loadData();
});
</script>
