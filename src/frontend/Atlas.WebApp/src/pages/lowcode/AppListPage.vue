<template>
  <div class="app-list-page">
    <div class="page-header">
      <div class="page-header-left">
        <h2>{{ t("lowcodeApp.listTitle") }}</h2>
        <a-input-search
          v-model:value="keyword"
          :placeholder="t('lowcodeApp.searchPlaceholder')"
          allow-clear
          style="width: 260px"
          @search="fetchData"
        />
      </div>
      <div class="page-header-right">
        <a-button v-if="canManageApps" @click="handleImportClick">导入应用</a-button>
        <a-button v-if="canManageApps" type="primary" @click="openCreateWizard">新建应用</a-button>
      </div>
    </div>

    <div v-if="!loading" class="app-grid">
      <div v-for="app in dataSource" :key="app.id" class="app-card" @click="handleOpenApp(app.id)">
        <div class="app-card-icon">{{ app.icon || app.name.slice(0, 1) }}</div>
        <div class="app-card-content">
          <div class="app-card-name">{{ app.name }}</div>
          <div class="app-card-key">{{ app.appKey }}</div>
          <div class="app-card-desc">{{ app.description || t("lowcodeApp.noDescription") }}</div>
        </div>
        <div class="app-card-footer">
          <a-tag :color="statusColor(app.status)">{{ statusLabel(app.status) }}</a-tag>
          <span class="app-card-version">{{ t("lowcodeApp.versionPrefix", { version: app.version }) }}</span>
        </div>
        <div class="app-card-actions" @click.stop>
          <a-dropdown v-if="canManageApps" trigger="click">
            <a-button type="text" size="small">...</a-button>
            <template #overlay>
              <a-menu>
                <a-menu-item key="edit" @click="openEditDialog(app)">编辑</a-menu-item>
                <a-menu-item key="export" @click="handleExport(app)">导出</a-menu-item>
                <a-menu-item v-if="app.status === 'Draft'" key="publish" @click="handlePublish(app.id)">发布</a-menu-item>
                <a-menu-item key="delete" danger @click="handleDelete(app.id)">删除</a-menu-item>
              </a-menu>
            </template>
          </a-dropdown>
        </div>
      </div>

      <div v-if="canManageApps" class="app-card app-card-new" @click="openCreateWizard">
        <div class="app-card-new-icon">+</div>
        <div class="app-card-new-text">{{ t("lowcodeApp.newCardText") }}</div>
      </div>
    </div>
    <div v-else class="loading-container">
      <a-spin size="large" :tip="t('lowcodeApp.loadingTip')" />
    </div>

    <a-modal
      v-model:open="editVisible"
      title="编辑应用"
      :confirm-loading="savingEdit"
      @ok="submitEdit"
    >
      <a-form layout="vertical">
        <a-form-item :label="t('lowcodeApp.appNameLabel')" required>
          <a-input v-model:value="editForm.name" />
        </a-form-item>
        <a-form-item :label="t('lowcodeApp.categoryLabel')">
          <a-input v-model:value="editForm.category" />
        </a-form-item>
        <a-form-item :label="t('lowcodeApp.descriptionLabel')">
          <a-textarea v-model:value="editForm.description" :rows="3" />
        </a-form-item>
        <a-form-item :label="t('lowcodeApp.iconLabel')">
          <a-input v-model:value="editForm.icon" />
        </a-form-item>
      </a-form>
    </a-modal>

    <a-modal
      v-model:open="wizardVisible"
      title="新建应用"
      width="920px"
      :mask-closable="false"
      @cancel="closeWizard"
    >
      <a-steps :current="wizardStep" style="margin-bottom: 20px;">
        <a-step title="基本信息" />
        <a-step title="数据源绑定" />
        <a-step title="共享策略与别名" />
      </a-steps>

      <template v-if="wizardStep === 0">
        <a-form layout="vertical">
          <a-form-item label="应用标识(AppKey)" required>
            <a-input v-model:value="wizardBasic.appKey" placeholder="如 crm_app" />
          </a-form-item>
          <a-form-item label="应用名称" required>
            <a-input v-model:value="wizardBasic.name" placeholder="请输入应用名称" />
          </a-form-item>
          <a-form-item label="描述">
            <a-textarea v-model:value="wizardBasic.description" :rows="3" />
          </a-form-item>
          <a-form-item label="分类">
            <a-input v-model:value="wizardBasic.category" />
          </a-form-item>
          <a-form-item label="图标">
            <a-input v-model:value="wizardBasic.icon" />
          </a-form-item>
        </a-form>
      </template>

      <template v-else-if="wizardStep === 1">
        <a-alert
          type="warning"
          show-icon
          message="⚠️ 数据源绑定后不可更改，请谨慎选择"
          style="margin-bottom: 16px"
        />
        <a-radio-group v-model:value="datasourceMode" @change="onDatasourceModeChange">
          <a-radio value="platform">平台默认</a-radio>
          <a-radio value="existing">选择已有数据源</a-radio>
          <a-radio value="new">创建新数据源</a-radio>
        </a-radio-group>

        <div v-if="datasourceMode === 'existing'" style="margin-top: 16px">
          <a-select
            v-model:value="selectedDatasourceId"
            show-search
            :filter-option="false"
            :options="datasourceOptions"
            :loading="datasourceLoading"
            placeholder="搜索并选择已有数据源"
            style="width: 100%"
            @search="loadDatasourceOptions"
            @focus="() => loadDatasourceOptions()"
          />
        </div>

        <div v-if="datasourceMode === 'new'" style="margin-top: 16px">
          <a-form layout="vertical">
            <a-form-item label="数据源名称" required>
              <a-input v-model:value="newDatasource.name" />
            </a-form-item>
            <a-form-item label="数据库类型" required>
              <a-select v-model:value="newDatasource.dbType">
                <a-select-option value="SQLite">SQLite</a-select-option>
                <a-select-option value="SqlServer">SQL Server</a-select-option>
                <a-select-option value="MySql">MySQL</a-select-option>
                <a-select-option value="PostgreSql">PostgreSQL</a-select-option>
              </a-select>
            </a-form-item>

            <template v-if="newDatasource.dbType === 'SQLite'">
              <a-form-item label="数据库文件路径" required>
                <a-input v-model:value="newDatasource.filePath" placeholder="如 /data/app.db" />
              </a-form-item>
            </template>
            <template v-else>
              <a-row :gutter="12">
                <a-col :span="16">
                  <a-form-item label="服务器" required>
                    <a-input v-model:value="newDatasource.server" />
                  </a-form-item>
                </a-col>
                <a-col :span="8">
                  <a-form-item label="端口" required>
                    <a-input v-model:value="newDatasource.port" />
                  </a-form-item>
                </a-col>
              </a-row>
              <a-form-item label="数据库名" required>
                <a-input v-model:value="newDatasource.database" />
              </a-form-item>
              <a-row :gutter="12">
                <a-col :span="12">
                  <a-form-item label="用户名" required>
                    <a-input v-model:value="newDatasource.username" />
                  </a-form-item>
                </a-col>
                <a-col :span="12">
                  <a-form-item label="密码" required>
                    <a-input-password v-model:value="newDatasource.password" />
                  </a-form-item>
                </a-col>
              </a-row>
            </template>
          </a-form>

          <a-space>
            <a-button :loading="testingDatasource" @click="handleTestNewDatasource">测试连接</a-button>
            <a-tag :color="newDatasourceTestPassed ? 'green' : 'default'">{{ newDatasourceTestPassed ? "已通过" : "未测试" }}</a-tag>
          </a-space>
        </div>
      </template>

      <template v-else>
        <a-form layout="vertical">
          <a-form-item label="用户账号来源">
            <a-switch v-model:checked="sharingPolicy.useSharedUsers" />
            <span class="switch-label">{{ sharingPolicy.useSharedUsers ? "继承平台" : "应用独立" }}</span>
          </a-form-item>
          <a-form-item label="角色权限来源">
            <a-switch v-model:checked="sharingPolicy.useSharedRoles" />
            <span class="switch-label">{{ sharingPolicy.useSharedRoles ? "继承平台" : "应用独立" }}</span>
          </a-form-item>
          <a-form-item label="部门组织来源">
            <a-switch v-model:checked="sharingPolicy.useSharedDepartments" />
            <span class="switch-label">{{ sharingPolicy.useSharedDepartments ? "继承平台" : "应用独立" }}</span>
          </a-form-item>
        </a-form>

        <a-table :data-source="aliases" row-key="entityType" :pagination="false" size="small">
          <a-table-column title="实体类型" data-index="entityType" key="entityType" />
          <a-table-column title="单数别名" key="singularAlias">
            <template #default="{ record }">
              <a-input v-model:value="record.singularAlias" />
            </template>
          </a-table-column>
          <a-table-column title="复数别名" key="pluralAlias">
            <template #default="{ record }">
              <a-input v-model:value="record.pluralAlias" />
            </template>
          </a-table-column>
        </a-table>
      </template>

      <template #footer>
        <a-space>
          <a-button @click="closeWizard">取消</a-button>
          <a-button v-if="wizardStep > 0" @click="wizardStep -= 1">上一步</a-button>
          <a-button
            type="primary"
            :loading="creating"
            @click="handleWizardPrimaryAction"
          >
            {{ wizardStep < 2 ? "下一步" : "创建应用" }}
          </a-button>
        </a-space>
      </template>
    </a-modal>

    <input
      ref="importInputRef"
      type="file"
      accept="application/json,.json"
      style="display: none"
      @change="handleImportFileChange"
    />
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from "vue";
import { useRouter } from "vue-router";
import { message } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import type { AppEntityAlias, LowCodeAppListItem } from "@/types/lowcode";
import type { TenantDataSourceDto } from "@/types/api";
import { getAuthProfile, getTenantId, hasPermission } from "@/utils/auth";
import {
  createTenantDataSource,
  getTenantDataSources,
  testTenantDataSourceConnection
} from "@/services/api";
import {
  createLowCodeApp,
  deleteLowCodeApp,
  exportLowCodeApp,
  getLowCodeAppsPaged,
  importLowCodeApp,
  parseLowCodeAppExportPackage,
  publishLowCodeApp,
  updateAppEntityAliases,
  updateLowCodeApp
} from "@/services/lowcode";

const router = useRouter();
const { t } = useI18n();
const canManageApps = hasPermission(getAuthProfile(), "apps:update");

const keyword = ref("");
const loading = ref(false);
const dataSource = ref<LowCodeAppListItem[]>([]);
const importing = ref(false);
const importInputRef = ref<HTMLInputElement | null>(null);

const editVisible = ref(false);
const savingEdit = ref(false);
const editingId = ref<string>();
const editForm = reactive({
  name: "",
  description: "",
  category: "",
  icon: ""
});

const wizardVisible = ref(false);
const wizardStep = ref(0);
const creating = ref(false);
const datasourceLoading = ref(false);
const testingDatasource = ref(false);
const newDatasourceTestPassed = ref(false);
const datasourceMode = ref<"platform" | "existing" | "new">("platform");
const selectedDatasourceId = ref<string>();
const datasourceOptions = ref<{ label: string; value: string }[]>([]);
// Tracks resources created in a previous (failed) attempt so retries can resume
// without creating orphaned duplicates.
const pendingDataSourceId = ref<string | undefined>(undefined);
const pendingAppId = ref<string | undefined>(undefined);

const wizardBasic = reactive({
  appKey: "",
  name: "",
  description: "",
  category: "",
  icon: ""
});

const sharingPolicy = reactive({
  useSharedUsers: true,
  useSharedRoles: true,
  useSharedDepartments: true
});

const aliases = ref<AppEntityAlias[]>([
  { entityType: "user", singularAlias: "用户", pluralAlias: "用户列表" },
  { entityType: "role", singularAlias: "角色", pluralAlias: "角色列表" },
  { entityType: "department", singularAlias: "部门", pluralAlias: "部门列表" }
]);

const newDatasource = reactive({
  name: "",
  dbType: "SQLite",
  filePath: "",
  server: "",
  port: "",
  database: "",
  username: "",
  password: ""
});

const statusColor = (status: string) => {
  const map: Record<string, string> = {
    Draft: "default",
    Published: "green",
    Disabled: "red",
    Archived: "gray"
  };
  return map[status] ?? "default";
};

const statusLabel = (status: string) => {
  const map: Record<string, string> = {
    Draft: t("lowcodeApp.statusDraft"),
    Published: t("lowcodeApp.statusPublished"),
    Disabled: t("lowcodeApp.statusDisabled"),
    Archived: t("lowcodeApp.statusArchived")
  };
  return map[status] ?? status;
};

const fetchData = async () => {
  loading.value = true;
  try {
    const result = await getLowCodeAppsPaged({
      pageIndex: 1,
      pageSize: 100,
      keyword: keyword.value || undefined
    });
    dataSource.value = result.items;
  } catch (error) {
    message.error((error as Error).message || t("lowcodeApp.queryFailed"));
  } finally {
    loading.value = false;
  }
};

const handleOpenApp = (id: string) => {
  if (!canManageApps) {
    message.warning("当前账号无应用编辑权限");
    return;
  }
  router.push(`/apps/${id}/dashboard`);
};

const openEditDialog = (app: LowCodeAppListItem) => {
  editingId.value = app.id;
  editForm.name = app.name;
  editForm.description = app.description ?? "";
  editForm.category = app.category ?? "";
  editForm.icon = app.icon ?? "";
  editVisible.value = true;
};

const submitEdit = async () => {
  if (!editingId.value || !editForm.name.trim()) {
    message.warning("请填写应用名称");
    return;
  }
  savingEdit.value = true;
  try {
    await updateLowCodeApp(editingId.value, {
      name: editForm.name,
      description: editForm.description || undefined,
      category: editForm.category || undefined,
      icon: editForm.icon || undefined
    });
    message.success("应用更新成功");
    editVisible.value = false;
    await fetchData();
  } catch (error) {
    message.error((error as Error).message || "更新失败");
  } finally {
    savingEdit.value = false;
  }
};

const resetWizard = () => {
  wizardStep.value = 0;
  wizardBasic.appKey = "";
  wizardBasic.name = "";
  wizardBasic.description = "";
  wizardBasic.category = "";
  wizardBasic.icon = "";
  datasourceMode.value = "platform";
  selectedDatasourceId.value = undefined;
  pendingDataSourceId.value = undefined;
  pendingAppId.value = undefined;
  newDatasource.name = "";
  newDatasource.dbType = "SQLite";
  newDatasource.filePath = "";
  newDatasource.server = "";
  newDatasource.port = "";
  newDatasource.database = "";
  newDatasource.username = "";
  newDatasource.password = "";
  newDatasourceTestPassed.value = false;
  sharingPolicy.useSharedUsers = true;
  sharingPolicy.useSharedRoles = true;
  sharingPolicy.useSharedDepartments = true;
  aliases.value = [
    { entityType: "user", singularAlias: "用户", pluralAlias: "用户列表" },
    { entityType: "role", singularAlias: "角色", pluralAlias: "角色列表" },
    { entityType: "department", singularAlias: "部门", pluralAlias: "部门列表" }
  ];
};

const openCreateWizard = () => {
  resetWizard();
  wizardVisible.value = true;
};

const closeWizard = () => {
  wizardVisible.value = false;
};

const loadDatasourceOptions = async (keywordValue?: string) => {
  datasourceLoading.value = true;
  try {
    const all = await getTenantDataSources();
    const normalized = (keywordValue ?? "").trim().toLowerCase();
    const filtered = normalized.length === 0
      ? all
      : all.filter((item: TenantDataSourceDto) =>
        item.name.toLowerCase().includes(normalized)
        || item.id.includes(normalized)
        || item.dbType.toLowerCase().includes(normalized));
    datasourceOptions.value = filtered.slice(0, 20).map(item => ({
      label: `${item.name} (${item.dbType})`,
      value: item.id
    }));
  } catch (error) {
    message.error((error as Error).message || "加载数据源失败");
  } finally {
    datasourceLoading.value = false;
  }
};

const onDatasourceModeChange = () => {
  newDatasourceTestPassed.value = false;
  if (datasourceMode.value === "existing" && datasourceOptions.value.length === 0) {
    void loadDatasourceOptions();
  }
};

/**
 * Escape a connection string value per ADO.NET rules:
 * single quotes are doubled, and the result is wrapped in single quotes
 * when the value contains any character that could break the key=value format
 * (semicolons, equals signs, curly braces, quotes, commas, whitespace).
 */
const escapeConnectionStringValue = (value: string): string => {
  const escaped = value.replace(/'/g, "''");
  if (/[;={}()'",\s]/.test(value)) {
    return `'${escaped}'`;
  }
  return escaped;
};

const buildConnectionString = () => {
  if (newDatasource.dbType === "SQLite") {
    return `Data Source=${newDatasource.filePath || "app.db"}`;
  }

  const server = escapeConnectionStringValue(newDatasource.server);
  const database = escapeConnectionStringValue(newDatasource.database);
  const username = escapeConnectionStringValue(newDatasource.username);
  const password = escapeConnectionStringValue(newDatasource.password);

  if (newDatasource.dbType === "SqlServer") {
    return `Server=${server},${newDatasource.port || "1433"};Database=${database};User Id=${username};Password=${password};TrustServerCertificate=True;`;
  }

  if (newDatasource.dbType === "MySql") {
    return `Server=${server};Port=${newDatasource.port || "3306"};Database=${database};Uid=${username};Pwd=${password};`;
  }

  return `Host=${server};Port=${newDatasource.port || "5432"};Database=${database};Username=${username};Password=${password};`;
};

const validateStepOne = () => {
  if (!wizardBasic.appKey.trim()) {
    message.warning("请填写应用标识");
    return false;
  }
  if (!wizardBasic.name.trim()) {
    message.warning("请填写应用名称");
    return false;
  }
  return true;
};

const validateStepTwo = () => {
  if (datasourceMode.value === "existing" && !selectedDatasourceId.value) {
    message.warning("请选择已有数据源");
    return false;
  }

  if (datasourceMode.value === "new") {
    if (!newDatasource.name.trim()) {
      message.warning("请填写数据源名称");
      return false;
    }
    if (newDatasource.dbType === "SQLite") {
      if (!newDatasource.filePath.trim()) {
        message.warning("请填写SQLite文件路径");
        return false;
      }
    } else if (!newDatasource.server.trim() || !newDatasource.database.trim() || !newDatasource.username.trim() || !newDatasource.password.trim()) {
      message.warning("请完善数据库连接信息");
      return false;
    }
    if (!newDatasourceTestPassed.value) {
      message.warning("请先测试连接并通过后继续");
      return false;
    }
  }
  return true;
};

const handleTestNewDatasource = async () => {
  try {
    testingDatasource.value = true;
    const result = await testTenantDataSourceConnection({
      connectionString: buildConnectionString(),
      dbType: newDatasource.dbType
    });
    if (result.success) {
      newDatasourceTestPassed.value = true;
      message.success("连接测试成功");
    } else {
      newDatasourceTestPassed.value = false;
      message.error(result.errorMessage || "连接测试失败");
    }
  } catch (error) {
    newDatasourceTestPassed.value = false;
    message.error((error as Error).message || "连接测试失败");
  } finally {
    testingDatasource.value = false;
  }
};

const resolveDataSourceId = async (): Promise<string | undefined> => {
  if (datasourceMode.value === "platform") {
    return undefined;
  }
  if (datasourceMode.value === "existing") {
    return selectedDatasourceId.value;
  }

  // Reuse datasource created in a previous (failed) attempt to avoid orphans on retry.
  if (pendingDataSourceId.value) {
    return pendingDataSourceId.value;
  }

  const tenantId = getTenantId();
  if (!tenantId) {
    throw new Error("缺少租户上下文，无法创建数据源");
  }
  const result = await createTenantDataSource({
    tenantIdValue: tenantId,
    name: newDatasource.name.trim(),
    connectionString: buildConnectionString(),
    dbType: newDatasource.dbType
  });
  pendingDataSourceId.value = result.id;
  return result.id;
};

const handleWizardPrimaryAction = async () => {
  if (wizardStep.value === 0) {
    if (!validateStepOne()) return;
    wizardStep.value = 1;
    return;
  }
  if (wizardStep.value === 1) {
    if (!validateStepTwo()) return;
    wizardStep.value = 2;
    return;
  }

  creating.value = true;
  try {
    // Step A: resolve (or reuse) the datasource — idempotent via pendingDataSourceId.
    let dataSourceId: string | undefined;
    try {
      dataSourceId = await resolveDataSourceId();
    } catch (error) {
      message.error((error as Error).message || "创建数据源失败，请重试");
      return;
    }

    const hasIndependentPolicy = !sharingPolicy.useSharedUsers || !sharingPolicy.useSharedRoles || !sharingPolicy.useSharedDepartments;
    if (hasIndependentPolicy && !dataSourceId) {
      message.warning("启用独立策略时必须绑定应用数据源");
      return;
    }

    // Step B: create the app — skipped on retry if already created (pendingAppId set).
    let appId = pendingAppId.value;
    if (!appId) {
      try {
        const app = await createLowCodeApp({
          appKey: wizardBasic.appKey.trim(),
          name: wizardBasic.name.trim(),
          description: wizardBasic.description || undefined,
          category: wizardBasic.category || undefined,
          icon: wizardBasic.icon || undefined,
          dataSourceId,
          useSharedUsers: sharingPolicy.useSharedUsers,
          useSharedRoles: sharingPolicy.useSharedRoles,
          useSharedDepartments: sharingPolicy.useSharedDepartments
        });
        pendingAppId.value = app.id;
        appId = app.id;
      } catch (error) {
        message.error((error as Error).message || "创建应用失败，请重试");
        return;
      }
    }

    // Step C: set entity aliases — safe to retry since PUT is idempotent.
    try {
      await updateAppEntityAliases(appId, aliases.value.map(item => ({
        entityType: item.entityType,
        singularAlias: item.singularAlias.trim(),
        pluralAlias: item.pluralAlias?.trim() || undefined
      })));
    } catch (error) {
      message.error((error as Error).message || "应用已创建，但保存实体别名失败，请点击确定重试");
      return;
    }

    message.success("应用创建成功");
    wizardVisible.value = false;
    await fetchData();
  } finally {
    creating.value = false;
  }
};

const handlePublish = async (id: string) => {
  try {
    await publishLowCodeApp(id);
    message.success(t("lowcodeApp.publishSuccess"));
    await fetchData();
  } catch (error) {
    message.error((error as Error).message || t("lowcodeApp.publishFailed"));
  }
};

const handleDelete = async (id: string) => {
  try {
    await deleteLowCodeApp(id);
    message.success(t("lowcodeApp.deleteSuccess"));
    await fetchData();
  } catch (error) {
    message.error((error as Error).message || t("lowcodeApp.deleteFailed"));
  }
};

const handleExport = async (app: LowCodeAppListItem) => {
  try {
    const blob = await exportLowCodeApp(app.id);
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = `${app.appKey}-export.json`;
    anchor.click();
    URL.revokeObjectURL(url);
    message.success("导出成功");
  } catch (error) {
    message.error((error as Error).message || "导出失败");
  }
};

const handleImportClick = () => {
  if (!importing.value) {
    importInputRef.value?.click();
  }
};

const handleImportFileChange = async (event: Event) => {
  const input = event.target as HTMLInputElement;
  const file = input.files?.[0];
  if (!file) {
    return;
  }

  importing.value = true;
  try {
    const rawText = await file.text();
    const pkg = parseLowCodeAppExportPackage(rawText);
    const result = await importLowCodeApp({
      package: pkg,
      conflictStrategy: "Rename"
    });
    if (result.skipped) {
      message.info("目标应用已存在，已按策略跳过导入");
    } else {
      message.success(`导入成功：${result.appKey}`);
    }
    await fetchData();
  } catch (error) {
    message.error((error as Error).message || "导入失败");
  } finally {
    importing.value = false;
    input.value = "";
  }
};

onMounted(fetchData);
</script>

<style scoped>
.app-list-page {
  padding: 24px;
}

.page-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  margin-bottom: 24px;
}

.page-header-left {
  display: flex;
  align-items: center;
  gap: 12px;
}

.page-header-left h2 {
  margin: 0;
  font-size: 20px;
}

.app-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 16px;
}

.app-card {
  position: relative;
  background: #fff;
  border: 1px solid #e8e8e8;
  border-radius: 8px;
  padding: 20px;
  cursor: pointer;
  transition: all 0.2s;
}

.app-card:hover {
  border-color: #1890ff;
  box-shadow: 0 2px 8px rgba(24, 144, 255, 0.15);
}

.app-card-icon {
  width: 48px;
  height: 48px;
  border-radius: 8px;
  background: linear-gradient(135deg, #1890ff, #36cfc9);
  display: flex;
  align-items: center;
  justify-content: center;
  font-size: 24px;
  color: #fff;
  margin-bottom: 12px;
}

.app-card-content {
  margin-bottom: 12px;
}

.app-card-name {
  font-size: 16px;
  font-weight: 500;
  margin-bottom: 4px;
}

.app-card-key {
  font-size: 12px;
  color: #999;
  margin-bottom: 4px;
}

.app-card-desc {
  font-size: 13px;
  color: #666;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.app-card-footer {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.app-card-version {
  font-size: 12px;
  color: #999;
}

.app-card-actions {
  position: absolute;
  top: 12px;
  right: 12px;
}

.app-card-new {
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  border-style: dashed;
  min-height: 200px;
}

.app-card-new-icon {
  font-size: 36px;
  color: #bbb;
  margin-bottom: 8px;
}

.app-card-new-text {
  font-size: 14px;
  color: #999;
}

.loading-container {
  display: flex;
  align-items: center;
  justify-content: center;
  height: 400px;
}

.switch-label {
  margin-left: 8px;
  color: #666;
}
</style>
