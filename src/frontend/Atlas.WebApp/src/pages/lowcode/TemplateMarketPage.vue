<template>
  <div class="template-market-page">
    <a-card :title="t('lowcode.template.title')">
      <div class="toolbar">
        <a-space>
          <a-input v-model:value="keyword" style="width: 220px" allow-clear :placeholder="t('lowcode.template.phKeyword')" @press-enter="loadData" />
          <a-input v-model:value="tags" style="width: 180px" allow-clear :placeholder="t('lowcode.template.phTags')" @press-enter="loadData" />
          <a-input v-model:value="version" style="width: 140px" allow-clear :placeholder="t('lowcode.template.phVersion')" @press-enter="loadData" />
          <a-button type="primary" @click="loadData">{{ t("lowcode.template.search") }}</a-button>
        </a-space>
      </div>

      <a-table :data-source="items" :loading="loading" row-key="id" :pagination="false">
        <a-table-column key="name" :title="t('lowcode.template.colName')" data-index="name" />
        <a-table-column key="tags" :title="t('lowcode.template.colTags')" data-index="tags" />
        <a-table-column key="version" :title="t('lowcode.template.colVersion')" data-index="version" width="120" />
        <a-table-column key="updatedAt" :title="t('lowcode.template.colUpdated')" data-index="updatedAt" width="180" />
        <a-table-column key="action" :title="t('lowcode.template.colActions')" width="140">
          <template #default="{ record }">
            <a-button type="link" @click="openCreateFromTemplate(record)">{{ t("lowcode.template.createFrom") }}</a-button>
          </template>
        </a-table-column>
      </a-table>
    </a-card>

    <a-modal v-model:open="createVisible" :title="t('lowcode.template.modalTitle')" @ok="handleCreateFromTemplate">
      <a-form layout="vertical">
        <a-form-item :label="t('lowcode.template.labelAppKey')" required>
          <a-input v-model:value="createForm.appKey" />
        </a-form-item>
        <a-form-item :label="t('lowcode.template.labelAppName')" required>
          <a-input v-model:value="createForm.name" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref, onUnmounted } from "vue";
import { useI18n } from "vue-i18n";

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { useRouter } from "vue-router";
import { message } from "ant-design-vue";
import { createLowCodeApp, createLowCodePage } from "@/services/lowcode";
import { instantiateTemplate, searchTemplates, type TemplateListItem } from "@/services/templates";

const { t } = useI18n();
const router = useRouter();
const loading = ref(false);
const items = ref<TemplateListItem[]>([]);
const keyword = ref("");
const tags = ref("");
const version = ref("");

const createVisible = ref(false);
const selectedTemplate = ref<TemplateListItem | null>(null);
const createForm = reactive({
  appKey: "",
  name: ""
});

const loadData = async () => {
  loading.value = true;
  try {
    const result = await searchTemplates({
      keyword: keyword.value || undefined,
      tags: tags.value || undefined,
      version: version.value || undefined,
      pageIndex: 1,
      pageSize: 50
    });

    if (!isMounted.value) return;
    items.value = result.items;
  } catch (error) {
    message.error((error as Error).message || t("lowcode.template.loadFailed"));
  } finally {
    loading.value = false;
  }
};

const openCreateFromTemplate = (record: TemplateListItem) => {
  selectedTemplate.value = record;
  createForm.appKey = "";
  createForm.name = "";
  createVisible.value = true;
};

const handleCreateFromTemplate = async () => {
  if (!selectedTemplate.value) return;
  if (!createForm.appKey.trim() || !createForm.name.trim()) {
    message.warning(t("lowcode.template.warnFill"));
    return;
  }
  try {
    const instantiated = await instantiateTemplate(selectedTemplate.value.id);

    if (!isMounted.value) return;
    const createdApp = await createLowCodeApp({
      appKey: createForm.appKey.trim(),
      name: createForm.name.trim(),
      category: "\u901a\u7528",
      description: t("lowcode.template.descFromTemplate", { name: selectedTemplate.value.name })
    });

    if (!isMounted.value) return;
    await createLowCodePage(createdApp.id, {
      pageKey: "index",
      name: "\u9996\u9875",
      pageType: "Form",
      schemaJson: instantiated.schemaJson,
      routePath: "/",
      sortOrder: 1
    });

    if (!isMounted.value) return;
    message.success(t("lowcode.template.createOk"));
    createVisible.value = false;
    router.push(`/apps/${createdApp.id}/builder`);
  } catch (error) {
    message.error((error as Error).message || t("lowcode.template.createFailed"));
  }
};

onMounted(() => {
  void loadData();
});
</script>

<style scoped>
.template-market-page {
  padding: 24px;
}

.toolbar {
  margin-bottom: 16px;
}
</style>
