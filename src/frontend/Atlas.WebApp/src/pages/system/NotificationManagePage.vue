<template>
  <CrudPageLayout
    v-model:keyword="keyword"
    :title="t('notificationManage.pageTitle')"
    :search-placeholder="t('notificationManage.searchPlaceholder')"
    :drawer-open="formVisible"
    :drawer-title="formMode === 'create' ? t('notificationManage.drawerCreate') : t('notificationManage.drawerEdit')"
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
      <a-button type="primary" :disabled="!canCreate" @click="openCreate">{{ t("notificationManage.publish") }}</a-button>
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
              {{ record.isActive ? t("notificationManage.statusPublished") : t("notificationManage.statusRevoked") }}
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
              <a-button type="link" :disabled="!canUpdate" @click="openEdit(record)">{{ t("notificationManage.edit") }}</a-button>
              <a-popconfirm
                v-if="record.isActive && canUpdate"
                :title="t('notificationManage.revokeConfirm')"
                @confirm="handleRevoke(record.id)"
              >
                <a-button type="link" danger>{{ t("notificationManage.revoke") }}</a-button>
              </a-popconfirm>
              <a-popconfirm
                v-if="canDelete"
                :title="t('notificationManage.deleteConfirm')"
                @confirm="handleDelete(record.id)"
              >
                <a-button type="link" danger :disabled="!canDelete">{{ t("common.delete") }}</a-button>
              </a-popconfirm>
            </a-space>
          </template>
        </template>
      </a-table>
    </template>

    <template #form>
      <a-form ref="formRef" :model="formData" :rules="formRules" layout="vertical">
        <a-form-item :label="t('notificationManage.labelType')" name="noticeType">
          <a-radio-group v-model:value="formData.noticeType">
            <a-radio value="System">{{ t("notificationManage.typeSystem") }}</a-radio>
            <a-radio value="Announcement">{{ t("notificationManage.typeAnnouncement") }}</a-radio>
            <a-radio value="Reminder">{{ t("notificationManage.typeReminder") }}</a-radio>
          </a-radio-group>
        </a-form-item>
        <a-form-item :label="t('notificationManage.labelPriority')" name="priority">
          <a-select
            v-model:value="formData.priority"
            :options="priorityOptions"
          />
        </a-form-item>
        <a-form-item :label="t('notificationManage.labelTitle')" name="title">
          <a-input v-model:value="formData.title" :placeholder="t('notificationManage.titlePlaceholder')" :maxlength="100" />
        </a-form-item>
        <a-form-item :label="t('notificationManage.labelContent')" name="content">
          <a-textarea v-model:value="formData.content" :placeholder="t('notificationManage.contentPlaceholder')" :rows="8" />
        </a-form-item>
      </a-form>
    </template>
  </CrudPageLayout>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, onUnmounted, computed } from "vue";
import { useI18n } from "vue-i18n";

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

const { t } = useI18n();

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

const priorityOptions = computed(() => [
  { label: t("notificationManage.priorityNormal"), value: 0 },
  { label: t("notificationManage.priorityImportant"), value: 1 },
  { label: t("notificationManage.priorityUrgent"), value: 2 },
]);

const formRules = computed(() => ({
  title: [{ required: true, message: t("notificationManage.titleRequired") }],
  noticeType: [{ required: true, message: t("notificationManage.typeRequired") }],
  priority: [{ required: true, message: t("notificationManage.priorityRequired") }],
  content: [{ required: true, message: t("notificationManage.contentRequired") }]
}));

const tableColumns = computed(() => [
  { title: t("notificationManage.colTitle"), dataIndex: "title", key: "title", width: 300, ellipsis: true },
  { title: t("notificationManage.colType"), key: "noticeType", width: 100 },
  { title: t("notificationManage.colPriority"), key: "priority", width: 100 },
  { title: t("notificationManage.colStatus"), key: "isActive", width: 100 },
  { title: t("notificationManage.colPublishedAt"), dataIndex: "createdAt", key: "createdAt", width: 180 },
  { title: t("notificationManage.colActions"), key: "actions", width: 200 }
]);

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
    message.error((err instanceof Error ? err.message : t("notificationManage.loadFailed")));
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
        message.error(t("notificationManage.noCreatePerm"));
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
        message.error(t("notificationManage.noEditPerm"));
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
    message.success(t("notificationManage.saveSuccess"));
    closeForm();
    loadData();
  } catch (err: unknown) {
    message.error((err instanceof Error ? err.message : t("notificationManage.saveFailed")));
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
    message.success(t("notificationManage.revokeSuccess"));
    loadData();
  } catch (err: unknown) {
    message.error((err instanceof Error ? err.message : t("notificationManage.revokeFailed")));
  }
}

async function handleDelete(id: string) {
  if (!canDelete) {
    return;
  }
  try {
    await deleteNotification(id);

    if (!isMounted.value) return;
    message.success(t("notificationManage.deleteSuccess"));
    loadData();
  } catch (err: unknown) {
    message.error((err instanceof Error ? err.message : t("notificationManage.deleteFailed")));
  }
}

function typeLabel(type: string): string {
  const map: Record<string, string> = {
    Announcement: t("notificationManage.typeLabelAnnouncement"),
    System: t("notificationManage.typeLabelSystem"),
    Reminder: t("notificationManage.typeLabelReminder")
  };
  return map[type] ?? type;
}

function priorityLabel(priority: number): string {
  if (priority >= 2) return t("notificationManage.priorityUrgent");
  if (priority === 1) return t("notificationManage.priorityImportant");
  return t("notificationManage.priorityNormal");
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
