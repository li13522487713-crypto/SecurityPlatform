<template>
  <a-card :title="t('ai.knowledgeBase.listTitle')" :bordered="false">
    <div class="toolbar">
      <a-space wrap>
        <a-input-search
          v-model:value="keyword"
          :placeholder="t('ai.knowledgeBase.searchPlaceholder')"
          style="width: 260px"
          @search="loadData"
        />
        <a-button type="primary" @click="openCreate">{{ t("ai.knowledgeBase.newKb") }}</a-button>
      </a-space>
    </div>

    <a-table
      row-key="id"
      :columns="columns"
      :data-source="list"
      :loading="loading"
      :pagination="false"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'type'">
          <a-tag :color="typeColor(record.type)">{{ typeLabel(record.type) }}</a-tag>
        </template>
        <template v-if="column.key === 'action'">
          <a-space>
            <a-button type="link" @click="goDetail(record.id)">{{ t("ai.knowledgeBase.detail") }}</a-button>
            <a-popconfirm :title="t('ai.knowledgeBase.deleteKbConfirm')" @confirm="handleDelete(record.id)">
              <a-button type="link" danger>{{ t("common.delete") }}</a-button>
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

    <a-modal
      v-model:open="modalVisible"
      :title="t('ai.knowledgeBase.modalCreateTitle')"
      :confirm-loading="modalLoading"
      @ok="submitCreate"
      @cancel="closeModal"
    >
      <a-form ref="formRef" :model="form" layout="vertical" :rules="rules">
        <a-form-item :label="t('ai.knowledgeBase.labelName')" name="name">
          <a-input v-model:value="form.name" />
        </a-form-item>
        <a-form-item :label="t('ai.knowledgeBase.labelDescription')" name="description">
          <a-textarea v-model:value="form.description" :rows="3" />
        </a-form-item>
        <a-form-item :label="t('ai.knowledgeBase.labelType')" name="type">
          <a-select v-model:value="form.type" :options="typeOptions" />
        </a-form-item>
      </a-form>
    </a-modal>
  </a-card>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, onUnmounted } from "vue";
import { useI18n } from "vue-i18n";

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { useRoute, useRouter } from "vue-router";
import { message } from "ant-design-vue";
import type { FormInstance } from "ant-design-vue";
import {
  createKnowledgeBase,
  deleteKnowledgeBase,
  getKnowledgeBasesPaged,
  type KnowledgeBaseDto
} from "@/services/api-knowledge";
import { resolveCurrentAppId } from "@/utils/app-context";

const { t } = useI18n();

const route = useRoute();
const router = useRouter();
const list = ref<KnowledgeBaseDto[]>([]);
const loading = ref(false);
const keyword = ref("");
const pageIndex = ref(1);
const pageSize = ref(20);
const total = ref(0);

const columns = computed(() => [
  { title: t("ai.promptLib.colName"), dataIndex: "name", key: "name" },
  { title: t("ai.knowledgeBase.labelType"), dataIndex: "type", key: "type", width: 120 },
  { title: t("ai.knowledgeBase.colDocCount"), dataIndex: "documentCount", key: "documentCount", width: 120 },
  { title: t("ai.knowledgeBase.colChunkCount"), dataIndex: "chunkCount", key: "chunkCount", width: 120 },
  { title: t("ai.knowledgeBase.colCreatedAt"), dataIndex: "createdAt", key: "createdAt", width: 220 },
  { title: t("ai.colActions"), key: "action", width: 180 }
]);

const typeOptions = computed(() => [
  { label: t("ai.knowledgeBase.typeText"), value: 0 },
  { label: t("ai.knowledgeBase.typeTable"), value: 1 },
  { label: t("ai.knowledgeBase.typeImage"), value: 2 }
]);

const modalVisible = ref(false);
const modalLoading = ref(false);
const formRef = ref<FormInstance>();
const form = reactive({
  name: "",
  description: "",
  type: 0 as 0 | 1 | 2
});

const rules = computed(() => ({
  name: [{ required: true, message: t("ai.knowledgeBase.ruleName") }]
}));

function typeLabel(type: number) {
  if (type === 1) return t("ai.knowledgeBase.typeTable");
  if (type === 2) return t("ai.knowledgeBase.typeImage");
  return t("ai.knowledgeBase.typeText");
}

function typeColor(type: number) {
  if (type === 1) return "purple";
  if (type === 2) return "orange";
  return "blue";
}

async function loadData() {
  loading.value = true;
  try {
    const result  = await getKnowledgeBasesPaged(
      { pageIndex: pageIndex.value, pageSize: pageSize.value },
      keyword.value || undefined
    );

    if (!isMounted.value) return;
    list.value = result.items;
    total.value = Number(result.total);
  } catch (err: unknown) {
    message.error((err as Error).message || t("ai.knowledgeBase.loadFailed"));
  } finally {
    loading.value = false;
  }
}

function goDetail(id: number) {
  const currentAppId = resolveCurrentAppId(route);
  if (currentAppId) {
    void router.push(`/apps/${currentAppId}/knowledge-bases/${id}`);
    return;
  }

  void router.push(`/ai/knowledge-bases/${id}`);
}

function openCreate() {
  Object.assign(form, { name: "", description: "", type: 0 });
  modalVisible.value = true;
}

function closeModal() {
  modalVisible.value = false;
  formRef.value?.resetFields();
}

async function submitCreate() {
  try {
    await formRef.value?.validate();

    if (!isMounted.value) return;
  } catch {
    return;
  }

  modalLoading.value = true;
  try {
    await createKnowledgeBase({
      name: form.name,
      description: form.description || undefined,
      type: form.type
    });

    if (!isMounted.value) return;
    message.success(t("crud.createSuccess"));
    modalVisible.value = false;
    await loadData();

    if (!isMounted.value) return;
  } catch (err: unknown) {
    message.error((err as Error).message || t("crud.submitFailed"));
  } finally {
    modalLoading.value = false;
  }
}

async function handleDelete(id: number) {
  try {
    await deleteKnowledgeBase(id);

    if (!isMounted.value) return;
    message.success(t("crud.deleteSuccess"));
    await loadData();

    if (!isMounted.value) return;
  } catch (err: unknown) {
    message.error((err as Error).message || t("crud.deleteFailed"));
  }
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
</style>
