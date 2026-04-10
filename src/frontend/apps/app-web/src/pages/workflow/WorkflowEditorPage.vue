<template>
  <WorkflowEditor :workflow-id="workflowId" :api-client="workflowV2Api" @back="backToList" />
</template>

<script setup lang="ts">
import { computed } from "vue";
import { useRoute, useRouter } from "vue-router";
import { WorkflowEditor } from "@atlas/workflow-editor";
import { workflowV2Api } from "@/services/api-workflow";

const route = useRoute();
const router = useRouter();
const workflowId = computed(() => String(route.params.id ?? ""));

function backToList() {
  void router.back();
}
</script>
