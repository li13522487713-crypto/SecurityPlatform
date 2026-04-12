<template>
  <div ref="containerRef" class="workflow-editor-bridge"></div>
</template>

<script setup lang="ts">
import { createElement } from "react";
import { createRoot, type Root } from "react-dom/client";
import { onBeforeUnmount, onMounted, ref, toRaw, watch } from "vue";
import {
  WorkflowEditor,
  type WorkflowApiClient,
  type WorkflowDetailQuery
} from "@atlas/workflow-editor-react";

interface Props {
  workflowId: string;
  locale?: string;
  readOnly?: boolean;
  detailQuery?: WorkflowDetailQuery;
  apiClient: WorkflowApiClient;
  onBack?: () => void;
}

const props = defineProps<Props>();
const containerRef = ref<HTMLElement | null>(null);
const rootRef = ref<Root | null>(null);
const isMounted = ref(false);
let renderQueued = false;

function renderEditor() {
  const container = containerRef.value;
  if (!container) {
    return;
  }

  if (!rootRef.value) {
    rootRef.value = createRoot(container);
  }

  rootRef.value.render(
    createElement(WorkflowEditor, {
      workflowId: props.workflowId,
      locale: props.locale,
      readOnly: props.readOnly,
      detailQuery: props.detailQuery,
      apiClient: toRaw(props.apiClient),
      onBack: props.onBack
    })
  );
}

function queueRender() {
  if (renderQueued) {
    return;
  }

  renderQueued = true;
  queueMicrotask(() => {
    renderQueued = false;
    if (!isMounted.value) {
      return;
    }
    renderEditor();
  });
}

onMounted(() => {
  isMounted.value = true;
  queueRender();
});

watch(
  () => [props.workflowId, props.locale, props.readOnly, props.apiClient, props.onBack],
  () => {
    queueRender();
  },
  { deep: false }
);

onBeforeUnmount(() => {
  isMounted.value = false;
  rootRef.value?.unmount();
  rootRef.value = null;
  if (containerRef.value) {
    containerRef.value.replaceChildren();
  }
});
</script>

<style scoped>
.workflow-editor-bridge {
  width: 100%;
  height: 100%;
  min-height: 0;
}
</style>
