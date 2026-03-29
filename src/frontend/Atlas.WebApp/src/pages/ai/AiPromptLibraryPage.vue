<template>
  <a-card :title="t('ai.promptLib.pageTitle')" :bordered="false">
    <div class="toolbar">
      <a-space wrap>
        <a-input-search
          v-model:value="keyword"
          :placeholder="t('ai.promptLib.searchPlaceholder')"
          style="width: 260px"
          @search="loadData"
        />
        <a-input
          v-model:value="category"
          :placeholder="t('ai.promptLib.categoryFilter')"
          style="width: 180px"
          allow-clear
          @press-enter="loadData"
        />
        <a-button @click="handleReset">{{ t("common.reset") }}</a-button>
        <a-button @click="openInsertModal">{{ t("ai.promptLib.insertTemplate") }}</a-button>
        <a-button type="primary" @click="openCreate">{{ t("ai.promptLib.newTemplate") }}</a-button>
      </a-space>
    </div>

    <a-table row-key="id" :columns="columns" :data-source="list" :loading="loading" :pagination="false">
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'content'">
          <a-typography-paragraph :ellipsis="{ rows: 2, expandable: true, symbol: t('ai.expand') }">
            {{ record.content }}
          </a-typography-paragraph>
        </template>
        <template v-if="column.key === 'tags'">
          <a-space wrap>
            <a-tag v-for="tag in record.tags" :key="tag">{{ tag }}</a-tag>
            <span v-if="record.tags.length === 0">-</span>
          </a-space>
        </template>
        <template v-if="column.key === 'action'">
          <a-space>
            <a-button type="link" @click="openEdit(record.id)">{{ t("common.edit") }}</a-button>
            <a-popconfirm :title="t('ai.promptLib.deleteConfirm')" @confirm="handleDelete(record.id)">
              <a-button type="link" danger :disabled="record.isSystem">{{ t("common.delete") }}</a-button>
            </a-popconfirm>
          </a-space>
        </template>
      </template>
    </a-table>

    <div class="pager">
      <a-pagination
        v-model:current="pageIndex"
        v-model:page-size="pageSize"
        :total="total"
        show-size-changer
        :page-size-options="['10', '20', '50']"
        @change="loadData"
      />
    </div>

    <a-drawer
      :open="modalOpen"
      :title="editingId ? t('ai.promptLib.drawerEdit') : t('ai.promptLib.drawerCreate')"
      placement="right"
      :width="860"
      :destroy-on-close="false"
      @close="closeModal"
    >
      <a-form ref="formRef" :model="form" layout="vertical" :rules="rules">
        <a-tabs v-model:active-key="activeTab" class="prompt-editor-tabs">
          <a-tab-pane key="basic" :tab="t('ai.promptLib.tabBasic')">
            <a-form-item :label="t('ai.promptLib.colName')" name="name">
              <a-input v-model:value="form.name" />
            </a-form-item>
            <a-form-item :label="t('ai.promptLib.labelDescription')" name="description">
              <a-textarea v-model:value="form.description" :rows="2" />
            </a-form-item>
            <a-form-item :label="t('ai.promptLib.labelCategory')" name="category">
              <a-input v-model:value="form.category" />
            </a-form-item>
            <a-form-item :label="t('ai.promptLib.labelTags')" name="tags">
              <a-input v-model:value="tagsText" />
            </a-form-item>
            <a-form-item v-if="!editingId" :label="t('ai.promptLib.labelSystem')">
              <a-switch v-model:checked="form.isSystem" />
            </a-form-item>
          </a-tab-pane>
          <a-tab-pane key="content" :tab="t('ai.promptLib.tabContent')">
            <a-space style="margin-bottom: 8px">
              <a-button size="small" @click="openInsertModal">{{ t("ai.promptLib.insertPromptToContent") }}</a-button>
            </a-space>
            <a-form-item :label="t('ai.promptLib.labelContent')" name="content">
              <a-textarea ref="contentInputRef" v-model:value="form.content" :rows="14" />
            </a-form-item>
          </a-tab-pane>
          <a-tab-pane key="preview" :tab="t('ai.promptLib.tabPreview')">
            <pre class="preview-block">{{ form.content || t("ai.promptLib.previewEmpty") }}</pre>
          </a-tab-pane>
        </a-tabs>
      </a-form>
      <template #footer>
        <div class="drawer-footer">
          <a-space>
            <a-button @click="closeModal">{{ t("common.cancel") }}</a-button>
            <a-button type="primary" :loading="modalLoading" @click="submitForm">{{ t("common.confirm") }}</a-button>
          </a-space>
        </div>
      </template>
    </a-drawer>

    <prompt-insert-modal
      :open="insertModalOpen"
      @cancel="insertModalOpen = false"
      @insert="handleInsertPrompt"
    />
  </a-card>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, onUnmounted, nextTick } from "vue";
import { useI18n } from "vue-i18n";

const { t } = useI18n();

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import type { FormInstance } from "ant-design-vue";
import { message } from "ant-design-vue";
import PromptInsertModal from "@/components/ai/PromptInsertModal.vue";
import {
  createAiPromptTemplate,
  deleteAiPromptTemplate,
  getAiPromptTemplateById,
  getAiPromptTemplatesPaged,
  updateAiPromptTemplate,
  type AiPromptTemplateListItem
} from "@/services/api-ai-prompt";

const keyword = ref("");
const category = ref("");
const list = ref<AiPromptTemplateListItem[]>([]);
const loading = ref(false);
const pageIndex = ref(1);
const pageSize = ref(20);
const total = ref(0);

const columns = computed(() => [
  { title: t("ai.promptLib.colName"), dataIndex: "name", key: "name", width: 220 },
  { title: t("ai.promptLib.colCategory"), dataIndex: "category", key: "category", width: 140 },
  { title: t("ai.promptLib.colTags"), key: "tags", width: 220 },
  { title: t("ai.promptLib.colContent"), key: "content" },
  { title: t("ai.colActions"), key: "action", width: 140 }
]);

const modalOpen = ref(false);
const modalLoading = ref(false);
const editingId = ref<number | null>(null);
const activeTab = ref("basic");
const formRef = ref<FormInstance>();
const contentInputRef = ref<unknown>(null);
const form = reactive({
  name: "",
  description: "",
  category: "",
  content: "",
  isSystem: false
});
const tagsText = ref("");
const rules = computed(() => ({
  name: [{ required: true, message: t("ai.promptLib.ruleName") }],
  content: [{ required: true, message: t("ai.promptLib.ruleContent") }]
}));

const insertModalOpen = ref(false);

async function loadData() {
  loading.value = true;
  try {
    const result  = await getAiPromptTemplatesPaged(
      { pageIndex: pageIndex.value, pageSize: pageSize.value, keyword: keyword.value || undefined },
      category.value || undefined
    );

    if (!isMounted.value) return;
    list.value = result.items;
    total.value = Number(result.total);
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.promptLib.loadFailed"));
  } finally {
    loading.value = false;
  }
}

function handleReset() {
  keyword.value = "";
  category.value = "";
  pageIndex.value = 1;
  void loadData();
}

function openCreate() {
  editingId.value = null;
  activeTab.value = "basic";
  Object.assign(form, {
    name: "",
    description: "",
    category: "",
    content: "",
    isSystem: false
  });
  tagsText.value = "";
  modalOpen.value = true;
}

async function openEdit(id: number) {
  try {
    const detail  = await getAiPromptTemplateById(id);

    if (!isMounted.value) return;
    editingId.value = id;
    Object.assign(form, {
      name: detail.name,
      description: detail.description ?? "",
      category: detail.category ?? "",
      content: detail.content,
      isSystem: detail.isSystem
    });
    tagsText.value = detail.tags.join(",");
    activeTab.value = "basic";
    modalOpen.value = true;
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.promptLib.loadDetailFailed"));
  }
}

function closeModal() {
  modalOpen.value = false;
  activeTab.value = "basic";
  formRef.value?.resetFields();
}

function parseTags() {
  return tagsText.value
    .split(",")
    .map((x) => x.trim())
    .filter((x) => x.length > 0);
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
    if (editingId.value) {
      await updateAiPromptTemplate(editingId.value, {
        name: form.name,
        description: form.description || undefined,
        category: form.category || undefined,
        content: form.content,
        tags: parseTags()
      });

      if (!isMounted.value) return;
      message.success(t("crud.updateSuccess"));
    } else {
      await createAiPromptTemplate({
        name: form.name,
        description: form.description || undefined,
        category: form.category || undefined,
        content: form.content,
        tags: parseTags(),
        isSystem: form.isSystem
      });

      if (!isMounted.value) return;
      message.success(t("crud.createSuccess"));
    }

    modalOpen.value = false;
    await loadData();

    if (!isMounted.value) return;
  } catch (error: unknown) {
    message.error((error as Error).message || t("crud.submitFailed"));
  } finally {
    modalLoading.value = false;
  }
}

async function handleDelete(id: number) {
  try {
    await deleteAiPromptTemplate(id);

    if (!isMounted.value) return;
    message.success(t("crud.deleteSuccess"));
    await loadData();

    if (!isMounted.value) return;
  } catch (error: unknown) {
    message.error((error as Error).message || t("crud.deleteFailed"));
  }
}

function openInsertModal() {
  insertModalOpen.value = true;
}

function handleInsertPrompt(content: string) {
  insertModalOpen.value = false;
  const textarea = getContentTextArea();
  if (!textarea) {
    form.content = form.content ? `${form.content}\n${content}` : content;
    activeTab.value = "preview";
    message.success(t("ai.promptLib.insertedToContent"));
    return;
  }

  const start = textarea.selectionStart ?? form.content.length;
  const end = textarea.selectionEnd ?? start;
  const before = form.content.slice(0, start);
  const after = form.content.slice(end);
  form.content = `${before}${content}${after}`;
  activeTab.value = "preview";
  const cursor = start + content.length;
  void nextTick(() => {
    const target = getContentTextArea();
    target?.focus();
    target?.setSelectionRange(cursor, cursor);
  });
  message.success(t("ai.promptLib.insertedToContent"));
}

function getContentTextArea(): HTMLTextAreaElement | null {
  const input = contentInputRef.value as { resizableTextArea?: { textArea?: HTMLTextAreaElement } } | null;
  return input?.resizableTextArea?.textArea ?? null;
}

onMounted(() => {
  void loadData();
});
</script>

<style scoped>
.toolbar {
  margin-bottom: 16px;
}

.pager {
  margin-top: 16px;
  display: flex;
  justify-content: flex-end;
}

.preview-block {
  margin: 0;
  padding: 12px;
  border-radius: 8px;
  background: #fafafa;
  max-height: 400px;
  overflow: auto;
  white-space: pre-wrap;
  word-break: break-all;
}

.prompt-editor-tabs {
  min-height: 520px;
}

.drawer-footer {
  display: flex;
  justify-content: flex-end;
}
</style>
