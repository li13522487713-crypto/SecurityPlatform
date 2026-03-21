<template>
  <a-card :title="t('systemConfig.title')" :bordered="false">
    <div class="crud-toolbar">
      <a-space wrap>
        <a-input-search
          v-model:value="keyword"
          :placeholder="t('systemConfig.searchPlaceholder')"
          allow-clear
          style="width: 260px"
          @search="loadConfigs"
        />
        <a-button @click="handleReset">{{ t("common.reset") }}</a-button>
        <a-button type="primary" @click="openCreate">{{ t("systemConfig.create") }}</a-button>
      </a-space>
    </div>

    <a-table
      :columns="columns"
      :data-source="dataList"
      :loading="loading"
      :pagination="pagination"
      row-key="id"
      :locale="{ emptyText: t('systemConfig.empty') }"
      @change="onTableChange"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'isBuiltIn'">
          <a-tag v-if="record.isBuiltIn" color="gold">
            <template #icon><LockOutlined /></template>
            {{ t("systemConfig.builtIn") }}
          </a-tag>
        </template>
        <template v-else-if="column.key === 'actions'">
          <a-space>
            <a-button type="link" size="small" @click="openEdit(record)">{{ t("common.edit") }}</a-button>
            <a-popconfirm
              v-if="!record.isBuiltIn"
              :title="t('systemConfig.deleteConfirm')"
              :ok-text="t('common.delete')"
              :cancel-text="t('common.cancel')"
              @confirm="handleDelete(record.id)"
            >
              <a-button type="link" danger size="small">{{ t("common.delete") }}</a-button>
            </a-popconfirm>
            <a-tooltip v-else :title="t('systemConfig.builtInCannotDelete')">
              <a-button type="link" danger size="small" disabled>{{ t("common.delete") }}</a-button>
            </a-tooltip>
          </a-space>
        </template>
      </template>
    </a-table>

    <!-- 新增/编辑弹窗 -->
    <a-modal
      v-model:open="modalVisible"
      :title="editTarget ? t('systemConfig.edit') : t('systemConfig.create')"
      :confirm-loading="modalLoading"
      @ok="submitForm"
      @cancel="closeModal"
    >
      <a-form :model="form" layout="vertical" :rules="rules" ref="formRef">
        <a-form-item :label="t('systemConfig.key')" name="configKey">
          <a-input
            v-model:value="form.configKey"
            :disabled="!!editTarget"
            :placeholder="t('systemConfig.keyPlaceholder')"
          />
        </a-form-item>
        <a-form-item :label="t('systemConfig.name')" name="configName">
          <a-input v-model:value="form.configName" :placeholder="t('systemConfig.namePlaceholder')" />
        </a-form-item>
        <a-form-item :label="t('systemConfig.value')" name="configValue">
          <a-textarea v-model:value="form.configValue" :rows="3" :placeholder="t('systemConfig.valuePlaceholder')" />
        </a-form-item>
        <a-form-item :label="t('systemConfig.remark')" name="remark">
          <a-textarea v-model:value="form.remark" :rows="2" :placeholder="t('systemConfig.remarkPlaceholder')" />
        </a-form-item>
      </a-form>
    </a-modal>
  </a-card>
</template>

<script setup lang="ts">
import { ref, reactive, onMounted, onUnmounted } from "vue";

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { message } from "ant-design-vue";
import { LockOutlined } from "@ant-design/icons-vue";
import { useI18n } from "vue-i18n";
import type { TablePaginationConfig } from "ant-design-vue";
import {
  getSystemConfigsPaged,
  createSystemConfig,
  updateSystemConfig,
  deleteSystemConfig,
  type SystemConfigDto
} from "@/services/system-config";

const { t } = useI18n();

const keyword = ref("");
const dataList = ref<SystemConfigDto[]>([]);
const loading = ref(false);
const pagination = reactive<TablePaginationConfig>({
  current: 1,
  pageSize: 20,
  total: 0,
  showSizeChanger: true,
  pageSizeOptions: ["10", "20", "50"]
});

const columns = [
  { title: t("systemConfig.colKey"), dataIndex: "configKey", key: "configKey", ellipsis: true },
  { title: t("systemConfig.colName"), dataIndex: "configName", key: "configName" },
  { title: t("systemConfig.colValue"), dataIndex: "configValue", key: "configValue", ellipsis: true },
  { title: t("systemConfig.colType"), key: "isBuiltIn", width: 90 },
  { title: t("systemConfig.colRemark"), dataIndex: "remark", key: "remark", ellipsis: true },
  { title: t("systemConfig.colActions"), key: "actions", width: 140, fixed: "right" as const }
];

async function loadConfigs() {
  loading.value = true;
  try {
    const result  = await getSystemConfigsPaged({
      pageIndex: pagination.current ?? 1,
      pageSize: pagination.pageSize ?? 20,
      keyword: keyword.value || undefined
    });

    if (!isMounted.value) return;
    dataList.value = result.items as SystemConfigDto[];
    pagination.total = Number(result.total);
  } catch (e: unknown) {
    message.error((e instanceof Error ? e.message : undefined) || t("systemConfig.loadFailed"));
  } finally {
    loading.value = false;
  }
}

function handleReset() {
  keyword.value = "";
  pagination.current = 1;
  loadConfigs();
}

function onTableChange(pag: TablePaginationConfig) {
  pagination.current = pag.current ?? 1;
  pagination.pageSize = pag.pageSize ?? 20;
  loadConfigs();
}

// ── 弹窗 ─────────────────────────────────────────────────────────────────────
const modalVisible = ref(false);
const modalLoading = ref(false);
const editTarget = ref<SystemConfigDto | null>(null);
const formRef = ref();
const form = reactive({ configKey: "", configName: "", configValue: "", remark: "" });

const rules = {
  configKey: [
    { required: true, message: t("systemConfig.keyRequired") },
    { pattern: /^[a-zA-Z][a-zA-Z0-9_.]{0,127}$/, message: t("systemConfig.keyPattern") }
  ],
  configName: [{ required: true, message: t("systemConfig.nameRequired") }],
  configValue: [{ required: true, message: t("systemConfig.valueRequired") }]
};

function openCreate() {
  editTarget.value = null;
  Object.assign(form, { configKey: "", configName: "", configValue: "", remark: "" });
  modalVisible.value = true;
}

function openEdit(item: SystemConfigDto) {
  editTarget.value = item;
  Object.assign(form, {
    configKey: item.configKey,
    configName: item.configName,
    configValue: item.configValue,
    remark: item.remark || ""
  });
  modalVisible.value = true;
}

function closeModal() {
  modalVisible.value = false;
  formRef.value?.resetFields();
}

async function submitForm() {
  try {
    await formRef.value?.validate();

    if (!isMounted.value) return;
  } catch {
    return;
  }
  modalLoading.value = true;
  try {
    if (editTarget.value) {
      await updateSystemConfig(editTarget.value.id, {
        configValue: form.configValue,
        configName: form.configName,
        remark: form.remark || undefined
      });

      if (!isMounted.value) return;
      message.success(t("systemConfig.updateSuccess"));
    } else {
      await createSystemConfig({
        configKey: form.configKey,
        configValue: form.configValue,
        configName: form.configName,
        remark: form.remark || undefined
      });

      if (!isMounted.value) return;
      message.success(t("systemConfig.createSuccess"));
    }
    modalVisible.value = false;
    loadConfigs();
  } catch (e: unknown) {
    message.error((e instanceof Error ? e.message : undefined) || t("systemConfig.operationFailed"));
  } finally {
    modalLoading.value = false;
  }
}

async function handleDelete(id: string) {
  try {
    await deleteSystemConfig(id);

    if (!isMounted.value) return;
    message.success(t("systemConfig.deleteSuccess"));
    loadConfigs();
  } catch (e: unknown) {
    message.error((e instanceof Error ? e.message : undefined) || t("systemConfig.deleteFailed"));
  }
}

onMounted(() => {
  loadConfigs();
});
</script>

<style scoped>
.crud-toolbar {
  margin-bottom: 16px;
}
</style>



