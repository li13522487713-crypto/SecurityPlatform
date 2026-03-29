<template>
  <a-card :title="pageTitle" class="page-card dynamic-tables-page">
    <template #extra>
      <a-space :size="8" wrap>
        <a-select
          v-model:value="selectedAppId"
          class="app-select"
          :options="appOptions"
          :loading="appLoading"
          show-search
          :filter-option="filterAppOption"
          :placeholder="t('dynamic.selectAppScope')"
          @change="handleAppScopeChange"
        />
        <a-button @click="refreshAll">{{ t("dynamic.refresh") }}</a-button>
        <a-button v-if="selectedAppId" type="primary" @click="openERDCanvas">{{ t("dynamic.openErd") }}</a-button>
      </a-space>
    </template>

    <a-row :gutter="[16, 16]" align="top">
      <a-col :xs="24" :lg="8" :xl="7">
        <a-card size="small" :title="t('dynamic.tableDirectory')" class="table-directory-card">
          <a-space direction="vertical" style="width: 100%" :size="12">
            <a-input-search
              v-model:value="tableKeyword"
              :placeholder="t('dynamic.searchTablesPlaceholder')"
              allow-clear
              @search="loadTableDirectory"
              @change="handleTableKeywordChange"
            />

            <a-spin :spinning="tableLoading">
              <a-list
                v-if="tableDirectory.length > 0"
                :data-source="tableDirectory"
                size="small"
                bordered
                class="table-directory-list"
              >
                <template #renderItem="{ item }">
                  <a-list-item>
                    <a-space direction="vertical" style="width: 100%" :size="4">
                      <div class="table-name">{{ item.displayName || item.tableKey }}</div>
                      <div class="table-key">{{ item.tableKey }}</div>
                      <a-space :size="4" wrap>
                        <a-button size="small" type="link" @click="openTableCrud(item.tableKey)">
                          {{ t("dynamic.openCrud") }}
                        </a-button>
                        <a-button size="small" type="link" @click="openTableNative(item.tableKey)">
                          {{ t("dynamic.openNativeView") }}
                        </a-button>
                      </a-space>
                    </a-space>
                  </a-list-item>
                </template>
              </a-list>
              <a-empty v-else :description="t('dynamic.noTableInCurrentApp')" />
            </a-spin>
          </a-space>
        </a-card>
      </a-col>

      <a-col :xs="24" :lg="16" :xl="17">
        <a-spin :spinning="loading">
          <AmisRenderer v-if="schema" :schema="schema" :data="pageData" />
          <a-empty v-else-if="!loading" :description="t('dynamic.emptyNoPage')" />
        </a-spin>
      </a-col>
    </a-row>
  </a-card>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, onUnmounted, watch } from "vue";
import { useI18n } from "vue-i18n";

const { t } = useI18n();

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { useRoute, useRouter } from "vue-router";
import { message } from "ant-design-vue";
import AmisRenderer from "@/components/amis/amis-renderer.vue";
import { getDynamicAmisSchema } from "@/services/dynamic-tables";
import { getLowCodeAppsPaged } from "@/services/lowcode";
import type { AmisSchema } from "@/types/amis";
import { getCurrentAppIdFromStorage, setCurrentAppIdToStorage } from "@/utils/app-context";
import { getAppAvailableDynamicTables } from "@/services/api-app-members";

const route = useRoute();
const router = useRouter();
const loading = ref(false);
const schema = ref<AmisSchema | null>(null);
const pageTitle = ref(t("dynamic.tablesTitle"));
const appLoading = ref(false);
const tableLoading = ref(false);
const tableKeyword = ref("");
const selectedAppId = ref<string | undefined>(
  typeof route.params.appId === "string" && route.params.appId.trim()
    ? route.params.appId
    : getCurrentAppIdFromStorage() ?? undefined
);
const appOptions = ref<Array<{ label: string; value: string }>>([]);
const tableDirectory = ref<Array<{ tableKey: string; displayName: string }>>([]);
const pageData = computed(() => ({
  appId: selectedAppId.value ?? null,
  currentAppId: selectedAppId.value ?? null
}));

const filterAppOption = (input: string, option: { label?: string; value?: string }) => {
  const label = (option.label ?? "").toString().toLowerCase();
  return label.includes(input.toLowerCase());
};

const loadSchema = async () => {
  loading.value = true;
  try {
    schema.value = (await getDynamicAmisSchema("list")) as AmisSchema;
  } catch (error) {
    schema.value = null;
    message.error((error as Error).message || t("dynamic.loadPageFailed"));
  } finally {
    loading.value = false;
  }
};

const loadTableDirectory = async () => {
  if (!selectedAppId.value) {
    tableDirectory.value = [];
    return;
  }
  tableLoading.value = true;
  try {
    const result = await getAppAvailableDynamicTables(selectedAppId.value, tableKeyword.value.trim() || undefined);
    if (!isMounted.value) return;
    tableDirectory.value = result;
  } catch (error) {
    message.error((error as Error).message || t("dynamic.loadTablesFailed"));
  } finally {
    if (isMounted.value) {
      tableLoading.value = false;
    }
  }
};

const loadAppOptions = async () => {
  appLoading.value = true;
  try {
    const result  = await getLowCodeAppsPaged({ pageIndex: 1, pageSize: 200 });

    if (!isMounted.value) return;
    appOptions.value = result.items.map((item) => ({
      label: `${item.name} (${item.appKey})`,
      value: item.id
    }));
  } catch (error) {
    message.error((error as Error).message || t("dynamic.loadAppsFailed"));
  } finally {
    appLoading.value = false;
  }
};

const handleAppScopeChange = (value: string | undefined) => {
  setCurrentAppIdToStorage(value);
  if (value && value !== route.params.appId) {
    void router.push(`/apps/${value}/data`);
    return;
  }
  void refreshAll();
};

const refreshAll = async () => {
  await Promise.all([loadSchema(), loadTableDirectory()]);
};

const handleTableKeywordChange = () => {
  void loadTableDirectory();
};

const openTableCrud = (tableKey: string) => {
  if (!selectedAppId.value) {
    return;
  }
  void router.push(`/apps/${selectedAppId.value}/data/${encodeURIComponent(tableKey)}`);
};

const openTableNative = (tableKey: string) => {
  if (!selectedAppId.value) {
    return;
  }
  void router.push(`/apps/${selectedAppId.value}/data/${encodeURIComponent(tableKey)}/native`);
};

onMounted(() => {
  void loadAppOptions();
  void refreshAll();
});

const openERDCanvas = () => {
  if (selectedAppId.value) {
    router.push(`/apps/${selectedAppId.value}/data/erd`);
  }
};

const syncSelectedAppFromRoute = () => {
  const routeAppId = typeof route.params.appId === "string" ? route.params.appId.trim() : "";
  const nextAppId = routeAppId || getCurrentAppIdFromStorage() || undefined;
  if (nextAppId !== selectedAppId.value) {
    selectedAppId.value = nextAppId;
  }
};

watch(() => route.params.appId, () => {
  syncSelectedAppFromRoute();
  void refreshAll();
});
</script>

<style scoped>
.dynamic-tables-page :deep(.ant-card-head-title) {
  white-space: normal;
}

.app-select {
  width: 280px;
}

.table-directory-card {
  height: 100%;
}

.table-directory-list {
  max-height: calc(100vh - 320px);
  overflow: auto;
}

.table-name {
  font-weight: 600;
}

.table-key {
  color: rgba(0, 0, 0, 0.45);
  font-size: 12px;
}

.dynamic-tables-page :deep(.dynamic-table-wizard) {
  max-width: 100%;
}

.dynamic-tables-page :deep(.dynamic-table-wizard .cxd-Wizard-steps) {
  max-height: 60vh;
  overflow: auto;
}

.dynamic-tables-page :deep(.dynamic-table-wizard .cxd-Form-itemControl) {
  min-width: 0;
}

@media (max-width: 992px) {
  .app-select {
    width: 220px;
  }

  .table-directory-list {
    max-height: 320px;
  }
}
</style>
