<template>
  <div class="template-market-page">
    <a-card title="模板市场">
      <div class="toolbar">
        <a-space>
          <a-input v-model:value="keyword" style="width: 220px" allow-clear placeholder="模板名称/描述" @pressEnter="loadData" />
          <a-input v-model:value="tags" style="width: 180px" allow-clear placeholder="标签（逗号分隔）" @pressEnter="loadData" />
          <a-input v-model:value="version" style="width: 140px" allow-clear placeholder="版本" @pressEnter="loadData" />
          <a-button type="primary" @click="loadData">查询</a-button>
        </a-space>
      </div>

      <a-table :data-source="items" :loading="loading" row-key="id" :pagination="false">
        <a-table-column key="name" title="模板名称" data-index="name" />
        <a-table-column key="tags" title="标签" data-index="tags" />
        <a-table-column key="version" title="版本" data-index="version" width="120" />
        <a-table-column key="updatedAt" title="更新时间" data-index="updatedAt" width="180" />
        <a-table-column key="action" title="操作" width="140">
          <template #default="{ record }">
            <a-button type="link" @click="openCreateFromTemplate(record)">从模板创建</a-button>
          </template>
        </a-table-column>
      </a-table>
    </a-card>

    <a-modal v-model:open="createVisible" title="从模板创建应用" @ok="handleCreateFromTemplate">
      <a-form layout="vertical">
        <a-form-item label="应用标识" required>
          <a-input v-model:value="createForm.appKey" />
        </a-form-item>
        <a-form-item label="应用名称" required>
          <a-input v-model:value="createForm.name" />
        </a-form-item>
      </a-form>
    </a-modal>
  </div>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from "vue";
import { useRouter } from "vue-router";
import { message } from "ant-design-vue";
import { createLowCodeApp, createLowCodePage } from "@/services/lowcode";
import { instantiateTemplate, searchTemplates, type TemplateListItem } from "@/services/templates";

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
    items.value = result.items;
  } catch (error) {
    message.error((error as Error).message || "加载模板失败");
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
    message.warning("请填写应用标识和应用名称");
    return;
  }
  try {
    const instantiated = await instantiateTemplate(selectedTemplate.value.id);
    const createdApp = await createLowCodeApp({
      appKey: createForm.appKey.trim(),
      name: createForm.name.trim(),
      category: "通用",
      description: `基于模板「${selectedTemplate.value.name}」创建`
    });
    await createLowCodePage(createdApp.id, {
      pageKey: "index",
      name: "首页",
      pageType: "Form",
      schemaJson: instantiated.schemaJson,
      routePath: "/",
      sortOrder: 1
    });
    message.success("已基于模板创建应用");
    createVisible.value = false;
    router.push(`/apps/${createdApp.id}/builder`);
  } catch (error) {
    message.error((error as Error).message || "模板创建失败");
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
