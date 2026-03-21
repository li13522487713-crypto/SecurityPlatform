<template>
  <a-space direction="vertical" style="width: 100%" :size="16">
    <a-card :title="t('ai.mockSets.pageTitle')" :bordered="false">
      <a-table :columns="columns" :data-source="mockSets" row-key="id" :pagination="false">
        <template #bodyCell="{ column, record }">
          <template v-if="column.key === 'status'">
            <a-tag :color="record.enabled ? 'green' : 'default'">
              {{ record.enabled ? t("common.statusEnabled") : t("common.statusDisabled") }}
            </a-tag>
          </template>
          <template v-if="column.key === 'action'">
            <a-button type="link" @click="previewMock(record.id)">{{ t("ai.mockSets.preview") }}</a-button>
          </template>
        </template>
      </a-table>
    </a-card>

    <PreviewPanel :title="previewTitle" :content="previewContent" />
  </a-space>
</template>

<script setup lang="ts">
import { computed, ref } from "vue";
import { useI18n } from "vue-i18n";
import PreviewPanel from "@/components/ai/PreviewPanel.vue";

const { t } = useI18n();

interface MockSetItem {
  id: number;
  name: string;
  targetApi: string;
  enabled: boolean;
  mockResponse: string;
}

const mockSets = computed<MockSetItem[]>(() => [
  {
    id: 1,
    name: t("ai.mockSets.demo1Name"),
    targetApi: t("ai.mockSets.demo1Api"),
    enabled: true,
    mockResponse: t("ai.mockSets.demo1Response")
  },
  {
    id: 2,
    name: t("ai.mockSets.demo2Name"),
    targetApi: t("ai.mockSets.demo2Api"),
    enabled: false,
    mockResponse: t("ai.mockSets.demo2Response")
  }
]);

const selectedId = ref<number>(1);

const columns = computed(() => [
  { title: t("ai.promptLib.colName"), dataIndex: "name", key: "name" },
  { title: t("ai.mockSets.colTargetApi"), dataIndex: "targetApi", key: "targetApi" },
  { title: t("ai.workflow.colStatus"), key: "status", width: 100 },
  { title: t("ai.colActions"), key: "action", width: 120 }
]);

const selectedItem = computed(() => mockSets.value.find((x) => x.id === selectedId.value));
const previewTitle = computed(() => selectedItem.value?.name ?? t("ai.mockSets.previewDefault"));
const previewContent = computed(() => selectedItem.value?.mockResponse ?? t("ai.mockSets.previewEmpty"));

function previewMock(id: number) {
  selectedId.value = id;
}
</script>
