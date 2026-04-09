<template>
  <section class="capability-page">
    <header class="capability-page__header">
      <h2 class="capability-page__title">Workflow</h2>
      <p class="capability-page__desc">
        hostMode={{ context.hostMode }} appKey={{ context.appKey ?? "-" }}
      </p>
    </header>

    <div class="capability-page__actions">
      <button type="button" class="capability-action" @click="goTo(workflowPath)">WorkflowList</button>
      <button type="button" class="capability-action" @click="goTo(logicFlowPath)">LogicFlow</button>
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

const workflowPath = computed(() => {
  if (context.value.hostMode === "app" && context.value.appKey) {
    return `/apps/${context.value.appKey}/workflows`;
  }
  if (context.value.appId) {
    return `/apps/${context.value.appId}/workflows`;
  }
  return "/workflow/list";
});

const logicFlowPath = computed(() => {
  if (context.value.hostMode === "app" && context.value.appKey) {
    return `/apps/${context.value.appKey}/logic-flow`;
  }
  if (context.value.appId) {
    return `/apps/${context.value.appId}/logic-flow`;
  }
  return "/apps";
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
