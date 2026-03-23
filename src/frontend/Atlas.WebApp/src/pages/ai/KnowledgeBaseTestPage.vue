<template>
  <a-card :bordered="false">
    <template #title>
      <a-space>
        <a-button type="link" @click="goBack">{{ t("ai.knowledgeBase.back") }}</a-button>
        <span>{{ t("ai.knowledgeBase.testPageTitle") }}</span>
      </a-space>
    </template>

    <a-form layout="vertical">
      <a-form-item :label="t('ai.knowledgeBase.testQuery')">
        <a-textarea v-model:value="queryText" :rows="3" :placeholder="t('ai.knowledgeBase.testQueryPlaceholder')" />
      </a-form-item>
      <a-row :gutter="12">
        <a-col :span="8">
          <a-form-item :label="t('ai.knowledgeBase.testTopK')">
            <a-input-number v-model:value="topK" :min="1" :max="50" style="width: 100%" />
          </a-form-item>
        </a-col>
        <a-col :span="16">
          <a-form-item :label="t('ai.knowledgeBase.testCurrentStrategy')">
            <a-input :value="currentStrategyLabel" disabled />
          </a-form-item>
        </a-col>
      </a-row>
      <a-space>
        <a-button type="primary" :loading="loading" @click="handleTest">
          {{ t("ai.knowledgeBase.testRun") }}
        </a-button>
      </a-space>
    </a-form>

    <a-divider />

    <a-table
      row-key="chunkId"
      :columns="columns"
      :data-source="results"
      :loading="loading"
      :pagination="false"
      :locale="{ emptyText: t('ai.knowledgeBase.testEmpty') }"
    >
      <template #bodyCell="{ column, record }">
        <template v-if="column.key === 'score'">
          {{ Number(record.score).toFixed(4) }}
        </template>
        <template v-if="column.key === 'content'">
          <a-typography-paragraph :ellipsis="{ rows: 2, expandable: true, symbol: t('common.more') }">
            {{ record.content }}
          </a-typography-paragraph>
        </template>
      </template>
    </a-table>
  </a-card>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from "vue";
import { useI18n } from "vue-i18n";
import { useRoute, useRouter } from "vue-router";
import { message } from "ant-design-vue";
import {
  getKnowledgeRetrievalConfig,
  testKnowledgeRetrieval,
  type KnowledgeRetrievalConfigDto,
  type KnowledgeRetrievalTestItem
} from "@/services/api-knowledge";
import { resolveCurrentAppId } from "@/utils/app-context";

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const knowledgeBaseId = Number(route.params["id"]);

const loading = ref(false);
const queryText = ref("");
const topK = ref(10);
const results = ref<KnowledgeRetrievalTestItem[]>([]);
const retrievalConfig = ref<KnowledgeRetrievalConfigDto>({
  strategy: "hybrid",
  enableRerank: true,
  vectorTopK: 12,
  bm25TopK: 12,
  bm25CandidateCount: 300,
  rrfK: 60
});

const columns = computed(() => [
  { title: t("ai.knowledgeBase.testColChunkId"), dataIndex: "chunkId", key: "chunkId", width: 120 },
  { title: t("ai.knowledgeBase.testColDocument"), dataIndex: "documentName", key: "documentName", width: 220 },
  { title: t("ai.knowledgeBase.testColScore"), key: "score", width: 120 },
  { title: t("ai.knowledgeBase.testColContent"), key: "content" }
]);

const currentStrategyLabel = computed(() => {
  switch (retrievalConfig.value.strategy) {
    case "vector":
      return t("ai.knowledgeBase.strategyVector");
    case "bm25":
      return t("ai.knowledgeBase.strategyBm25");
    default:
      return t("ai.knowledgeBase.strategyHybrid");
  }
});

function goBack() {
  const currentAppId = resolveCurrentAppId(route);
  if (currentAppId) {
    void router.push(`/apps/${currentAppId}/knowledge-bases/${knowledgeBaseId}`);
    return;
  }

  void router.push(`/ai/knowledge-bases/${knowledgeBaseId}`);
}

async function loadRetrievalConfig() {
  try {
    retrievalConfig.value = await getKnowledgeRetrievalConfig(knowledgeBaseId);
  } catch {
    // 后端未提供该接口时使用默认值，不阻断测试页使用。
  }
}

async function handleTest() {
  if (!queryText.value.trim()) {
    message.warning(t("ai.knowledgeBase.testQueryRequired"));
    return;
  }

  loading.value = true;
  try {
    results.value = await testKnowledgeRetrieval(knowledgeBaseId, {
      query: queryText.value.trim(),
      topK: topK.value
    });
  } catch (error: unknown) {
    message.error((error as Error).message || t("ai.knowledgeBase.testRunFailed"));
  } finally {
    loading.value = false;
  }
}

onMounted(() => {
  void loadRetrievalConfig();
});
</script>
