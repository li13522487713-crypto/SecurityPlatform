<template>
  <WorkflowEditorBridge
    :key="workflowId"
    :workflow-id="workflowId"
    :api-client="workflowV2Api"
    :locale="currentLocale"
    :read-only="readOnly"
    :detail-query="detailQuery"
    :on-back="backToList"
  />
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useI18n } from "vue-i18n";
import type { WorkflowDetailQuery } from "@atlas/workflow-editor-react";
import { workflowV2Api } from "@/services/api-workflow";
import WorkflowEditorBridge from "@/components/workflow/WorkflowEditorBridge.vue";

const route = useRoute();
const router = useRouter();
const { locale } = useI18n();
const workflowId = computed(() => String(route.params.id ?? ""));
const currentLocale = computed(() => locale.value);
const readOnly = computed(() => {
  const raw = String(route.query.readOnly ?? route.query.readonly ?? "");
  return raw === "1" || raw.toLowerCase() === "true";
});
const detailQuery = computed<WorkflowDetailQuery>(() => {
  const source = String(route.query.source ?? "").trim().toLowerCase();
  const versionId = String(route.query.versionId ?? "").trim();
  const query: WorkflowDetailQuery = {};
  if (source === "published" || source === "draft") {
    query.source = source;
  }
  if (versionId) {
    query.versionId = versionId;
  }
  return query;
});

function backToList() {
  void router.back();
}
</script>
