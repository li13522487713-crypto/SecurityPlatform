<template>
  <a-card :title="t('datasource.title')" class="page-card">
    <div class="toolbar">
      <a-space>
        <a-button type="primary" :disabled="!canManage" @click="openCreate">{{ t("datasource.create") }}</a-button>
        <a-button :loading="loading" @click="loadData">{{ t("common.refresh") }}</a-button>
        <a-button :disabled="!canViewMigration" @click="goAppMigrations">{{ t("route.consoleAppDbMigrations") }}</a-button>
      </a-space>
    </div>

    <a-table :columns="columns" :data-source="items" :loading="loading" row-key="id" :pagination="false">
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'isActive'">
          <a-tag :color="record.isActive ? 'green' : 'default'">
            {{ record.isActive ? t("datasource.statusEnabled") : t("datasource.statusDisabled") }}
          </a-tag>
        </template>
        <template v-else-if="column.key === 'updatedAt'">
          {{ record.updatedAt ? formatDateTime(record.updatedAt) : "-" }}
        </template>
        <template v-else-if="column.key === 'lastTestSuccess'">
          <a-tag :color="testResultColor(record.lastTestSuccess)">
            {{ testResultText(record.lastTestSuccess) }}
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
            <a-button type="link" :disabled="!canManage" @click="openPreview(record)">
              {{ t("datasource.sqlPreview") }}
            </a-button>
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
          <a-form-item :label="t('datasource.modeLabel')" name="mode">
            <a-radio-group v-model:value="formModel.mode" size="small">
              <a-radio-button value="visual">{{ t("datasource.modeVisual") }}</a-radio-button>
              <a-radio-button value="raw">{{ t("datasource.modeRaw") }}</a-radio-button>
            </a-radio-group>
          </a-form-item>
        </a-col>
      </a-row>

      <a-alert
        v-if="currentDriver"
        type="info"
        show-icon
        style="margin-bottom: 12px"
        :message="t('datasource.driverInfo', { name: currentDriver.displayName })"
        :description="t('datasource.driverExample', { example: currentDriver.connectionStringExample })"
      />

      <template v-if="formModel.mode === 'visual'">
        <a-row :gutter="16">
          <a-col
            v-for="field in visualFields"
            :key="field.key"
            :span="field.multiline || field.inputType === 'textarea' ? 24 : 12"
          >
            <a-form-item :label="field.label" :name="['visualConfig', field.key]" :required="field.required">
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
import { computed, onMounted, onUnmounted, reactive, ref, watch } from "vue";
import type { Rule } from "ant-design-vue/es/form";
import type { FormInstance } from "ant-design-vue/es/form";
import { message } from "ant-design-vue";
import {
  getAuthProfile,
  getTenantId,
  hasPermission,
  type DataSourceDriverDefinition,
  type TenantDataSourceCreateRequest,
  type TenantDataSourceDto,
  type TenantDataSourceUpdateRequest
} from "@atlas/shared-core";
import { useI18n } from "vue-i18n";
import { useRouter } from "vue-router";
import AdvancedDataPreviewDrawer from "./AdvancedDataPreviewDrawer.vue";
import {
  createTenantDataSource,
  deleteTenantDataSource,
  getTenantDataSourceDrivers,
  getTenantDataSources,
  testTenantDataSourceConnection,
  testTenantDataSourceConnectionById,
  updateTenantDataSource
} from "@/services/api-datasource";

const profile = getAuthProfile();
const canManage = computed(() => hasPermission(profile, "system:admin"));
const canViewMigration = computed(() => hasPermission(profile, "apps:view"));

const { t, locale } = useI18n();
const router = useRouter();

const isMounted = ref(false);
const loading = ref(false);
const saving = ref(false);
const testing = ref(false);
const items = ref<TenantDataSourceDto[]>([]);
const driverDefinitions = ref<DataSourceDriverDefinition[]>([]);

const formVisible = ref(false);
const formMode = ref<"create" | "edit">("create");
const editingId = ref("");
const formRef = ref<FormInstance>();
const previewDrawerRef = ref<InstanceType<typeof AdvancedDataPreviewDrawer> | null>(null);

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

const columns = computed(() => [
  { title: t("datasource.name"), dataIndex: "name", key: "name" },
  { title: t("datasource.tenantId"), dataIndex: "tenantIdValue", key: "tenantIdValue" },
  { title: t("datasource.dbType"), dataIndex: "dbType", key: "dbType" },
  { title: t("datasource.colLastTest"), key: "lastTestSuccess" },
  { title: t("datasource.colLastTestedAt"), key: "lastTestedAt" },
  { title: t("datasource.colStatus"), key: "isActive" },
  { title: t("datasource.colUpdatedAt"), key: "updatedAt" },
  { title: t("datasource.colActions"), key: "actions" }
]);

const testingById = reactive<Record<string, boolean>>({});

const formRules: Record<string, Rule[]> = {
  tenantIdValue: [{ required: true, message: t("datasource.tenantIdRequired") }],
  name: [{ required: true, message: t("datasource.nameRequired") }],
  dbType: [{ required: true, message: t("datasource.dbTypeRequired") }],
  connectionString: [
    {
      validator: async (_, value: string) => {
        if (formModel.mode === "visual") {
          for (const field of visualFields.value) {
            if (field.required && !String(formModel.visualConfig[field.key] ?? "").trim()) {
              throw new Error(t("datasource.fieldRequired", { field: field.label }));
            }
          }
          return;
        }

        if (formMode.value === "create" && !value?.trim()) {
          throw new Error(t("datasource.connectionStringRequired"));
        }
      }
    }
  ]
};

function formatDateTime(value: string) {
  if (!value) {
    return "-";
  }

  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }
  return date.toLocaleString(locale.value);
}

function testResultColor(result?: boolean) {
  if (result === true) {
    return "green";
  }
  if (result === false) {
    return "red";
  }
  return "default";
}

function testResultText(result?: boolean) {
  if (result === true) {
    return t("datasource.testOk");
  }
  if (result === false) {
    return t("datasource.testFail");
  }
  return t("datasource.testUnknown");
}

function getVisualValue(key: string) {
  return String(formModel.visualConfig[key] ?? "");
}

function setVisualValue(key: string, value: string) {
  formModel.visualConfig = { ...formModel.visualConfig, [key]: value ?? "" };
}

function resetFormModel() {
  formModel.tenantIdValue = getTenantId() ?? "";
  formModel.name = "";
  formModel.dbType = "SQLite";
  formModel.mode = "raw";
  formModel.connectionString = "";
  formModel.visualConfig = {};
  formModel.maxPoolSize = 50;
  formModel.connectionTimeoutSeconds = 15;
}

async function loadData() {
  loading.value = true;
  try {
    items.value = await getTenantDataSources();
  } catch (error) {
    if (isMounted.value) {
      message.error(error instanceof Error ? error.message : t("datasource.loadFailed"));
    }
  } finally {
    loading.value = false;
  }
}

async function loadDriverDefinitions() {
  try {
    driverDefinitions.value = await getTenantDataSourceDrivers();
    if (driverDefinitions.value.length > 0 && !driverDefinitions.value.some((item) => item.code === formModel.dbType)) {
      formModel.dbType = driverDefinitions.value[0].code;
    }
  } catch (error) {
    if (isMounted.value) {
      message.error(error instanceof Error ? error.message : t("datasource.loadDriversFailed"));
    }
  }
}

function openPreview(record: TenantDataSourceDto) {
  previewDrawerRef.value?.open(record);
}

function openCreate() {
  formMode.value = "create";
  editingId.value = "";
  resetFormModel();
  formVisible.value = true;
}

function openEdit(record: TenantDataSourceDto) {
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
}

function closeForm() {
  formVisible.value = false;
}

async function handleTestConnection() {
  const valid = await formRef.value?.validate().catch(() => false);
  if (!valid) {
    return;
  }

  testing.value = true;
  try {
    const result =
      formMode.value === "edit" && !formModel.connectionString.trim() && editingId.value
        ? await testTenantDataSourceConnectionById(editingId.value)
        : await testTenantDataSourceConnection({
            connectionString: formModel.connectionString,
            dbType: formModel.dbType,
            mode: formModel.mode,
            visualConfig: formModel.mode === "visual" ? formModel.visualConfig : undefined
          });

    if (result.success) {
      message.success(
        result.latencyMs
          ? t("datasource.testWithLatency", { latency: result.latencyMs })
          : t("datasource.testSuccess")
      );
      await loadData();
      return;
    }

    message.error(
      result.errorMessage
        || (result.latencyMs
          ? t("datasource.testFailedWithLatency", { latency: result.latencyMs })
          : t("datasource.testFailed"))
    );
  } catch (error) {
    message.error(error instanceof Error ? error.message : t("datasource.testFailed"));
  } finally {
    testing.value = false;
  }
}

async function submitForm() {
  const valid = await formRef.value?.validate().catch(() => false);
  if (!valid) {
    return;
  }

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
      message.success(t("datasource.updateSuccess"));
    }

    formVisible.value = false;
    await loadData();
  } catch (error) {
    message.error(error instanceof Error ? error.message : t("datasource.saveFailed"));
  } finally {
    saving.value = false;
  }
}

async function handleDelete(id: string) {
  try {
    await deleteTenantDataSource(id);
    message.success(t("datasource.deleteSuccess"));
    await loadData();
  } catch (error) {
    message.error(error instanceof Error ? error.message : t("common.failed"));
  }
}

async function handleTestById(record: TenantDataSourceDto) {
  if (!canManage.value || testingById[record.id]) {
    return;
  }

  testingById[record.id] = true;
  try {
    const result = await testTenantDataSourceConnectionById(record.id);
    if (result.success) {
      message.success(
        result.latencyMs
          ? t("datasource.testWithLatency", { latency: result.latencyMs })
          : t("datasource.testSuccess")
      );
    } else {
      message.error(
        result.errorMessage
          || (result.latencyMs
            ? t("datasource.testFailedWithLatency", { latency: result.latencyMs })
            : t("datasource.testFailed"))
      );
    }
    await loadData();
  } catch (error) {
    message.error(error instanceof Error ? error.message : t("datasource.testFailed"));
  } finally {
    testingById[record.id] = false;
  }
}

function goAppMigrations() {
  void router.push("/console/migration-governance");
}

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
  isMounted.value = true;
  void loadDriverDefinitions();
  void loadData();
});

onUnmounted(() => {
  isMounted.value = false;
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
