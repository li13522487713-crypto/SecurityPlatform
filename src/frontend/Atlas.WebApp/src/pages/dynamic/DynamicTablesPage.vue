<template>
  <a-card :title="pageTitle" class="page-card">
    <template #extra>
      <a-space>
        <a-select
          v-model:value="selectedAppId"
          style="width: 260px"
          :options="appOptions"
          :loading="appLoading"
          allow-clear
          show-search
          :placeholder="t('dynamic.selectAppScope')"
          @change="handleAppScopeChange"
        />
        <a-button @click="loadSchema">{{ t("dynamic.refresh") }}</a-button>
        <a-button v-if="selectedAppId" type="primary" @click="openERDCanvas">ERD 模型设计</a-button>
      </a-space>
    </template>
    <a-spin :spinning="loading">
      <AmisRenderer v-if="schema" :schema="schema" :data="pageData" />
      <a-empty v-else-if="!loading" :description="t('dynamic.emptyNoPage')" />
    </a-spin>
  </a-card>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, onUnmounted } from "vue";
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

const route = useRoute();
const router = useRouter();
const loading = ref(false);
const schema = ref<AmisSchema | null>(null);
const pageTitle = ref(t("dynamic.tablesTitle"));
const appLoading = ref(false);
const selectedAppId = ref<string | undefined>(
  typeof route.params.appId === "string" && route.params.appId.trim()
    ? route.params.appId
    : getCurrentAppIdFromStorage() ?? undefined
);
const appOptions = ref<Array<{ label: string; value: string }>>([]);
const pageData = computed(() => ({
  appId: selectedAppId.value ?? null,
  currentAppId: selectedAppId.value ?? null
}));

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
  void loadSchema();
};

onMounted(() => {
  void loadAppOptions();
  void loadSchema();
});

const openERDCanvas = () => {
  if (selectedAppId.value) {
    router.push(`/apps/${selectedAppId.value}/data/erd`);
  }
};
</script>
