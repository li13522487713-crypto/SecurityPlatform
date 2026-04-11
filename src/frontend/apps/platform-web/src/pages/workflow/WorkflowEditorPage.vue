<template>
  <WorkflowEditorBridge
    :workflow-id="workflowId"
    :api-client="workflowV2Api"
    :locale="currentLocale"
    :on-back="backToList"
  />
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useRoute, useRouter } from "vue-router";
import { useI18n } from "vue-i18n";
import { workflowV2Api } from "@/services/api-workflow";
import { resolveCurrentAppId } from "@/utils/app-context";
import WorkflowEditorBridge from "@/components/workflow/WorkflowEditorBridge";

const route = useRoute();
const router = useRouter();
const { locale } = useI18n();
const workflowId = computed(() => String(route.params.id ?? ""));
const currentLocale = computed(() => locale.value);

function backToList() {
  const currentAppId = resolveCurrentAppId(route);
  if (!currentAppId) {
    void router.push("/console/apps");
    return;
  }
  void router.push(`/apps/${currentAppId}/workflows`);
}
</script>
