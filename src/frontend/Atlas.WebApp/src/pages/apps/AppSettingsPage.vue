<template>
  <div class="app-settings-page">
    <a-page-header :title="t('appsSettings.pageTitle')" :sub-title="t('appsSettings.pageSubtitle')" />

    <a-row :gutter="[16, 16]" class="mt12">
      <a-col :span="24">
        <a-card :title="t('appsSettings.cardDataSource')">
          <a-descriptions :column="2" bordered size="small">
            <a-descriptions-item :label="t('appsSettings.dsId')">
              {{ dataSourceInfo?.dataSourceId || "-" }}
            </a-descriptions-item>
            <a-descriptions-item :label="t('appsSettings.dsName')">
              {{ dataSourceInfo?.name || "-" }}
            </a-descriptions-item>
            <a-descriptions-item :label="t('appsSettings.dbType')">
              {{ dataSourceInfo?.dbType || "-" }}
            </a-descriptions-item>
            <a-descriptions-item :label="t('appsSettings.maxPool')">
              {{ dataSourceInfo?.maxPoolSize ?? "-" }}
            </a-descriptions-item>
            <a-descriptions-item :label="t('appsSettings.connTimeout')">
              {{ dataSourceInfo?.connectionTimeoutSeconds ?? "-" }}
            </a-descriptions-item>
            <a-descriptions-item :label="t('appsSettings.lastTest')">
              <a-tag v-if="dataSourceInfo?.lastTestSuccess === true" color="green">{{ t("appsSettings.testOk") }}</a-tag>
              <a-tag v-else-if="dataSourceInfo?.lastTestSuccess === false" color="red">{{ t("appsSettings.testFail") }}</a-tag>
              <span v-else>-</span>
            </a-descriptions-item>
          </a-descriptions>

          <a-space class="mt12">
            <a-button
              type="primary"
              :disabled="!dataSourceInfo?.dataSourceId"
              :loading="testingDataSource"
              @click="handleTestDataSource"
            >
              {{ t("appsSettings.testConnection") }}
            </a-button>
            <a-button @click="go('/settings/system/datasources')">{{ t("appsSettings.gotoDsMgmt") }}</a-button>
            <a-button
              v-if="!dataSourceInfo?.dataSourceId"
              :loading="bindingDataSource"
              @click="handleOpenBindDataSource"
            >
              {{ t("appsSettings.bindDs") }}
            </a-button>
            <a-button
              v-else
              :loading="bindingDataSource"
              @click="handleOpenSwitchDataSource"
            >
              {{ t("appsSettings.switchDs") }}
            </a-button>
            <a-popconfirm
              v-if="dataSourceInfo?.dataSourceId"
              :title="t('appsSettings.unbindConfirm')"
              :ok-text="t('appsSettings.unbindOk')"
              :cancel-text="t('common.cancel')"
              @confirm="handleUnbindDataSource"
            >
              <a-button danger :loading="bindingDataSource">{{ t("appsSettings.unbindBtn") }}</a-button>
            </a-popconfirm>
          </a-space>
        </a-card>
      </a-col>

      <a-col :span="24">
        <a-card :title="t('appsSettings.cardAliases')">
          <a-table :data-source="entityAliases" :pagination="false" row-key="entityType" bordered size="small">
            <a-table-column :title="t('appsSettings.colEntityType')" data-index="entityType" key="entityType" width="180" />
            <a-table-column :title="t('appsSettings.colSingular')" key="singularAlias">
              <template #default="{ record }">
                <a-input v-model:value="record.singularAlias" />
              </template>
            </a-table-column>
            <a-table-column :title="t('appsSettings.colPlural')" key="pluralAlias">
              <template #default="{ record }">
                <a-input v-model:value="record.pluralAlias" />
              </template>
            </a-table-column>
          </a-table>
          <a-button type="primary" class="mt12" :loading="savingAliases" @click="saveEntityAliases">
            {{ t("appsSettings.saveAliases") }}
          </a-button>
        </a-card>
      </a-col>

      <a-col :span="24">
        <a-card :title="t('appsSettings.cardFileStorage')">
          <a-form layout="vertical">
            <a-form-item :label="t('appsSettings.inheritBasePath')">
              <a-switch v-model:checked="fileStorageForm.inheritBasePath" />
            </a-form-item>
            <a-form-item :label="t('appsSettings.basePath')" :help="t('appsSettings.basePathHelp')">
              <a-input
                v-model:value="fileStorageForm.overrideBasePath"
                :disabled="fileStorageForm.inheritBasePath"
                :placeholder="t('appsSettings.basePathPlaceholder')"
              />
            </a-form-item>
            <a-form-item :label="t('appsSettings.inheritBucket')">
              <a-switch v-model:checked="fileStorageForm.inheritMinioBucketName" />
            </a-form-item>
            <a-form-item :label="t('appsSettings.minioBucket')" :help="t('appsSettings.bucketHelp')">
              <a-input
                v-model:value="fileStorageForm.overrideMinioBucketName"
                :disabled="fileStorageForm.inheritMinioBucketName"
                :placeholder="t('appsSettings.bucketPlaceholder')"
              />
            </a-form-item>
            <a-descriptions :column="2" bordered size="small" class="mt12">
              <a-descriptions-item :label="t('appsSettings.effectiveBasePath')">
                {{ fileStorageForm.effectiveBasePath || "-" }}
              </a-descriptions-item>
              <a-descriptions-item :label="t('appsSettings.effectiveBucket')">
                {{ fileStorageForm.effectiveMinioBucketName || "-" }}
              </a-descriptions-item>
            </a-descriptions>
            <a-button type="primary" class="mt12" :loading="savingFileStorage" @click="saveFileStorageSettings">
              {{ t("appsSettings.saveFileStorage") }}
            </a-button>
          </a-form>
        </a-card>
      </a-col>
    </a-row>

    <a-modal
      v-model:open="dataSourceSelectorVisible"
      :title="dataSourceSelectionMode === 'bind' ? t('appsSettings.modalBindTitle') : t('appsSettings.modalSwitchTitle')"
      :ok-text="t('appsSettings.modalOk')"
      :cancel-text="t('common.cancel')"
      :confirm-loading="bindingDataSource"
      @ok="handleConfirmDataSourceSelection"
    >
      <a-alert
        type="info"
        show-icon
        :message="t('appsSettings.selectDsTitle')"
        :description="t('appsSettings.selectDsDesc')"
        style="margin-bottom: 12px"
      />
      <a-select
        v-model:value="selectedDataSourceId"
        style="width: 100%"
        :options="dataSourceOptions"
        show-search
        allow-clear
        :loading="loadingDataSourceOptions"
        :filter-option="false"
        :placeholder="t('appsSettings.selectDsPlaceholder')"
        @search="handleSearchDataSources"
      />
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, onUnmounted } from "vue";
import { useI18n } from "vue-i18n";

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { useRoute, useRouter } from "vue-router";
import { message } from "ant-design-vue";
import type { TenantDataSourceDto } from "@/types/api";
import type {
  LowCodeAppDataSourceInfo,
  LowCodeAppEntityAliasItem
} from "@/types/lowcode";
import type { TenantAppFileStorageSettings, TenantAppInstanceDetail } from "@/types/platform-v2";
import { getTenantDataSources } from "@/services/api-system";
import {
  getTenantAppInstanceDataSourceInfo,
  getTenantAppInstanceDetail,
  getTenantAppInstanceEntityAliases,
  getTenantAppInstanceFileStorageSettings,
  testTenantAppInstanceDataSource,
  updateTenantAppInstanceFileStorageSettings,
  updateTenantAppInstance,
  updateTenantAppInstanceEntityAliases
} from "@/services/api-tenant-app-instances";
import { debounce } from "@/utils/common";

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const appId = computed(() => String(route.params.appId ?? ""));

const dataSourceInfo = ref<LowCodeAppDataSourceInfo | null>(null);
const tenantAppDetail = ref<TenantAppInstanceDetail | null>(null);
const testingDataSource = ref(false);
const bindingDataSource = ref(false);
const savingAliases = ref(false);
const dataSourceSelectorVisible = ref(false);
const dataSourceSelectionMode = ref<"bind" | "switch">("bind");
const loadingDataSourceOptions = ref(false);
const selectedDataSourceId = ref<string>();
const dataSourceOptions = ref<Array<{ label: string; value: string }>>([]);
const savingFileStorage = ref(false);
const fileStorageForm = reactive<TenantAppFileStorageSettings>({
  tenantAppInstanceId: "",
  appId: "",
  effectiveBasePath: "",
  effectiveMinioBucketName: "",
  overrideBasePath: "",
  overrideMinioBucketName: "",
  inheritBasePath: true,
  inheritMinioBucketName: true
});

function defaultEntityAliases(): LowCodeAppEntityAliasItem[] {
  return [
    { entityType: "users", singularAlias: t("appsSettings.defaultAliasUsersS"), pluralAlias: t("appsSettings.defaultAliasUsersP") },
    { entityType: "roles", singularAlias: t("appsSettings.defaultAliasRolesS"), pluralAlias: t("appsSettings.defaultAliasRolesP") },
    { entityType: "departments", singularAlias: t("appsSettings.defaultAliasDeptsS"), pluralAlias: t("appsSettings.defaultAliasDeptsP") }
  ];
}

const entityAliases = ref<LowCodeAppEntityAliasItem[]>(defaultEntityAliases());

function go(path: string) {
  router.push(path);
}

async function loadSettings() {
  if (!appId.value) return;

  try {
    const [dataSource, aliases, appDetail, fileStorage]  = await Promise.all([
      getTenantAppInstanceDataSourceInfo(appId.value),
      getTenantAppInstanceEntityAliases(appId.value),
      getTenantAppInstanceDetail(appId.value),
      getTenantAppInstanceFileStorageSettings(appId.value)
    ]);

    if (!isMounted.value) return;

    dataSourceInfo.value = dataSource;
    tenantAppDetail.value = appDetail;
    Object.assign(fileStorageForm, fileStorage);
    if (aliases.length > 0) {
      entityAliases.value = aliases;
    } else {
      entityAliases.value = defaultEntityAliases();
    }
  } catch (error) {
    message.error((error as Error).message || t("appsSettings.loadFailed"));
  }
}

async function loadDataSourceOptions(keyword = "") {
  loadingDataSourceOptions.value = true;
  try {
    const allDataSources  = await getTenantDataSources();

    if (!isMounted.value) return;
    dataSourceOptions.value = mapDataSourceOptions(allDataSources, keyword);
  } catch (error) {
    message.error((error as Error).message || t("appsSettings.loadDsListFailed"));
  } finally {
    loadingDataSourceOptions.value = false;
  }
}

function mapDataSourceOptions(dataSources: TenantDataSourceDto[], keyword: string) {
  const normalizedKeyword = keyword.trim().toLowerCase();
  return dataSources
    .filter((item) => {
      if (item.appId && item.appId !== appId.value) {
        return false;
      }
      if (!normalizedKeyword) {
        return true;
      }
      return item.name.toLowerCase().includes(normalizedKeyword)
        || item.dbType.toLowerCase().includes(normalizedKeyword)
        || item.id.toLowerCase().includes(normalizedKeyword);
    })
    .slice(0, 20)
    .map((item) => ({
      value: item.id,
      label: `${item.name} (${item.dbType})`
    }));
}

const handleSearchDataSources = debounce((value: string) => {
  void loadDataSourceOptions(value);
}, 300);

function handleOpenBindDataSource() {
  openDataSourceSelector("bind");
}

function handleOpenSwitchDataSource() {
  openDataSourceSelector("switch");
}

function openDataSourceSelector(mode: "bind" | "switch") {
  dataSourceSelectionMode.value = mode;
  selectedDataSourceId.value = dataSourceInfo.value?.dataSourceId || undefined;
  dataSourceSelectorVisible.value = true;
  void loadDataSourceOptions();
}

async function ensureTenantAppDetail() {
  if (!appId.value) {
    return null;
  }

  if (tenantAppDetail.value) {
    return tenantAppDetail.value;
  }

  tenantAppDetail.value = await getTenantAppInstanceDetail(appId.value);


  if (!isMounted.value) return;
  return tenantAppDetail.value;
}

async function updateAppDataSourceBinding(targetDataSourceId: number | null) {
  if (!appId.value) {
    return;
  }

  const currentDetail  = await ensureTenantAppDetail();


  if (!isMounted.value) return;
  if (!currentDetail) {
    return;
  }

  bindingDataSource.value = true;
  try {
    await updateTenantAppInstance(appId.value, {
      name: currentDetail.name,
      description: currentDetail.description,
      category: currentDetail.category,
      icon: currentDetail.icon,
      dataSourceId: targetDataSourceId,
      unbindDataSource: targetDataSourceId === null
    });

    if (!isMounted.value) return;
    await loadSettings();

    if (!isMounted.value) return;
  } catch (error) {
    message.error((error as Error).message || t("appsSettings.updateBindFailed"));
    throw error;
  } finally {
    bindingDataSource.value = false;
  }
}

async function handleConfirmDataSourceSelection() {
  if (!selectedDataSourceId.value) {
    message.warning(t("appsSettings.pickDataSource"));
    return;
  }

  if (selectedDataSourceId.value === dataSourceInfo.value?.dataSourceId) {
    message.info(t("appsSettings.sameDataSource"));
    return;
  }

  await updateAppDataSourceBinding(Number(selectedDataSourceId.value));


  if (!isMounted.value) return;
  message.success(dataSourceSelectionMode.value === "bind" ? t("appsSettings.bindSuccess") : t("appsSettings.switchSuccess"));
  dataSourceSelectorVisible.value = false;
}

async function handleUnbindDataSource() {
  await updateAppDataSourceBinding(null);

  if (!isMounted.value) return;
  message.success(t("appsSettings.unboundSuccess"));
}

async function handleTestDataSource() {
  if (!appId.value) return;
  testingDataSource.value = true;
  try {
    const result  = await testTenantAppInstanceDataSource(appId.value);

    if (!isMounted.value) return;
    if (result.success) {
      message.success(t("appsSettings.testOkMsg"));
    } else {
      message.error(result.errorMessage || t("appsSettings.testFailMsg"));
    }
    dataSourceInfo.value = await getTenantAppInstanceDataSourceInfo(appId.value);

    if (!isMounted.value) return;
  } catch (error) {
    message.error((error as Error).message || t("appsSettings.testFailed"));
  } finally {
    testingDataSource.value = false;
  }
}

async function saveEntityAliases() {
  if (!appId.value) return;
  savingAliases.value = true;
  try {
    await updateTenantAppInstanceEntityAliases(appId.value, {
      items: entityAliases.value.map((item) => ({
        entityType: item.entityType.trim(),
        singularAlias: item.singularAlias.trim(),
        pluralAlias: item.pluralAlias.trim()
      }))
    });

    if (!isMounted.value) return;
    message.success(t("appsSettings.aliasesSaved"));
  } catch (error) {
    message.error((error as Error).message || t("appsSettings.aliasesSaveFailed"));
  } finally {
    savingAliases.value = false;
  }
}

async function saveFileStorageSettings() {
  if (!appId.value) return;
  if (!fileStorageForm.inheritBasePath && !fileStorageForm.overrideBasePath?.trim()) {
    message.warning(t("appsSettings.basePathRequired"));
    return;
  }

  if (!fileStorageForm.inheritMinioBucketName && !fileStorageForm.overrideMinioBucketName?.trim()) {
    message.warning(t("appsSettings.bucketRequired"));
    return;
  }

  savingFileStorage.value = true;
  try {
    await updateTenantAppInstanceFileStorageSettings(appId.value, {
      inheritBasePath: fileStorageForm.inheritBasePath,
      inheritMinioBucketName: fileStorageForm.inheritMinioBucketName,
      overrideBasePath: fileStorageForm.inheritBasePath ? undefined : fileStorageForm.overrideBasePath?.trim(),
      overrideMinioBucketName: fileStorageForm.inheritMinioBucketName ? undefined : fileStorageForm.overrideMinioBucketName?.trim()
    });

    if (!isMounted.value) return;
    message.success(t("appsSettings.fileStorageSaved"));
    const latest = await getTenantAppInstanceFileStorageSettings(appId.value);

    if (!isMounted.value) return;
    Object.assign(fileStorageForm, latest);
  } catch (error) {
    message.error((error as Error).message || t("appsSettings.fileStorageSaveFailed"));
  } finally {
    savingFileStorage.value = false;
  }
}

onMounted(loadSettings);
</script>

<style scoped>
.app-settings-page {
  padding: 8px;
}

.mt12 {
  margin-top: 12px;
}
</style>
