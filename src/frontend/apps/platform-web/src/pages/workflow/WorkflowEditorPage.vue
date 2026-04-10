<template>
  <WorkflowEditor :workflow-id="workflowId" :api-client="workflowV2Api" @back="backToList" />
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useRoute, useRouter } from "vue-router";
import { WorkflowEditor } from "@atlas/workflow-editor";
import { workflowV2Api } from "@/services/api-workflow";
import { resolveCurrentAppId } from "@/utils/app-context";

const route = useRoute();
const router = useRouter();
const workflowId = computed(() => String(route.params.id ?? ""));

function backToList() {
  const currentAppId = resolveCurrentAppId(route);
  if (!currentAppId) {
    void router.push("/console/apps");
    return;
  }
  void router.push(`/apps/${currentAppId}/workflows`);
}
</script>
