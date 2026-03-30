<template>
  <a-card :title="t('datasource.title')" class="page-card">
    <div class="toolbar">
      <a-space>
        <a-button type="primary" :disabled="!canManage" @click="openCreate">{{ t("datasource.create") }}</a-button>
        <a-button :loading="loading" @click="loadData">{{ t("common.refresh") }}</a-button>
        <a-button :disabled="!canViewMigration" @click="goAppMigrations">{{ t("route.consoleAppDbMigrations") }}</a-button>
      </a-space>
    </div>

    <a-table
      :columns="columns"
      :data-source="items"
      :loading="loading"
      row-key="id"
      :pagination="false"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'isActive'">
          <a-tag :color="record.isActive ? 'green' : 'default'">{{ record.isActive ? "启用" : "停用" }}</a-tag>
        </template>
        <template v-else-if="column.key === 'updatedAt'">
          {{ record.updatedAt ? formatDateTime(record.updatedAt) : "-" }}
        </template>
        <template v-else-if="column.key === 'lastTestSuccess'">
          <a-tag
            :color="record.lastTestSuccess === true ? 'green' : record.lastTestSuccess === false ? 'red' : 'default'"
          >
            {{ record.lastTestSuccess === true ? "成功" : record.lastTestSuccess === false ? "失败" : "-" }}
          </a-tag>
        </template>
        <template v-else-if="column.key === 'lastTestedAt'">
          {{ record.lastTestedAt ? formatDateTime(record.lastTestedAt) : "-" }}
        </template>
        <template v-else-if="column.key === 'actions'">
          <a-space>
            <a-button type="link" :disabled="!canManage || testingById[record.id]" @click="handleTestById(record)">
              {{ t("datasource.testConnection") }}
            </a-button>
            <a-button type="link" :disabled="!canManage" @click="openPreview(record)">SQL预览</a-button>
            <a-button type="link" :disabled="!canManage" @click="openEdit(record)">{{ t("common.edit") }}</a-button>
            <a-popconfirm
              :title="t('datasource.deleteConfirm')"
              :ok-text="t('common.delete')"
              :cancel-text="t('common.cancel')"
              @confirm="handleDelete(record.id)"
            >
              <a-button type="link" danger :disabled="!canManage">{{ t("common.delete") }}</a-button>
            </a-popconfirm>
          </a-space>
        </template>
      </template>
    </a-table>
  </a-card>

  <a-drawer
    v-model:open="formVisible"
      :title="formMode === 'create' ? t('datasource.create') : t('datasource.edit')"
    placement="right"
    :width="760"
    :destroy-on-close="true"
    @close="closeForm"
  >
    <a-alert
      v-if="formMode === 'edit'"
      type="info"
      show-icon
      :message="t('datasource.updateHint')"
      style="margin-bottom: 16px"
    />
    <a-form ref="formRef" :model="formModel" :rules="formRules" layout="vertical" size="small" class="datasource-form">
      <a-row :gutter="16">
        <a-col :span="12">
          <a-form-item :label="t('datasource.tenantId')" name="tenantIdValue">
            <a-input v-model:value="formModel.tenantIdValue" size="small" :disabled="formMode === 'edit'" />
          </a-form-item>
        </a-col>
        <a-col :span="12">
          <a-form-item :label="t('datasource.name')" name="name">
            <a-input v-model:value="formModel.name" size="small" />
          </a-form-item>
        </a-col>
        <a-col :span="12">
          <a-form-item :label="t('datasource.dbType')" name="dbType">
            <a-select v-model:value="formModel.dbType" size="small" :options="dbTypeOptions" />
          </a-form-item>
        </a-col>
        <a-col :span="12">
          <a-form-item label="连接配置模式" name="mode">
            <a-radio-group v-model:value="formModel.mode" size="small">
              <a-radio-button value="visual">可视化配置</a-radio-button>
              <a-radio-button value="raw">连接字符串</a-radio-button>
            </a-radio-group>
          </a-form-item>
        </a-col>
      </a-row>

      <a-alert
        v-if="currentDriver"
        type="info"
        show-icon
        style="margin-bottom: 12px"
        :message="`Driver: ${currentDriver.displayName}`"
        :description="`示例: ${currentDriver.connectionStringExample}`"
      />

      <template v-if="formModel.mode === 'visual'">
        <a-row :gutter="16">
          <a-col
            v-for="field in visualFields"
            :key="field.key"
            :span="field.multiline || field.inputType === 'textarea' ? 24 : 12"
          >
            <a-form-item
              :label="field.label"
              :name="['visualConfig', field.key]"
              :required="field.required"
            >
              <a-textarea
                v-if="field.multiline || field.inputType === 'textarea'"
                :value="getVisualValue(field.key)"
                :rows="2"
                :placeholder="field.placeholder || ''"
                @update:value="setVisualValue(field.key, $event)"
              />
              <a-input
                v-else
                :type="field.secret ? 'password' : 'text'"
                :value="getVisualValue(field.key)"
                size="small"
                :placeholder="field.placeholder || ''"
                @update:value="setVisualValue(field.key, $event)"
              />
            </a-form-item>
          </a-col>
        </a-row>
      </template>
      <a-form-item v-else :label="t('datasource.connectionString')" name="connectionString">
        <a-textarea
          v-model:value="formModel.connectionString"
          :rows="3"
          :placeholder="currentDriver?.connectionStringExample || t('datasource.connectionString')"
        />
      </a-form-item>

      <a-divider style="margin: 8px 0 16px 0" />

      <a-row :gutter="16">
        <a-col :span="12">
          <a-form-item :label="t('datasource.maxPoolSize')">
            <a-input-number v-model:value="formModel.maxPoolSize" size="small" :min="1" :max="500" style="width: 100%" />
          </a-form-item>
        </a-col>
        <a-col :span="12">
          <a-form-item :label="t('datasource.connectionTimeoutSeconds')">
            <a-input-number
              v-model:value="formModel.connectionTimeoutSeconds"
              size="small"
              :min="1"
              :max="120"
              style="width: 100%"
            />
          </a-form-item>
        </a-col>
      </a-row>
    </a-form>
    <template #footer>
      <a-space>
        <a-button :loading="testing" @click="handleTestConnection">{{ t("datasource.testConnection") }}</a-button>
        <a-button @click="closeForm">{{ t("common.cancel") }}</a-button>
        <a-button type="primary" :loading="saving" :disabled="!canManage" @click="submitForm">{{ t("common.save") }}</a-button>
      </a-space>
    </template>
  </a-drawer>

  <advanced-data-preview-drawer ref="previewDrawerRef" />
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, onUnmounted, watch } from "vue";

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import type { FormInstance, Rule } from "ant-design-vue/es/form";
import { message } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import { useRouter } from "vue-router";
import {
  createTenantDataSource,
  deleteTenantDataSource,
  getTenantDataSources,
  getTenantDataSourceDrivers,
  testTenantDataSourceConnectionById,
  testTenantDataSourceConnection,
  updateTenantDataSource
} from "@/services/api";
import type {
  DataSourceDriverDefinition,
  TenantDataSourceDto,
  TenantDataSourceCreateRequest,
  TenantDataSourceUpdateRequest
} from "@/types/api";
import { getAuthProfile, getTenantId, hasPermission } from "@/utils/auth";
import AdvancedDataPreviewDrawer from "./AdvancedDataPreviewDrawer.vue";

const profile = getAuthProfile();
const canManage = computed(() => hasPermission(profile, "system:admin"));
const canViewMigration = computed(() => hasPermission(profile, "apps:view"));
const { t } = useI18n();
const router = useRouter();

const loading = ref(false);
const saving = ref(false);
const testing = ref(false);
const items = ref<TenantDataSourceDto[]>([]);
const driverDefinitions = ref<DataSourceDriverDefinition[]>([]);

const formVisible = ref(false);
const formMode = ref<"create" | "edit">("create");
const editingId = ref<string>("");
const formRef = ref<FormInstance>();
const previewDrawerRef = ref<InstanceType<typeof AdvancedDataPreviewDrawer>>();

const formModel = reactive({
  tenantIdValue: getTenantId() ?? "",
  name: "",
  dbType: "SQLite",
  mode: "raw" as "raw" | "visual",
  connectionString: "",
  visualConfig: {} as Record<string, string>,
  maxPoolSize: 50,
  connectionTimeoutSeconds: 15
});

const dbTypeOptions = computed(() => {
  if (driverDefinitions.value.length === 0) {
    return [{ label: "SQLite", value: "SQLite" }];
  }
  return driverDefinitions.value.map((item) => ({ label: item.displayName, value: item.code }));
});

const currentDriver = computed(() =>
  driverDefinitions.value.find((item) => item.code === formModel.dbType) ?? null
);

const visualFields = computed(() => {
  if (formModel.mode !== "visual" || !currentDriver.value?.supportsVisual) {
    return [];
  }
  return currentDriver.value.fields;
});

const formRules: Record<string, Rule[]> = {
  tenantIdValue: [{ required: true, message: "请输入租户ID" }],
  name: [{ required: true, message: "请输入数据源名称" }],
  dbType: [{ required: true, message: "请选择数据库类型" }],
  connectionString: [{
    validator: async (_, value: string) => {
      if (formModel.mode === "visual") {
        for (const field of visualFields.value) {
          if (field.required && !String(formModel.visualConfig[field.key] ?? "").trim()) {
            throw new Error(`请填写 ${field.label}`);
          }
        }
        return;
      }
      if (formMode.value === "create" && !value?.trim()) {
        throw new Error("请输入连接字符串");
      }
    }
  }]
};

const columns = [
  { title: "名称", dataIndex: "name", key: "name" },
  { title: "租户ID", dataIndex: "tenantIdValue", key: "tenantIdValue" },
  { title: "数据库类型", dataIndex: "dbType", key: "dbType" },
  { title: "最近测试", key: "lastTestSuccess" },
  { title: "测试时间", key: "lastTestedAt" },
  { title: "状态", key: "isActive" },
  { title: "更新时间", key: "updatedAt" },
  { title: "操作", key: "actions" }
];
const testingById = reactive<Record<string, boolean>>({});

const loadData = async () => {
  loading.value = true;
  try {
    items.value = await getTenantDataSources();

    if (!isMounted.value) return;
  } catch (error) {
    message.error(error instanceof Error ? error.message : t("datasource.loadFailed"));
  } finally {
    loading.value = false;
  }
};

const loadDriverDefinitions = async () => {
  try {
    driverDefinitions.value = await getTenantDataSourceDrivers();
    if (driverDefinitions.value.length > 0 && !driverDefinitions.value.some((x) => x.code === formModel.dbType)) {
      formModel.dbType = driverDefinitions.value[0].code;
    }
  } catch (error) {
    message.error(error instanceof Error ? error.message : "加载数据源驱动失败");
  }
};

const resetFormModel = () => {
  formModel.tenantIdValue = getTenantId() ?? "";
  formModel.name = "";
  formModel.dbType = "SQLite";
  formModel.mode = "raw";
  formModel.connectionString = "";
  formModel.visualConfig = {};
  formModel.maxPoolSize = 50;
  formModel.connectionTimeoutSeconds = 15;
};

const openPreview = (record: TenantDataSourceDto) => {
  previewDrawerRef.value?.open(record);
};

const openCreate = () => {
  formMode.value = "create";
  editingId.value = "";
  resetFormModel();
  formVisible.value = true;
};

const openEdit = (record: TenantDataSourceDto) => {
  formMode.value = "edit";
  editingId.value = record.id;
  formModel.tenantIdValue = record.tenantIdValue;
  formModel.name = record.name;
  formModel.dbType = record.dbType;
  formModel.mode = "raw";
  formModel.connectionString = "";
  formModel.visualConfig = {};
  formModel.maxPoolSize = record.maxPoolSize ?? 50;
  formModel.connectionTimeoutSeconds = record.connectionTimeoutSeconds ?? 15;
  formVisible.value = true;
};

const closeForm = () => {
  formVisible.value = false;
};

const handleTestConnection = async () => {
  const valid  = await formRef.value?.validate().catch(() => false);

  if (!isMounted.value) return;
  if (!valid) return;

  testing.value = true;
  try {
    const result = formMode.value === "edit" && !formModel.connectionString.trim() && editingId.value
      ? await testTenantDataSourceConnectionById(editingId.value)
      : await testTenantDataSourceConnection({
          connectionString: formModel.connectionString,
          dbType: formModel.dbType,
          mode: formModel.mode,
          visualConfig: formModel.mode === "visual" ? formModel.visualConfig : undefined
        });
    if (result.success) {
      message.success(result.latencyMs ? `${t("datasource.testSuccess")}（${result.latencyMs}ms）` : t("datasource.testSuccess"));
      await loadData();

      if (!isMounted.value) return;
      return;
    }
    const suffix = result.latencyMs ? `（${result.latencyMs}ms）` : "";
    message.error((result.errorMessage || t("datasource.testFailed")) + suffix);
  } catch (error) {
    message.error(error instanceof Error ? error.message : t("datasource.testFailed"));
  } finally {
    testing.value = false;
  }
};

const submitForm = async () => {
  const valid  = await formRef.value?.validate().catch(() => false);

  if (!isMounted.value) return;
  if (!valid) return;

  saving.value = true;
  try {
    if (formMode.value === "create") {
      const payload: TenantDataSourceCreateRequest = {
        tenantIdValue: formModel.tenantIdValue,
        name: formModel.name,
        dbType: formModel.dbType,
        connectionString: formModel.connectionString,
        mode: formModel.mode,
        visualConfig: formModel.mode === "visual" ? formModel.visualConfig : undefined,
        maxPoolSize: formModel.maxPoolSize,
        connectionTimeoutSeconds: formModel.connectionTimeoutSeconds
      };
      await createTenantDataSource(payload);

      if (!isMounted.value) return;
      message.success(t("datasource.createSuccess"));
    } else {
      const payload: TenantDataSourceUpdateRequest = {
        name: formModel.name,
        dbType: formModel.dbType,
        connectionString: formModel.connectionString.trim() ? formModel.connectionString : undefined,
        mode: formModel.mode,
        visualConfig: formModel.mode === "visual" ? formModel.visualConfig : undefined,
        maxPoolSize: formModel.maxPoolSize,
        connectionTimeoutSeconds: formModel.connectionTimeoutSeconds
      };
      await updateTenantDataSource(editingId.value, payload);

      if (!isMounted.value) return;
      message.success(t("datasource.updateSuccess"));
    }
    formVisible.value = false;
    await loadData();

    if (!isMounted.value) return;
  } catch (error) {
    message.error(error instanceof Error ? error.message : t("datasource.saveFailed"));
  } finally {
    saving.value = false;
  }
};

const handleDelete = async (id: string) => {
  try {
    await deleteTenantDataSource(id);

    if (!isMounted.value) return;
    message.success(t("datasource.deleteSuccess"));
    await loadData();

    if (!isMounted.value) return;
  } catch (error) {
    message.error(error instanceof Error ? error.message : t("common.failed"));
  }
};

const goAppMigrations = () => {
  void router.push("/console/app-db-migrations");
};

const handleTestById = async (record: TenantDataSourceDto) => {
  if (!canManage.value || testingById[record.id]) {
    return;
  }
  testingById[record.id] = true;
  try {
    const result  = await testTenantDataSourceConnectionById(record.id);

    if (!isMounted.value) return;
    if (result.success) {
      message.success(result.latencyMs ? `${t("datasource.testSuccess")}（${result.latencyMs}ms）` : t("datasource.testSuccess"));
    } else {
      const suffix = result.latencyMs ? `（${result.latencyMs}ms）` : "";
      message.error((result.errorMessage || t("datasource.testFailed")) + suffix);
    }
    await loadData();

    if (!isMounted.value) return;
  } catch (error) {
    message.error(error instanceof Error ? error.message : t("datasource.testFailed"));
  } finally {
    testingById[record.id] = false;
  }
};

const formatDateTime = (value: string) => {
  return new Date(value).toLocaleString("zh-CN");
};

const getVisualValue = (key: string) => String(formModel.visualConfig[key] ?? "");
const setVisualValue = (key: string, value: string) => {
  formModel.visualConfig = { ...formModel.visualConfig, [key]: value ?? "" };
};

watch(
  () => [formModel.dbType, formModel.mode],
  () => {
    if (formModel.mode !== "visual") {
      return;
    }
    const driver = currentDriver.value;
    if (!driver || !driver.supportsVisual) {
      formModel.mode = "raw";
      return;
    }
    const next: Record<string, string> = { ...formModel.visualConfig };
    for (const field of driver.fields) {
      if (!next[field.key] && field.defaultValue) {
        next[field.key] = field.defaultValue;
      }
    }
    formModel.visualConfig = next;
  },
  { immediate: true }
);

onMounted(() => {
  void loadDriverDefinitions();
  void loadData();
});
</script>

<style scoped>
.toolbar {
  margin-bottom: 16px;
  display: flex;
  justify-content: space-between;
}

.datasource-form :deep(.ant-form-item) {
  margin-bottom: 10px;
}
</style>
