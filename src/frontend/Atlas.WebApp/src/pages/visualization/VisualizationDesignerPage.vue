<template>
  <a-page-header title="流程设计器" sub-title="创建和编辑审批流程" />
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
            <a-form-item label="流程名称">
              <a-input v-model:value="definition.name" />
            </a-form-item>
            <a-form-item label="流程编码">
              <a-input v-model:value="definition.code" />
            </a-form-item>
            <a-form-item label="表单编码">
              <a-input v-model:value="definition.formCode" />
            </a-form-item>
            <a-form-item>
              <a-checkbox v-model:checked="definition.isLowCodeFlow">低代码流程</a-checkbox>
            </a-form-item>
            <a-form-item>
              <a-checkbox v-model:checked="definition.isOutSideProcess">外部流程</a-checkbox>
            </a-form-item>
          </a-form>
        </template>
        <template #node>
          <div v-if="selectedNode">
            <a-form layout="vertical">
              <a-form-item label="节点名称">
                <a-input v-model:value="selectedNode.name" />
              </a-form-item>
              <a-form-item label="节点类型">
                <a-tag color="blue">{{ selectedNode.type }}</a-tag>
              </a-form-item>
            </a-form>
          </div>
          <a-empty v-else description="请选择节点后编辑属性" />
        </template>
        <template #validate>
          <div v-if="validation">
            <a-alert v-if="validation.isValid" type="success" message="校验通过" show-icon />
            <a-alert v-else type="error" message="校验失败" show-icon />
            <ul class="msg-list">
              <li v-for="(err, idx) in validation.errors" :key="idx">{{ err }}</li>
              <li v-for="(warn, idx) in validation.warnings || []" :key="'w'+idx" class="warn">{{ warn }}</li>
            </ul>
          </div>
          <a-empty v-else description="暂无校验结果" />
        </template>
      </PropertyPanel>
    </div>
  </div>
</template>

<script setup lang="ts">
import { reactive, ref, onMounted } from "vue";
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
  }
});

const load = async (id: string) => {
  loading.value = true;
  try {
    const data = await loadFlowDefinition(id);
    Object.assign(definition, data);
    currentId.value = id;
  } catch (e) {
    message.error((e as Error).message || "加载流程失败");
  } finally {
    loading.value = false;
  }
};

const onSave = async () => {
  loading.value = true;
  try {
    if (currentId.value) {
      await updateFlowDefinition(currentId.value, { tenantId, definition });
      message.success("已更新");
    } else {
      const res = await saveFlowDefinition({ tenantId, definition });
      currentId.value = res.id;
      message.success("已保存");
    }
  } finally {
    loading.value = false;
  }
};

const onAddNode = (type: NodeType) => {
  const node = createNode(type);
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
  const start = createNode("start");
  const end = createNode("end");
  start.children = [end];
  definition.nodes.push(start);
  selectedNodeId.value = start.id;
  selectedNode.value = start;
};

const onValidate = async () => {
  loading.value = true;
  try {
    validation.value = await validateFlowDefinition({ tenantId, definition });
    if (validation.value.isValid) {
      message.success("校验通过");
    } else {
      message.error("校验未通过");
    }
  } finally {
    loading.value = false;
  }
};

const onPreview = async () => {
  message.info("预览功能待接入");
};

const onPublish = async () => {
  loading.value = true;
  try {
    if (!currentId.value) {
      message.warning("请先保存流程");
      return;
    }
    if (!validation.value?.isValid) {
      const proceed = await confirmProceed();
      if (!proceed) {
        return;
      }
    }
    await publishFlowDefinition(currentId.value);
    message.success("发布成功");
  } finally {
    loading.value = false;
  }
};

const confirmProceed = () => {
  return new Promise<boolean>((resolve) => {
    Modal.confirm({
      title: "尚未校验或存在错误",
      content: "确认继续发布？",
      okText: "继续发布",
      cancelText: "取消",
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
