<template>
  <a-card :title="t('ai.plugin.listTitle')" :bordered="false">
    <div class="toolbar">
      <a-space wrap>
        <a-input-search
          v-model:value="keyword"
          :placeholder="t('ai.plugin.searchPlaceholder')"
          style="width: 260px"
          @search="loadData"
        />
        <a-button @click="showBuiltInMetadata">{{ t("ai.plugin.builtIn") }}</a-button>
        <a-button @click="handleReset">{{ t("common.reset") }}</a-button>
        <a-button type="primary" @click="openCreate">{{ t("ai.plugin.newPlugin") }}</a-button>
      </a-space>
    </div>

    <a-table row-key="id" :columns="columns" :data-source="list" :loading="loading" :pagination="false">
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'type'">
          <a-tag :color="record.type === 1 ? 'purple' : 'blue'">
            {{ record.type === 1 ? t("ai.plugin.typeBuiltIn") : t("ai.plugin.typeCustom") }}
          </a-tag>
        </template>
        <template v-if="column.key === 'status'">
          <a-tag :color="record.status === 1 ? 'green' : 'default'">
            {{ record.status === 1 ? t("ai.plugin.statusPublished") : t("ai.plugin.statusDraft") }}
          </a-tag>
        </template>
        <template v-if="column.key === 'lock'">
          <a-tag :color="record.isLocked ? 'red' : 'default'">
            {{ record.isLocked ? t("ai.plugin.locked") : t("ai.plugin.unlocked") }}
          </a-tag>
        </template>
        <template v-if="column.key === 'action'">
          <a-space>
            <a-button type="link" @click="goDetail(record.id)">{{ t("ai.plugin.detail") }}</a-button>
            <a-button type="link" @click="openEdit(record.id)">{{ t("common.edit") }}</a-button>
            <a-popconfirm :title="t('ai.plugin.deleteConfirm')" @confirm="handleDelete(record.id)">
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
      v-model:open="modalOpen"
      :title="editingId ? t('ai.plugin.modalEdit') : t('ai.plugin.modalCreate')"
      :confirm-loading="modalLoading"
      width="760px"
      @ok="submitForm"
      @cancel="closeModal"
    >
      <a-form ref="formRef" :model="form" layout="vertical" :rules="rules">
        <a-form-item :label="t('ai.promptLib.colName')" name="name">
          <a-input v-model:value="form.name" />
        </a-form-item>
        <a-form-item :label="t('ai.promptLib.labelDescription')" name="description">
          <a-textarea v-model:value="form.description" :rows="3" />
        </a-form-item>
        <a-form-item :label="t('ai.plugin.labelIcon')" name="icon">
          <a-input v-model:value="form.icon" />
        </a-form-item>
        <a-form-item :label="t('ai.promptLib.labelCategory')" name="category">
          <a-input v-model:value="form.category" />
        </a-form-item>
        <a-form-item :label="t('ai.plugin.labelType')" name="type">
          <a-select v-model:value="form.type" :options="typeOptions" />
        </a-form-item>
        <a-form-item :label="t('ai.plugin.labelSourceType')" name="sourceType">
          <a-select v-model:value="form.sourceType" :options="sourceTypeOptions" />
        </a-form-item>
        <a-form-item :label="t('ai.plugin.labelAuthType')" name="authType">
          <a-select v-model:value="form.authType" :options="authTypeOptions" />
        </a-form-item>
        <a-form-item :label="t('ai.plugin.labelAuthConfigJson')" name="authConfigJson">
          <a-textarea v-model:value="form.authConfigJson" :rows="4" />
        </a-form-item>
        <a-form-item :label="t('ai.plugin.labelDefinitionJson')" name="definitionJson">
          <a-textarea v-model:value="form.definitionJson" :rows="8" />
        </a-form-item>
        <a-form-item :label="t('ai.plugin.labelToolSchemaJson')" name="toolSchemaJson">
          <a-textarea v-model:value="form.toolSchemaJson" :rows="6" />
        </a-form-item>
        <a-form-item :label="t('ai.plugin.labelOpenApiSpecJson')" name="openApiSpecJson">
          <a-textarea v-model:value="form.openApiSpecJson" :rows="6" />
        </a-form-item>
      </a-form>
    </a-modal>

    <a-drawer v-model:open="builtInDrawerOpen" :title="t('ai.plugin.drawerBuiltInTitle')" width="760">
      <a-table row-key="code" :columns="builtInColumns" :data-source="builtInList" :pagination="false">
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'tags'">
            <a-space wrap>
              <a-tag v-for="tag in record.tags" :key="tag">{{ tag }}</a-tag>
            </a-space>
          </template>
        </template>
      </a-table>
    </a-drawer>
  </a-card>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, onUnmounted } from "vue";
import { useI18n } from "vue-i18n";

const { t } = useI18n();

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { useRoute, useRouter } from "vue-router";
import type { FormInstance } from "ant-design-vue";
import { message } from "ant-design-vue";
import {
  createAiPlugin,
  deleteAiPlugin,
  getAiPluginBuiltInMetadata,
  getAiPluginById,
  getAiPluginsPaged,
  updateAiPlugin,
  type AiPluginBuiltInMetaItem,
  type AiPluginAuthType,
  type AiPluginListItem,
  type AiPluginSourceType,
  type AiPluginType
} from "@/services/api-ai-plugin";
import { resolveCurrentAppId } from "@/utils/app-context";

const route = useRoute();
const router = useRouter();
const keyword = ref("");
const list = ref<AiPluginListItem[]>([]);
const loading = ref(false);
const pageIndex = ref(1);
const pageSize = ref(20);
const total = ref(0);

const columns = computed(() => [
  { title: t("ai.promptLib.colName"), dataIndex: "name", key: "name", width: 220 },
  { title: t("ai.promptLib.labelCategory"), dataIndex: "category", key: "category", width: 140 },
  { title: t("ai.plugin.labelType"), key: "type", width: 100 },
  { title: t("ai.workflow.colStatus"), key: "status", width: 100 },
  { title: t("ai.plugin.colLock"), key: "lock", width: 100 },
  { title: t("ai.workflow.colUpdatedAt"), dataIndex: "updatedAt", key: "updatedAt", width: 200 },
  { title: t("ai.colActions"), key: "action", width: 180 }
]);

const typeOptions = computed(() => [
  { label: t("ai.plugin.typeCustom"), value: 0 },
  { label: t("ai.plugin.typeBuiltIn"), value: 1 }
]);
const sourceTypeOptions = computed(() => [
  { label: t("ai.plugin.sourceManual"), value: 0 },
  { label: t("ai.plugin.sourceOpenApi"), value: 1 },
  { label: t("ai.plugin.sourceBuiltInCatalog"), value: 2 }
]);
const authTypeOptions = computed(() => [
  { label: t("ai.plugin.authNone"), value: 0 },
  { label: "API Key", value: 1 },
  { label: "Bearer Token", value: 2 },
  { label: "Basic", value: 3 },
  { label: t("ai.plugin.authCustom"), value: 4 }
]);

const modalOpen = ref(false);
const modalLoading = ref(false);
const editingId = ref<number | null>(null);
const formRef = ref<FormInstance>();
const form = reactive({
  name: "",
  description: "",
  icon: "",
  category: "",
  type: 0 as AiPluginType,
  sourceType: 0 as AiPluginSourceType,
  authType: 0 as AiPluginAuthType,
  authConfigJson: "{}",
  definitionJson: "{}",
  toolSchemaJson: "[]",
  openApiSpecJson: "{}"
});
const rules = computed(() => ({
  name: [{ required: true, message: t("ai.plugin.ruleName") }]
}));

const builtInDrawerOpen = ref(false);
const builtInList = ref<AiPluginBuiltInMetaItem[]>([]);
const builtInColumns = computed(() => [
  { title: "Code", dataIndex: "code", key: "code", width: 180 },
  { title: t("ai.promptLib.colName"), dataIndex: "name", key: "name", width: 150 },
  { title: t("ai.promptLib.labelCategory"), dataIndex: "category", key: "category", width: 120 },
  { title: t("ai.promptLib.labelDescription"), dataIndex: "description", key: "description" },
  { title: t("ai.promptLib.colTags"), key: "tags", width: 220 }
]);

async function loadData() {
  loading.value = true;
  try {
    const result  = await getAiPluginsPaged(
      { pageIndex: pageIndex.value, pageSize: pageSize.value },
      keyword.value || undefined
    );

    if (!isMounted.value) return;
    list.value = result.items;
    total.value = Number(result.total);
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.plugin.loadListFailed"));
  } finally {
    loading.value = false;
  }
}

function handleReset() {
  keyword.value = "";
  pageIndex.value = 1;
  void loadData();
}

function goDetail(id: number) {
  const currentAppId = resolveCurrentAppId(route);
  if (!currentAppId) {
    void router.push("/console/apps");
    return;
  }
  void router.push(`/apps/${currentAppId}/plugins/${id}`);
}

function openCreate() {
  editingId.value = null;
  Object.assign(form, {
    name: "",
    description: "",
    icon: "",
    category: "",
    type: 0 as AiPluginType,
    sourceType: 0 as AiPluginSourceType,
    authType: 0 as AiPluginAuthType,
    authConfigJson: "{}",
    definitionJson: "{}",
    toolSchemaJson: "[]",
    openApiSpecJson: "{}"
  });
  modalOpen.value = true;
}

async function openEdit(id: number) {
  try {
    const detail  = await getAiPluginById(id);

    if (!isMounted.value) return;
    editingId.value = id;
    Object.assign(form, {
      name: detail.name,
      description: detail.description ?? "",
      icon: detail.icon ?? "",
      category: detail.category ?? "",
      type: detail.type,
      sourceType: detail.sourceType,
      authType: detail.authType,
      authConfigJson: detail.authConfigJson,
      definitionJson: detail.definitionJson,
      toolSchemaJson: detail.toolSchemaJson,
      openApiSpecJson: detail.openApiSpecJson
    });
    modalOpen.value = true;
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.plugin.loadDetailFailed"));
  }
}

function closeModal() {
  modalOpen.value = false;
  formRef.value?.resetFields();
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
    const payload = {
      name: form.name,
      description: form.description || undefined,
      icon: form.icon || undefined,
      category: form.category || undefined,
      type: form.type,
      sourceType: form.sourceType,
      authType: form.authType,
      authConfigJson: form.authConfigJson || undefined,
      definitionJson: form.definitionJson || undefined,
      toolSchemaJson: form.toolSchemaJson || undefined,
      openApiSpecJson: form.openApiSpecJson || undefined
    };

    if (editingId.value) {
      await updateAiPlugin(editingId.value, payload);

      if (!isMounted.value) return;
      message.success(t("crud.updateSuccess"));
    } else {
      await createAiPlugin(payload);

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
    await deleteAiPlugin(id);

    if (!isMounted.value) return;
    message.success(t("crud.deleteSuccess"));
    await loadData();

    if (!isMounted.value) return;
  } catch (error: unknown) {
    message.error((error as Error).message || t("crud.deleteFailed"));
  }
}

async function showBuiltInMetadata() {
  try {
    builtInList.value = await getAiPluginBuiltInMetadata();

    if (!isMounted.value) return;
    builtInDrawerOpen.value = true;
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.plugin.loadBuiltInFailed"));
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
