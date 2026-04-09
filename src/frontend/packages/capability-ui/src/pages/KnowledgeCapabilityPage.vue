<template>
  <section class="capability-page">
    <header class="capability-page__header">
      <h2 class="capability-page__title">Knowledge</h2>
      <p class="capability-page__desc">
        hostMode={{ context.hostMode }} appKey={{ context.appKey ?? "-" }}
      </p>
    </header>

    <div class="capability-page__actions">
      <button type="button" class="capability-action" @click="goTo(knowledgePath)">KnowledgeBases</button>
      <button type="button" class="capability-action" @click="goTo(modelPath)">ModelConfigs</button>
    </div>
  </section>
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useRouter } from "vue-router";
import { useCapabilityHostContext } from "@atlas/shared-kernel/context";

const router = useRouter();
const hostContext = useCapabilityHostContext();
const context = computed(() => hostContext.context.value);

const knowledgePath = computed(() => {
  if (context.value.hostMode === "app" && context.value.appKey) {
    return `/apps/${context.value.appKey}/knowledge-bases`;
  }
  return "/ai/knowledge-bases";
});

const modelPath = computed(() => {
  if (context.value.hostMode === "app" && context.value.appKey) {
    return `/apps/${context.value.appKey}/model-configs`;
  }
  return "/ai/model-configs";
});

function goTo(path: string) {
  void router.push(path);
}
</script>

<style scoped>
.capability-page {
  background: #fff;
  border-radius: 12px;
  border: 1px solid #e5e7eb;
  padding: 16px;
}

.capability-page__header {
  margin-bottom: 12px;
}

.capability-page__title {
  margin: 0;
  font-size: 20px;
}

.capability-page__desc {
  margin: 6px 0 0;
  color: #6b7280;
  font-size: 13px;
}

.capability-page__actions {
  display: flex;
  gap: 10px;
  flex-wrap: wrap;
}

.capability-action {
  border: 1px solid #c7d2fe;
  background: #eef2ff;
  color: #3730a3;
  border-radius: 8px;
  padding: 8px 12px;
  cursor: pointer;
}
</style>
