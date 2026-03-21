<template>
  <a-page-header :title="t('visualization.designerTitle')" :sub-title="t('visualization.designerSubtitle')" />
  <div class="designer-shell">
    <DesignerToolbar
      :loading="loading"
      @save="onSave"
      @validate="onValidate"
      @preview="onPreview"
      @publish="onPublish"
    />
    <div class="designer-body">
      <div class="left-column">
        <PalettePanel @add="onAddNode" />
        <NodeList
          :nodes="definition.nodes"
          :selected-id="selectedNodeId"
          @select="onSelectNode"
          @add-default="addDefaultStartEnd"
        />
      </div>
      <DesignerCanvas />
      <PropertyPanel>
        <template #basic>
          <a-form layout="vertical">
            <a-form-item :label="t('visualization.labelFlowName')">
              <a-input v-model:value="definition.name" />
            </a-form-item>
            <a-form-item :label="t('visualization.labelFlowCode')">
              <a-input v-model:value="definition.code" />
            </a-form-item>
            <a-form-item :label="t('visualization.labelFormCode')">
              <a-input v-model:value="definition.formCode" />
            </a-form-item>
            <a-form-item>
              <a-checkbox v-model:checked="definition.isLowCodeFlow">{{ t("visualization.chkLowCodeFlow") }}</a-checkbox>
            </a-form-item>
            <a-form-item>
              <a-checkbox v-model:checked="definition.isOutSideProcess">{{ t("visualization.chkOutsideProcess") }}</a-checkbox>
            </a-form-item>
          </a-form>
        </template>
        <template #node>
          <div v-if="selectedNode">
            <a-form layout="vertical">
              <a-form-item :label="t('visualization.labelNodeName')">
                <a-input v-model:value="selectedNode.name" />
              </a-form-item>
              <a-form-item :label="t('visualization.labelNodeType')">
                <a-tag color="blue">{{ selectedNode.type }}</a-tag>
              </a-form-item>
            </a-form>
          </div>
          <a-empty v-else :description="t('visualization.emptySelectNode')" />
        </template>
        <template #validate>
          <div v-if="validation">
            <a-alert v-if="validation.isValid" type="success" :message="t('visualization.validateOk')" show-icon />
            <a-alert v-else type="error" :message="t('visualization.validateFail')" show-icon />
            <ul class="msg-list">
              <li v-for="err in validation.errors" :key="err">{{ err }}</li>
              <li v-for="warn in validation.warnings || []" :key="`w-${warn}`" class="warn">{{ warn }}</li>
            </ul>
          </div>
          <a-empty v-else :description="t('visualization.emptyValidate')" />
        </template>
      </PropertyPanel>
    </div>
  </div>
</template>

<script setup lang="ts">
import { reactive, ref, onMounted, onUnmounted } from "vue";
import { useI18n } from "vue-i18n";

const { t } = useI18n();

const isMounted = ref(false);
onMounted(() => { isMounted.value = true; });
onUnmounted(() => { isMounted.value = false; });

import { message, Modal } from "ant-design-vue";
import type { FlowDefinition, FlowNode, FlowValidationResult, NodeType } from "@/types/workflow";
import { createNode } from "@/components/designer/NodePalette";
import {
  loadFlowDefinition,
  saveFlowDefinition,
  updateFlowDefinition,
  validateFlowDefinition,
  publishFlowDefinition
} from "@/services/api";
import { useRoute } from "vue-router";

const loading = ref(false);
const route = useRoute();
const tenantId = localStorage.getItem("tenant_id") ?? "00000000-0000-0000-0000-000000000001";
const definition = reactive<FlowDefinition>({
  code: "",
  name: "",
  formCode: "",
  isLowCodeFlow: false,
  isOutSideProcess: false,
  nodes: []
});
const validation = ref<FlowValidationResult | null>(null);
const currentId = ref<string | null>(null);
const selectedNodeId = ref<string | null>(null);
const selectedNode = ref<FlowNode | null>(null);

onMounted(async () => {
  const id = (route.params.id as string) ?? null;
  if (id) {
    await load(id);

    if (!isMounted.value) return;
  }
});

const load = async (id: string) => {
  loading.value = true;
  try {
    const data  = await loadFlowDefinition(id);

    if (!isMounted.value) return;
    Object.assign(definition, data);
    currentId.value = id;
  } catch (e) {
    message.error((e as Error).message || t("visualization.loadFlowFailed"));
  } finally {
    loading.value = false;
  }
};

const onSave = async () => {
  loading.value = true;
  try {
    if (currentId.value) {
      await updateFlowDefinition(currentId.value, { tenantId, definition });

      if (!isMounted.value) return;
      message.success(t("visualization.updated"));
    } else {
      const res  = await saveFlowDefinition({ tenantId, definition });

      if (!isMounted.value) return;
      currentId.value = res.id;
      message.success(t("visualization.saved"));
    }
  } finally {
    loading.value = false;
  }
};

const paletteName = (type: NodeType) => t(`approvalPalette.${type.replace(/-/g, "_")}`);

const onAddNode = (type: NodeType) => {
  const node = createNode(type, paletteName(type));
  definition.nodes.push(node);
  selectedNodeId.value = node.id;
  selectedNode.value = node;
};

const onSelectNode = (id: string | undefined) => {
  if (!id) {
    selectedNodeId.value = null;
    selectedNode.value = null;
    return;
  }
  selectedNodeId.value = id;
  selectedNode.value = findNodeById(definition.nodes, id);
};

const addDefaultStartEnd = () => {
  if (definition.nodes.length > 0) return;
  const start = createNode("start", paletteName("start"));
  const end = createNode("end", paletteName("end"));
  start.children = [end];
  definition.nodes.push(start);
  selectedNodeId.value = start.id;
  selectedNode.value = start;
};

const onValidate = async () => {
  loading.value = true;
  try {
    validation.value = await validateFlowDefinition({ tenantId, definition });

    if (!isMounted.value) return;
    if (validation.value.isValid) {
      message.success(t("visualization.validatePassed"));
    } else {
      message.error(t("visualization.validateNotPassed"));
    }
  } finally {
    loading.value = false;
  }
};

const onPreview = async () => {
  message.info(t("visualization.previewPending"));
};

const onPublish = async () => {
  loading.value = true;
  try {
    if (!currentId.value) {
      message.warning(t("visualization.saveFlowFirst"));
      return;
    }
    if (!validation.value?.isValid) {
      const proceed  = await confirmProceed();

      if (!isMounted.value) return;
      if (!proceed) {
        return;
      }
    }
    await publishFlowDefinition(currentId.value);

    if (!isMounted.value) return;
    message.success(t("visualization.publishOk"));
  } finally {
    loading.value = false;
  }
};

const confirmProceed = () => {
  return new Promise<boolean>((resolve) => {
    Modal.confirm({
      title: t("visualization.publishConfirmTitle"),
      content: t("visualization.publishConfirmContent"),
      okText: t("visualization.continuePublish"),
      cancelText: t("common.cancel"),
      onOk: () => resolve(true),
      onCancel: () => resolve(false)
    });
  });
};

const findNodeById = (nodes: FlowNode[], id: string): FlowNode | null => {
  for (const node of nodes) {
    if (node.id === id) return node;
    if (node.children && node.children.length > 0) {
      const found = findNodeById(node.children, id);
      if (found) return found;
    }
  }
  return null;
};
</script>

<style scoped>
.designer-shell {
  display: flex;
  flex-direction: column;
  gap: 12px;
}
.designer-toolbar {
  display: flex;
  justify-content: space-between;
  padding: 8px 0;
}
.designer-body {
  display: grid;
  grid-template-columns: 2fr 1fr;
  gap: 12px;
  min-height: 600px;
}
.designer-canvas {
  border: 1px dashed var(--color-border-secondary);
  border-radius: 6px;
  padding: 12px;
  background: var(--color-bg-container);
}
.designer-panel {
  border: 1px solid var(--color-bg-hover);
  border-radius: 6px;
  background: var(--color-bg-container);
  padding: 12px;
}
.msg-list {
  margin: 8px 0 0;
  padding-left: 16px;
}
.msg-list .warn {
  color: var(--color-warning);
}
</style>
