<template>
  <a-card :title="t('ai.evaluation.datasetPageTitle')" :bordered="false">
    <div class="toolbar">
      <a-space wrap>
        <a-input-search
          v-model:value="keyword"
          :placeholder="t('ai.evaluation.datasetSearchPlaceholder')"
          style="width: 280px"
          @search="loadDatasets"
        />
        <a-button type="primary" @click="openDatasetModal">
          {{ t("ai.evaluation.newDataset") }}
        </a-button>
        <a-button @click="goTaskPage()">
          {{ t("ai.evaluation.gotoTaskPage") }}
        </a-button>
      </a-space>
    </div>

    <a-table
      row-key="id"
      :columns="columns"
      :data-source="rows"
      :loading="loading"
      :pagination="false"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'updatedAt'">
          {{ formatDateTime(record.updatedAt) }}
        </template>
        <template v-else-if="column.key === 'action'">
          <a-space>
            <a-button type="link" @click="openCases(record.id)">
              {{ t("ai.evaluation.viewCases") }}
            </a-button>
            <a-button type="link" @click="goTaskPage(record.id)">
              {{ t("ai.evaluation.createTask") }}
            </a-button>
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
        @change="loadDatasets"
      />
    </div>
  </a-card>

  <a-modal
    v-model:open="datasetModalOpen"
    :title="t('ai.evaluation.newDataset')"
    :confirm-loading="datasetSaving"
    @ok="handleCreateDataset"
  >
    <a-form ref="datasetFormRef" layout="vertical" :model="datasetForm" :rules="datasetRules">
      <a-form-item :label="t('ai.evaluation.datasetName')" name="name">
        <a-input v-model:value="datasetForm.name" :maxlength="64" />
      </a-form-item>
      <a-form-item :label="t('ai.evaluation.datasetScene')" name="scene">
        <a-input v-model:value="datasetForm.scene" :maxlength="64" />
      </a-form-item>
      <a-form-item :label="t('ai.evaluation.datasetDescription')" name="description">
        <a-textarea v-model:value="datasetForm.description" :rows="3" :maxlength="500" />
      </a-form-item>
    </a-form>
  </a-modal>

  <a-drawer
    v-model:open="caseDrawerOpen"
    :title="t('ai.evaluation.caseDrawerTitle', { name: activeDatasetName })"
    width="900"
  >
    <div class="drawer-toolbar">
      <a-button type="primary" @click="openCaseModal">
        {{ t("ai.evaluation.newCase") }}
      </a-button>
    </div>
    <a-table
      row-key="id"
      :columns="caseColumns"
      :data-source="caseRows"
      :loading="caseLoading"
      :pagination="false"
      size="small"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'tags'">
          <a-space wrap>
            <a-tag v-for="tag in record.tags" :key="tag">{{ tag }}</a-tag>
            <span v-if="record.tags.length === 0">-</span>
          </a-space>
        </template>
        <template v-else-if="column.key === 'createdAt'">
          {{ formatDateTime(record.createdAt) }}
        </template>
      </template>
    </a-table>
  </a-drawer>

  <a-modal
    v-model:open="caseModalOpen"
    :title="t('ai.evaluation.newCase')"
    :confirm-loading="caseSaving"
    width="860px"
    @ok="handleCreateCase"
  >
    <a-form ref="caseFormRef" layout="vertical" :model="caseForm" :rules="caseRules">
      <a-form-item :label="t('ai.evaluation.caseInput')" name="input">
        <a-textarea v-model:value="caseForm.input" :rows="3" :maxlength="2000" />
      </a-form-item>
      <a-form-item :label="t('ai.evaluation.caseExpectedOutput')" name="expectedOutput">
        <a-textarea v-model:value="caseForm.expectedOutput" :rows="2" :maxlength="2000" />
      </a-form-item>
      <a-form-item :label="t('ai.evaluation.caseReferenceOutput')" name="referenceOutput">
        <a-textarea v-model:value="caseForm.referenceOutput" :rows="2" :maxlength="2000" />
      </a-form-item>
      <a-form-item :label="t('ai.evaluation.caseTags')" name="tagsText">
        <a-input
          v-model:value="caseForm.tagsText"
          :placeholder="t('ai.evaluation.caseTagsPlaceholder')"
          :maxlength="300"
        />
      </a-form-item>
    </a-form>
  </a-modal>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from "vue";
import type { FormInstance } from "ant-design-vue";
import { message } from "ant-design-vue";
import { useI18n } from "vue-i18n";
import { useRoute, useRouter } from "vue-router";
import {
  createEvaluationCase,
  createEvaluationDataset,
  getEvaluationCases,
  getEvaluationDatasetsPaged,
  type EvaluationCaseDto,
  type EvaluationDatasetDto
} from "@/services/api-evaluation";
import { formatDateTime } from "@/utils/common";
import { resolveCurrentAppId } from "@/utils/app-context";

const { t } = useI18n();
const route = useRoute();
const router = useRouter();

const rows = ref<EvaluationDatasetDto[]>([]);
const loading = ref(false);
const keyword = ref("");
const pageIndex = ref(1);
const pageSize = ref(20);
const total = ref(0);

const datasetModalOpen = ref(false);
const datasetSaving = ref(false);
const datasetFormRef = ref<FormInstance>();
const datasetForm = reactive({
  name: "",
  description: "",
  scene: ""
});

const datasetRules = computed(() => ({
  name: [{ required: true, message: t("ai.evaluation.ruleDatasetName") }]
}));

const caseDrawerOpen = ref(false);
const caseLoading = ref(false);
const caseRows = ref<EvaluationCaseDto[]>([]);
const activeDatasetId = ref<number>(0);
const activeDatasetName = ref("");

const caseModalOpen = ref(false);
const caseSaving = ref(false);
const caseFormRef = ref<FormInstance>();
const caseForm = reactive({
  input: "",
  expectedOutput: "",
  referenceOutput: "",
  tagsText: ""
});

const caseRules = computed(() => ({
  input: [{ required: true, message: t("ai.evaluation.ruleCaseInput") }]
}));

const columns = computed(() => [
  { title: t("ai.evaluation.datasetName"), dataIndex: "name", key: "name" },
  { title: t("ai.evaluation.datasetScene"), dataIndex: "scene", key: "scene", width: 180 },
  { title: t("ai.evaluation.datasetCaseCount"), dataIndex: "caseCount", key: "caseCount", width: 130 },
  { title: t("ai.evaluation.datasetUpdatedAt"), dataIndex: "updatedAt", key: "updatedAt", width: 190 },
  { title: t("ai.colActions"), key: "action", width: 200 }
]);

const caseColumns = computed(() => [
  { title: t("ai.evaluation.caseInput"), dataIndex: "input", key: "input", ellipsis: true },
  { title: t("ai.evaluation.caseExpectedOutput"), dataIndex: "expectedOutput", key: "expectedOutput", ellipsis: true },
  { title: t("ai.evaluation.caseTags"), dataIndex: "tags", key: "tags", width: 180 },
  { title: t("ai.evaluation.caseCreatedAt"), dataIndex: "createdAt", key: "createdAt", width: 180 }
]);

async function loadDatasets() {
  loading.value = true;
  try {
    const result = await getEvaluationDatasetsPaged({
      pageIndex: pageIndex.value,
      pageSize: pageSize.value,
      keyword: keyword.value || undefined
    });
    rows.value = result.items;
    total.value = Number(result.total);
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.evaluation.datasetLoadFailed"));
  } finally {
    loading.value = false;
  }
}

function openDatasetModal() {
  datasetForm.name = "";
  datasetForm.scene = "";
  datasetForm.description = "";
  datasetModalOpen.value = true;
}

async function handleCreateDataset() {
  try {
    await datasetFormRef.value?.validate();
  } catch {
    return;
  }

  datasetSaving.value = true;
  try {
    await createEvaluationDataset({
      name: datasetForm.name.trim(),
      scene: datasetForm.scene.trim() || undefined,
      description: datasetForm.description.trim() || undefined
    });
    message.success(t("crud.createSuccess"));
    datasetModalOpen.value = false;
    await loadDatasets();
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.evaluation.datasetCreateFailed"));
  } finally {
    datasetSaving.value = false;
  }
}

async function openCases(datasetId: number) {
  const dataset = rows.value.find((item) => item.id === datasetId);
  activeDatasetId.value = datasetId;
  activeDatasetName.value = dataset?.name || `#${datasetId}`;
  caseDrawerOpen.value = true;
  await loadCases();
}

async function loadCases() {
  if (!activeDatasetId.value) {
    return;
  }

  caseLoading.value = true;
  try {
    caseRows.value = await getEvaluationCases(activeDatasetId.value);
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.evaluation.caseLoadFailed"));
  } finally {
    caseLoading.value = false;
  }
}

function openCaseModal() {
  caseForm.input = "";
  caseForm.expectedOutput = "";
  caseForm.referenceOutput = "";
  caseForm.tagsText = "";
  caseModalOpen.value = true;
}

async function handleCreateCase() {
  if (!activeDatasetId.value) {
    return;
  }

  try {
    await caseFormRef.value?.validate();
  } catch {
    return;
  }

  const tags = caseForm.tagsText
    .split(",")
    .map((tag) => tag.trim())
    .filter(Boolean);

  caseSaving.value = true;
  try {
    await createEvaluationCase(activeDatasetId.value, {
      input: caseForm.input.trim(),
      expectedOutput: caseForm.expectedOutput.trim() || undefined,
      referenceOutput: caseForm.referenceOutput.trim() || undefined,
      tags
    });
    message.success(t("crud.createSuccess"));
    caseModalOpen.value = false;
    await Promise.all([loadCases(), loadDatasets()]);
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.evaluation.caseCreateFailed"));
  } finally {
    caseSaving.value = false;
  }
}

function goTaskPage(datasetId?: number) {
  const currentAppId = resolveCurrentAppId(route);
  const query = datasetId ? { datasetId: String(datasetId) } : undefined;
  if (currentAppId) {
    void router.push({
      path: `/apps/${currentAppId}/evaluations/tasks`,
      query
    });
    return;
  }

  void router.push({
    path: "/ai/devops/evaluations/tasks",
    query
  });
}

onMounted(() => {
  void loadDatasets();
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

.drawer-toolbar {
  margin-bottom: 12px;
}
</style>
