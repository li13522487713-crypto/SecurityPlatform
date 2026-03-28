<template>
  <a-card :title="t('ai.agent.listTitle')" :bordered="false">
    <div class="toolbar">
      <a-space wrap>
        <a-input-search
          v-model:value="keyword"
          :placeholder="t('ai.agent.searchPlaceholder')"
          style="width: 260px"
          @search="loadData"
        />
        <a-segmented v-model:value="statusFilter" :options="statusOptions" @change="handleFilterChange" />
        <a-button type="primary" @click="openCreate">{{ t("ai.agent.newAgent") }}</a-button>
      </a-space>
    </div>

    <a-row :gutter="[16, 16]">
      <a-col v-for="item in list" :key="item.id" :xs="24" :sm="12" :lg="8" :xl="6">
        <a-card class="agent-card" hoverable>
          <template #title>
            <a-space>
              <a-avatar :src="item.avatarUrl">{{ item.name.slice(0, 1) }}</a-avatar>
              <span>{{ item.name }}</span>
            </a-space>
          </template>
          <template #extra>
            <a-tag :color="statusColor(item.status)">{{ item.status }}</a-tag>
          </template>
          <p class="description">{{ item.description || t("ai.agent.noDescription") }}</p>
          <p class="meta">{{ t("ai.agent.modelLabel", { name: item.modelName || "-" }) }}</p>
          <p class="meta">{{ t("ai.agent.versionLabel", { version: item.publishVersion }) }}</p>
          <a-space>
            <a-button type="link" size="small" @click="goEdit(item.id)">{{ t("common.edit") }}</a-button>
            <a-button type="link" size="small" @click="handleDuplicate(item.id)">{{ t("ai.agent.duplicate") }}</a-button>
            <a-popconfirm :title="t('ai.agent.deleteConfirm')" @confirm="handleDelete(item.id)">
              <a-button type="link" danger size="small">{{ t("common.delete") }}</a-button>
            </a-popconfirm>
          </a-space>
        </a-card>
      </a-col>
    </a-row>

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
      :title="t('ai.agent.modalCreateTitle')"
      :confirm-loading="modalLoading"
      @ok="submitCreate"
      @cancel="closeModal"
    >
      <a-form ref="formRef" :model="form" layout="vertical" :rules="rules">
        <a-form-item :label="t('ai.promptLib.colName')" name="name">
          <a-input v-model:value="form.name" />
        </a-form-item>
        <a-form-item :label="t('ai.promptLib.labelDescription')" name="description">
          <a-textarea v-model:value="form.description" :rows="3" />
        </a-form-item>
        <a-form-item :label="t('ai.agent.labelModelConfig')">
          <a-select
            v-model:value="form.modelConfigId"
            allow-clear
            show-search
            :options="modelOptions"
            :filter-option="false"
            @search="handleModelSearch"
          />
        </a-form-item>
      </a-form>
    </a-modal>
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
import { message } from "ant-design-vue";
import type { FormInstance } from "ant-design-vue";
import {
  createAgent,
  deleteAgent,
  duplicateAgent,
  getAgentsPaged,
  type AgentListItem
} from "@/services/api-agent";
import { getEnabledModelConfigs, type ModelConfigDto } from "@/services/api-model-config";
import { resolveCurrentAppId } from "@/utils/app-context";

const route = useRoute();
const router = useRouter();
const list = ref<AgentListItem[]>([]);
const keyword = ref("");
const loading = ref(false);
const pageIndex = ref(1);
const pageSize = ref(20);
const total = ref(0);
const statusFilter = ref<string>("All");

const statusOptions = ["All", "Draft", "Published", "Disabled"];

const modelConfigs = ref<ModelConfigDto[]>([]);
const modelOptions = computed(() =>
  modelConfigs.value.map((item) => ({
    label: `${item.name} (${item.providerType})`,
    value: item.id
  }))
);

const modalVisible = ref(false);
const modalLoading = ref(false);
const formRef = ref<FormInstance>();
const form = reactive({
  name: "",
  description: "",
  modelConfigId: undefined as string | undefined
});

const rules = computed(() => ({
  name: [{ required: true, message: t("ai.agent.ruleName") }]
}));

function statusColor(status: string) {
  if (status === "Published") return "green";
  if (status === "Disabled") return "default";
  return "blue";
}

function handleFilterChange() {
  pageIndex.value = 1;
  void loadData();
}

async function loadData() {
  loading.value = true;
  try {
    const result  = await getAgentsPaged(
      {
        pageIndex: pageIndex.value,
        pageSize: pageSize.value,
        keyword: keyword.value || undefined
      },
      statusFilter.value === "All" ? undefined : statusFilter.value
    );

    if (!isMounted.value) return;
    list.value = result.items;
    total.value = Number(result.total);
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.agent.loadFailed"));
  } finally {
    loading.value = false;
  }
}

async function loadModelConfigs(keywordText?: string) {
  const result  = await getEnabledModelConfigs();

  if (!isMounted.value) return;
  if (!keywordText) {
    modelConfigs.value = result;
    return;
  }

  const search = keywordText.toLowerCase();
  modelConfigs.value = result.filter((item) =>
    item.name.toLowerCase().includes(search) || item.providerType.toLowerCase().includes(search)
  );
}

function handleModelSearch(value: string) {
  void loadModelConfigs(value);
}

function goEdit(id: string) {
  const currentAppId = resolveCurrentAppId(route);
  if (!currentAppId) {
    void router.push("/console/apps");
    return;
  }
  void router.push(`/apps/${currentAppId}/agents/${id}/edit`);
}

function openCreate() {
  Object.assign(form, { name: "", description: "", modelConfigId: undefined });
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
    await createAgent({
      name: form.name,
      description: form.description || undefined,
      modelConfigId: form.modelConfigId
    });

    if (!isMounted.value) return;
    message.success(t("crud.createSuccess"));
    modalVisible.value = false;
    await loadData();

    if (!isMounted.value) return;
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.agent.createFailed"));
  } finally {
    modalLoading.value = false;
  }
}

async function handleDuplicate(id: string) {
  try {
    await duplicateAgent(id);

    if (!isMounted.value) return;
    message.success(t("ai.workflow.copySuccess"));
    await loadData();

    if (!isMounted.value) return;
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.agent.duplicateFailed"));
  }
}

async function handleDelete(id: string) {
  try {
    await deleteAgent(id);

    if (!isMounted.value) return;
    message.success(t("crud.deleteSuccess"));
    await loadData();

    if (!isMounted.value) return;
  } catch (error: unknown) {
    message.error((error as Error).message || t("crud.deleteFailed"));
  }
}

onMounted(async () => {
  await Promise.all([loadData(), loadModelConfigs()]);

  if (!isMounted.value) return;
});
</script>

<style scoped>
.toolbar {
  margin-bottom: 16px;
}

.agent-card {
  height: 100%;
}

.description {
  min-height: 44px;
  color: rgba(0, 0, 0, 0.65);
}

.meta {
  margin-bottom: 6px;
  color: rgba(0, 0, 0, 0.45);
}

.pager {
  margin-top: 16px;
  display: flex;
  justify-content: flex-end;
}
</style>
