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
import WorkflowEditorBridge from "@/components/workflow/WorkflowEditorBridge";

const route = useRoute();
const router = useRouter();
const { locale } = useI18n();
const workflowId = computed(() => String(route.params.id ?? ""));
const currentLocale = computed(() => locale.value);

function backToList() {
  void router.back();
}
</script>
